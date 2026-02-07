using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Json;
using Oculus.Voice;

/// <summary>
/// Voice Command Handler for HRI Experiment
/// Listens for voice commands like "로봇 시작", "start", "press" to trigger robot action
/// </summary>
public class VoiceCommandHandler : MonoBehaviour
{
    [Header("Voice Experience")]
    public AppVoiceExperience voiceExperience;
    
    [Header("Experiment Manager")]
    public HRIExperimentManager experimentManager;
    
    [Header("Settings")]
    public bool autoActivate = true;
    public float activationCooldown = 2f;
    
    [Header("Command Keywords")]
    public string[] startCommands = { "start", "go", "press", "로봇", "시작", "눌러" };
    
    private bool isListening = false;
    private float lastCommandTime = 0f;

    void Start()
    {
        if (voiceExperience == null)
        {
            voiceExperience = FindFirstObjectByType<AppVoiceExperience>();
        }
        
        if (voiceExperience != null)
        {
            // Subscribe to voice events
            voiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
            voiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);
            voiceExperience.VoiceEvents.OnStartListening.AddListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnStoppedListening);
            voiceExperience.VoiceEvents.OnError.AddListener(OnError);
            
            Debug.Log("[VoiceCommand] Voice command handler initialized");
            
            if (autoActivate)
            {
                StartListening();
            }
        }
        else
        {
            Debug.LogError("[VoiceCommand] AppVoiceExperience not found!");
        }
    }

    public void StartListening()
    {
        if (voiceExperience != null && !isListening)
        {
            voiceExperience.Activate();
            Debug.Log("[VoiceCommand] Listening for voice commands...");
        }
    }

    public void StopListening()
    {
        if (voiceExperience != null && isListening)
        {
            voiceExperience.Deactivate();
        }
    }

    void OnStartListening()
    {
        isListening = true;
        Debug.Log("[VoiceCommand] Started listening");
    }

    void OnStoppedListening()
    {
        isListening = false;
        Debug.Log("[VoiceCommand] Stopped listening");
        
        // Auto-restart listening
        if (autoActivate)
        {
            Invoke(nameof(StartListening), 0.5f);
        }
    }

    void OnPartialTranscription(string transcription)
    {
        Debug.Log($"[VoiceCommand] Partial: {transcription}");
        CheckForCommand(transcription);
    }

    void OnFullTranscription(string transcription)
    {
        Debug.Log($"[VoiceCommand] Full: {transcription}");
        CheckForCommand(transcription);
    }

    void CheckForCommand(string transcription)
    {
        // Cooldown check
        if (Time.time - lastCommandTime < activationCooldown)
            return;

        string lower = transcription.ToLower();
        
        foreach (string cmd in startCommands)
        {
            if (lower.Contains(cmd.ToLower()))
            {
                ExecuteRobotCommand();
                lastCommandTime = Time.time;
                return;
            }
        }
    }

    void ExecuteRobotCommand()
    {
        Debug.Log("[VoiceCommand] Robot command detected!");
        
        if (experimentManager != null)
        {
            experimentManager.OnRobotButtonClicked();
        }
        else
        {
            Debug.LogWarning("[VoiceCommand] ExperimentManager not assigned!");
        }
    }

    void OnError(string error, string message)
    {
        Debug.LogError($"[VoiceCommand] Error: {error} - {message}");
        
        // Auto-restart on error
        if (autoActivate)
        {
            Invoke(nameof(StartListening), 1f);
        }
    }

    void OnDestroy()
    {
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
            voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            voiceExperience.VoiceEvents.OnError.RemoveListener(OnError);
        }
    }

    [ContextMenu("Test Voice Command")]
    void TestVoiceCommand()
    {
        ExecuteRobotCommand();
    }
}
