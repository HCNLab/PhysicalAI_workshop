using UnityEngine;
using System.Collections;

/// <summary>
/// Pressable object for assembly - detects robot gripper contact
/// Attach to: Cylinder, Cube objects
/// </summary>
public class PressableObject : MonoBehaviour
{
    [Header("State")]
    public bool isAssembled = false;
    public bool isFullyPressed = false;
    
    [Header("Reference")]
    public SpongeStop spongeStop;  // Optional reference to sponge stop
    
    [Header("Robot Detection")]
    [Tooltip("Keywords to identify robot gripper")]
    public string[] robotKeywords = { "gripper", "jaw", "end_effector", "tool" };

    void Awake()
    {
        // Ensure keywords are set correctly even if Inspector has old values
        robotKeywords = new string[] { "gripper", "jaw", "end_effector", "tool" };
    }

    void Start()
    {
        // 런타임에 SpongeStop 자동 찾기 (Base가 동적 생성되므로)
        if (spongeStop == null)
        {
            string targetName = gameObject.name.Contains("Cylinder") ? "CylinderStop" : "CubeStop";
            GameObject stopper = GameObject.Find(targetName);
            
            if (stopper != null)
            {
                spongeStop = stopper.GetComponent<SpongeStop>();
                Debug.Log($"[PressableObject] Auto-connected to {targetName} (Success)");
            }
            else
            {
                Debug.LogWarning($"[PressableObject] Could not find {targetName} in scene! Name mismatch?");
            }
        }
    }

    /// <summary>
    /// Called when object is placed in assembly slot
    /// </summary>
    public void OnAssembled()
    {
        isAssembled = true;
    }

    /// <summary>
    /// Called when object is picked up
    /// </summary>
    public void OnPickedUp()
    {
        isAssembled = false;
        isFullyPressed = false;
        
        // Re-enable sponge stop for next placement
        if (spongeStop != null)
            spongeStop.EnableStop();
    }

    /// <summary>
    /// Called when robot presses the object
    /// </summary>
    [Header("Behavior")]
    public float pressDelay = 1.0f;  // Seconds to wait before falling

    /// <summary>
    /// Called when robot presses the object
    /// </summary>
    public void OnRobotPressed()
    {
        if (isFullyPressed) return;
        
        isFullyPressed = true;
        Debug.Log($"[PressableObject] {gameObject.name} pressed by robot - waiting {pressDelay}s");
        
        StartCoroutine(DisableStopWithDelay());
    }

    IEnumerator DisableStopWithDelay()
    {
        yield return new WaitForSeconds(pressDelay);
        
        // Disable sponge stop so object can fall through
        if (spongeStop != null)
        {
            spongeStop.DisableStop();
            // Debug.Log($"[PressableObject] SpongeStop disabled after delay");
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (isFullyPressed) return;
        
        // 0. Check if we should ignore press detection for THIS object (MISS/AIR actions)
        string myType = gameObject.name.ToLower().Contains("cylinder") ? "cylinder" : "cube";
        string ignoreType = HRIExperimentManager.ignoreTargetType;
        
        // Debug Log for troubleshooting
        if (!string.IsNullOrEmpty(ignoreType))
        {
            Debug.Log($"[PressableObject DEBUG] Checking Ignore: MyType='{myType}', IgnoreType='{ignoreType}'");
        }

        if (!string.IsNullOrEmpty(ignoreType) && ignoreType.ToLower() == myType)
        {
            Debug.Log($"[PressableObject] BLOCKED press for {myType} (Match found!)");
            return;
        }
        
        // 1. Robot Keyword Check
        if (!IsRobotGripper(collision.collider)) return;

        // 2. Contact Normal Check (위에서 눌렀는지 확인)
        ContactPoint contact = collision.contacts[0];
        Vector3 normal = contact.normal;
        
        Debug.Log($"[PressableObject] Robot Contact! Normal: {normal}, Y={normal.y:F3}");

        // Relaxed threshold: -0.4 (allows more angled presses, robot gripper might be angled)
        if (normal.y < -0.4f)
        {
            Debug.Log($"[PressableObject] Valid Top-Down Press Detected! (Source: {collision.collider.name})");
            OnRobotPressed();
        }
        else
        {
            Debug.Log($"[PressableObject] Ignored (Not from top). Normal.y = {normal.y:F3}");
        }
    }
    
    bool IsRobotGripper(Collider other)
    {
        string objName = other.name.ToLower();
        foreach (string keyword in robotKeywords)
        {
            if (objName.Contains(keyword.ToLower()))
                return true;
        }
        return false;
    }

    [ContextMenu("Set As Assembled")]
    void SetAssembled() => OnAssembled();

    [ContextMenu("Simulate Robot Press")]
    void SimulatePress() => OnRobotPressed();
}

