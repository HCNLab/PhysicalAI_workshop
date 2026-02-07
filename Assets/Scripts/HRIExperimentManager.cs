using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using System;
using System.Collections;
using TMPro;

/// <summary>
/// HRI Experiment Manager
/// Manages the assembly task experiment with error injection and feedback
/// </summary>
public class HRIExperimentManager : MonoBehaviour
{
    private ROSConnection ros;

    [Header("Experiment Settings")]
    public int totalTrials = 5;
    public int currentTrial = 0;
    
    [Header("Error Injection")]
    public ErrorType currentErrorType = ErrorType.None;
    public bool randomizeErrors = true;  // Auto-randomize each trial
    
    // Which object type to ignore press detection for (Miss/Air actions)
    // "Cylinder", "Cube", "All", or "" for none
    public static string ignoreTargetType = "";
    
    [Header("Trial Flow")]
    public bool autoAdvance = true;      // Auto-start next trial
    public float cleanupDelay = 5.0f;    // Time for participant to clear table

    [Header("Test mode A:B")]
    public GameObject desk_pick_place;
    public GameObject conveyor_group;
    
    [Header("UI Elements")]
    public Button robotCommandButton;
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI trialCounterText;
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI expectedOrderText;  // Shows expected robot order for ErrP
    public GameObject feedbackPanel;  // Shared panel for instruction and feedback

    [Header("Assembly Objects")]
    public Transform framePosition;
    public GameObject baseObject;
    public GameObject cylinderObject;
    public GameObject cubeObject;

    [Header("ROS Topics")]
    public string robotCommandTopic = "/unity/robot_command";
    public string robotStatusTopic = "/unity/robot_status";
    public string eegMarkerTopic = "/unity/eeg_marker";

    [Header("EEG Integration")]
    public EEGDataLogger eegDataLogger;
    public LSLManager lslManager;
    public AssemblyDetector assemblyDetector;
    
    [Header("Conveyor Belt")]
    public ObjectSpawner objectSpawner;
    public GameObject objectSpanwer_obj;

    [Header("Conveyor Control in the main view")]
    public ConveyorBeltMover conveyorBelt_L;  // Physics mover
    public ConveyorBeltScroll conveyorScroll_L;  // Visual texture scroll
    public ConveyorBeltMover conveyorBelt_R;  // Physics mover
    public ConveyorBeltScroll conveyorScroll_R;  // Visual texture scroll

    [Header("Audio (Optional)")]
    public AudioClip successSound;
    public AudioClip failureSound;
    public AudioSource audioSource;

    // Experiment State
    public enum ExperimentState { Waiting, Assembling, RobotWorking, Feedback, Cleanup }
    public ExperimentState currentState = ExperimentState.Waiting;

    // Error Types
    public enum ErrorType { None }

    // Events for data logging
    public event Action<int, float> OnTrialStart;
    public event Action<int, float, bool, ErrorType> OnTrialEnd;
    public event Action<float> OnRobotCommandPressed;

    // Timing
    private float trialStartTime;
    private float assemblyCompleteTime;
    private float robotCommandTime;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        // Register ROS publishers
        ros.RegisterPublisher<StringMsg>(robotCommandTopic);
        
        // Subscribe to robot status
        ros.Subscribe<StringMsg>(robotStatusTopic, OnRobotStatus);
        
        // Subscribe to EEG markers from ROS
        ros.Subscribe<StringMsg>(eegMarkerTopic, OnEEGMarkerReceived);

