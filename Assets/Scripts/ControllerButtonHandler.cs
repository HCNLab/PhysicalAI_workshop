using UnityEngine;

/// <summary>
/// Controller Button Handler for HRI Experiment
/// Detects A/B/X/Y button presses on Meta Quest controllers
/// </summary>
public class ControllerButtonHandler : MonoBehaviour
{
    [Header("Experiment Manager")]
    public HRIExperimentManager experimentManager;
    
    [Header("Button Settings")]
    public OVRInput.Button triggerButton = OVRInput.Button.One; // A button (Right) or X button (Left)
    public OVRInput.Controller controller = OVRInput.Controller.RTouch; // Right controller
    
    [Header("Feedback")]
    public bool vibrationFeedback = true;
    public float vibrationDuration = 0.1f;
    public float vibrationStrength = 0.5f;

    private bool buttonPressed = false;

    void Update()
    {
        // Check for button press
        if (OVRInput.GetDown(triggerButton, controller))
        {
            if (!buttonPressed)
            {
                buttonPressed = true;
                OnButtonPressed();
            }
        }
        
        // Check for button release
        if (OVRInput.GetUp(triggerButton, controller))
        {
            buttonPressed = false;
        }
    }

    void OnButtonPressed()
    {
        Debug.Log($"[ControllerButton] Button pressed: {triggerButton} on {controller}");
        
        // Vibration feedback
        if (vibrationFeedback)
        {
            StartCoroutine(Vibrate());
        }
        
        // Trigger robot command
        if (experimentManager != null)
        {
            experimentManager.OnRobotButtonClicked();
        }
        else
        {
            Debug.LogWarning("[ControllerButton] ExperimentManager not assigned!");
        }
    }

    System.Collections.IEnumerator Vibrate()
    {
        OVRInput.SetControllerVibration(vibrationStrength, vibrationStrength, controller);
        yield return new WaitForSeconds(vibrationDuration);
        OVRInput.SetControllerVibration(0, 0, controller);
    }

    void OnEnable()
    {
        Debug.Log($"[ControllerButton] Listening for {triggerButton} on {controller}");
    }

    [ContextMenu("Test Button Press")]
    void TestButtonPress()
    {
        OnButtonPressed();
    }
}