        // Hide feedback initially
        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);

        // Auto-find EEGDataLogger if not assigned
        if (eegDataLogger == null)
            eegDataLogger = FindFirstObjectByType<EEGDataLogger>();
        
        // Auto-find LSLManager if not assigned
        if (lslManager == null)
            lslManager = FindFirstObjectByType<LSLManager>();
        
        if (assemblyDetector == null)
            assemblyDetector = FindFirstObjectByType<AssemblyDetector>();
        
        // Subscribe to object placement events for instruction updates
        if (assemblyDetector != null)
            assemblyDetector.OnObjectPlaced += OnObjectPlacedForInstruction;

        UpdateUI();
        init_conveyor();
        Debug.Log("[HRIExperimentManager] Initialized");
    }

    void OnEEGMarkerReceived(StringMsg msg)
    {
        string marker = msg.data;
        Debug.Log($"[HRIExperimentManager] EEG Marker received: {marker}");
        
        // Check for IGNORE command from Python (for MISS/AIR actions)
        if (marker.StartsWith("IGNORE:"))
        {
            string target = marker.Substring(7); // "cylinder" or "cube"
            ignoreTargetType = target;
            Debug.Log($"[HRIExperimentManager] Press detection IGNORE set for: {target}");
            return; // Don't forward this internal command as marker
        }
        
        // Forward to EEGDataLogger
        if (eegDataLogger != null)
        {
            eegDataLogger.LogEvent("ROBOT", marker);
        }
        
        // Forward to LSLManager - parse numeric code if possible
        if (lslManager != null)
        {
            if (int.TryParse(marker, out int code))
            {
                // Numeric code from Python - send as proper marker
                lslManager.SendMarker(code);
            }
            else
            {
                // Text marker - send as text
                lslManager.SendMarkerText("ROBOT", marker);
            }
        }
    }

    public void init_conveyor()
    {
        conveyorBelt_L.isRunning = false;
        conveyorBelt_R.isRunning = false;
        conveyorScroll_L.isRunning = false;
        conveyorScroll_R.isRunning = false;
    }

    public void move_conveyor()
    {
        conveyorBelt_L.isRunning = true;
        conveyorBelt_R.isRunning = true;
        conveyorScroll_L.isRunning = true;
        conveyorScroll_R.isRunning = true;
    }

    public void StartExperiment()
    {
        currentTrial = 0;

        desk_pick_place.SetActive(false);
        conveyor_group.SetActive(true);
        move_conveyor();
        objectSpanwer_obj.SetActive(true);
        StartNextTrial();
    }

    public void StartNextTrial()
    {
        currentTrial++;
        if (currentTrial > totalTrials)
        {
            EndExperiment();
            return;
        }

        trialStartTime = Time.time;
        currentState = ExperimentState.Assembling;

 
        OnTrialStart?.Invoke(currentTrial, trialStartTime);
        // Send LSL marker: Trial Start
        if (lslManager != null)
            lslManager.SendTrialStart(currentTrial);
        
        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);
        
        // Hide expected order at trial start
        if (expectedOrderText != null)
            expectedOrderText.text = "";
        
        if (robotCommandButton != null)
            robotCommandButton.interactable = false;
        
        // Start conveyor belt spawning
        if (objectSpawner != null)
            objectSpawner.StartNewTrial();
        
        // Set initial instruction and show panel
        if (instructionText != null)
            instructionText.text = "베이스를 프레임에 올려주세요";
        if (feedbackText != null)
            feedbackText.text = "";  // Clear previous feedback
        if (feedbackPanel != null)
            feedbackPanel.SetActive(true);

        UpdateUI();
        Debug.Log($"[HRIExperimentManager] Trial {currentTrial} started | Error Type: {currentErrorType}");
    }
    
    /// <summary>
    /// Called when an object is placed on the assembly (by AssemblyDetector)
    /// Updates the instruction text for the next step
    /// </summary>
    public void OnObjectPlacedForInstruction(string objectName)
    {
        if (instructionText == null) return;
        
        switch (objectName)
        {
            case "Base":
                instructionText.text = "실린더를 베이스에 넣어주세요";
                break;
            case "Cylinder":
                instructionText.text = "큐브를 베이스에 넣어주세요";
                break;
            case "Cube":
                // Assembly complete - instruction will be updated by OnAssemblyComplete
                break;
        }
    }
    

    /// <summary>
    /// Called when all assembly objects are in place
    /// </summary>
    public void OnAssemblyComplete()
    {
        if (currentState != ExperimentState.Assembling) return;

        assemblyCompleteTime = Time.time;
        
        // Send LSL marker: Assembly Complete
        if (lslManager != null)
            lslManager.SendAssemblyComplete();
        
        if (instructionText != null)
            instructionText.text = "조립 완료! 로봇 대기 중...\n실린더 → 큐브";

        Debug.Log("[HRIExperimentManager] Assembly complete, auto-triggering robot in 1.5s...");       
        // Auto-trigger robot after delay (instead of waiting for button)
        StartCoroutine(AutoTriggerRobotAfterDelay(1.5f));
    }
    
    /// <summary>
    /// Coroutine to automatically trigger robot after assembly is complete
    /// </summary>
    private IEnumerator AutoTriggerRobotAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        
        // Make sure we're still in Assembling state (not cancelled)
        if (currentState == ExperimentState.Assembling)
        {
            Debug.Log("[HRIExperimentManager] Auto-triggering robot command...");
            OnRobotButtonClicked(); // Reuse existing logic
        }
    }

    public void OnRobotButtonClicked()
    {
        if (currentState != ExperimentState.Assembling) return;
        
        // Only allow if assembly is actually complete
        if (assemblyDetector != null && !assemblyDetector.isAssemblyComplete)
        {
            Debug.Log("[HRIExperimentManager] Button clicked but assembly not complete - ignoring");
            return;
        }

        robotCommandTime = Time.time;
        float reactionTime = robotCommandTime - assemblyCompleteTime;
        
        OnRobotCommandPressed?.Invoke(reactionTime);
        
        // Send LSL marker: Button Press
        if (lslManager != null)
            lslManager.SendButtonPress();
        
        currentState = ExperimentState.RobotWorking;
        
        if (robotCommandButton != null)
            robotCommandButton.interactable = false;

        if (instructionText != null)
            instructionText.text = "로봇 작동 중...\n실린더 → 큐브";

        // Send robot command based on error type
        SendRobotCommand();
        
        Debug.Log($"[HRIExperimentManager] Robot command sent, reaction time: {reactionTime:F2}s");
    }

    void SendRobotCommand()
    {
        string command = "";
        bool isError = (currentErrorType != ErrorType.None);
        
        // Send LSL marker: Robot Start (with error flag)
        if (lslManager != null)
            lslManager.SendRobotStart(isError);

        // Reset ignore target first
        ignoreTargetType = "";
        
        if (currentErrorType == ErrorType.None)
        {
            // Send correct command (Normal or FeedbackError)
            command = "PRESS_CORRECT";
        }
        ros.Publish(robotCommandTopic, new StringMsg(command));
    }

    void OnRobotStatus(StringMsg msg)
    {
        if (currentState != ExperimentState.RobotWorking) return;

        string status = msg.data;

        if (status == "COMPLETE")
        {
            // Finalize assembly (attach objects for cleanup)
            if (assemblyDetector != null)
                assemblyDetector.FinalizeAssembly();

            StartCoroutine(ShowFeedback());
        }
    }

    /// <summary>
    /// Call this from ROS when robot action is complete
    /// Or trigger manually for testing
    /// </summary>
    public void OnRobotActionComplete()
    {
        if (currentState == ExperimentState.RobotWorking)
        {
            // Finalize assembly (attach objects for cleanup)
            if (assemblyDetector != null)
                assemblyDetector.FinalizeAssembly();

            StartCoroutine(ShowFeedback());
        }
    }

    IEnumerator ShowFeedback()
    {
        currentState = ExperimentState.Feedback;
        
        bool showSuccess;
        // Normal: show success
        showSuccess = true;
            
        // Send LSL marker: Feedback Correct
        if (lslManager != null)

        // Show feedback UI and hide instruction panel
        if (feedbackPanel != null)
            feedbackPanel.SetActive(true);
        // feedbackPanel stays active - it's shared for both instruction and feedback

        if (feedbackText != null)
        {
            feedbackText.text = showSuccess ? "성공" : "실패";
            feedbackText.color = showSuccess ? Color.green : Color.red;
        }

        // Play sound
        if (audioSource != null)
        {
            AudioClip clip = showSuccess ? successSound : failureSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        // Show cleanup instruction (on feedbackPanel or separate)
        if (instructionText != null)
            instructionText.text = "완료된 베이스를 왼쪽 컨베이어벨트에 올려주세요";

        // Log trial end
        float trialDuration = Time.time - trialStartTime;
        OnTrialEnd?.Invoke(currentTrial, trialDuration, showSuccess, currentErrorType);
        
        // Send LSL marker: Trial End
        if (lslManager != null)
            lslManager.SendTrialEnd(currentTrial);

        yield return new WaitForSeconds(2f);
        
        // FinalizeAssembly moved to OnRobotStatus for immediate effect        
        currentState = ExperimentState.Cleanup;
        
        if (autoAdvance)
        {
            if (instructionText != null)
                instructionText.text = $"다음 트라이얼까지 {cleanupDelay:F0}초...";
                
            yield return new WaitForSeconds(cleanupDelay);
            
            OnCleanupComplete();
        }
    }

    /// <summary>
    /// Call when participant has cleaned up and is ready for next trial
    /// </summary>
    public void OnCleanupComplete()
    {
        if (currentState != ExperimentState.Cleanup) return;

        StartNextTrial();
    }

    void EndExperiment()
    {
        currentState = ExperimentState.Waiting;
        
        if (feedbackText != null)
        {
            feedbackText.color = Color.green;
            feedbackText.text = "실험 종료!";
        }
        
        if (instructionText != null)
            instructionText.text = "모든 트라이얼 완료. 참여해 주셔서 감사합니다!";

        Debug.Log("[HRIExperimentManager] Experiment complete");
        objectSpanwer_obj.SetActive(false);
    }

    void UpdateUI()
    {
        if (trialCounterText != null)
            trialCounterText.text = $"트라이얼: {currentTrial} / {totalTrials}";
        
        // Note: Don't clear instructionText here - it's managed by step-by-step instructions
    }

    /// <summary>
    /// Set error type for current/next trial
    /// </summary>
    public void SetErrorType(ErrorType errorType)
    {
        currentErrorType = errorType;
        Debug.Log($"[HRIExperimentManager] Error type set to: {errorType}");
    }

    // Inspector test buttons
    [ContextMenu("Start Experiment")]
    void StartExperimentFromMenu() => StartExperiment();

    [ContextMenu("Simulate Assembly Complete")]
    void SimulateAssemblyComplete() => OnAssemblyComplete();

    [ContextMenu("Simulate Robot Complete")]
    void SimulateRobotComplete() => OnRobotActionComplete();

    [ContextMenu("Simulate Cleanup Complete")]
    void SimulateCleanupComplete() => OnCleanupComplete();

    [ContextMenu("Set Error: None")]
    void SetErrorNone() => SetErrorType(ErrorType.None);


    [ContextMenu("Simulate Button Click")]
    void SimulateButtonClick()
    {
        Debug.Log("[HRIExperimentManager] Simulating button click...");
        OnRobotButtonClicked();
    }

    [ContextMenu("Test Full Flow (Auto)")]
    void TestFullFlow()
    {
        Debug.Log("[HRIExperimentManager] Testing full flow...");
        StartCoroutine(TestFullFlowCoroutine());
    }

    IEnumerator TestFullFlowCoroutine()
    {
        // 1. Start experiment
        StartExperiment();
        yield return new WaitForSeconds(0.5f);
        
        // 2. Simulate assembly complete
        OnAssemblyComplete();
        yield return new WaitForSeconds(0.5f);
        
        // 3. Simulate button click
        OnRobotButtonClicked();
        yield return new WaitForSeconds(1f);
        
        // 4. Simulate robot complete
        OnRobotActionComplete();
        
        Debug.Log("[HRIExperimentManager] Full flow test complete!");
    }
}
