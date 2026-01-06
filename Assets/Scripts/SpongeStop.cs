using UnityEngine;

/// <summary>
/// Sponge stopper for assembly holes - simplified version
/// Blocks parts until disabled by PressableObject when robot presses
/// 
/// SETUP:
/// 1. Create child object "CylinderStop" or "CubeStop" under Base
/// 2. Add BoxCollider (Is Trigger = OFF) - this blocks parts
/// 3. Add this script
/// 4. On Cylinder/Cube prefabs: set PressableObject.spongeStop reference
/// </summary>
public class SpongeStop : MonoBehaviour
{
    [Header("Collider Reference")]
    [Tooltip("Solid collider that blocks parts (Is Trigger = OFF)")]
    public Collider stopCollider;
    
    [Header("State")]
    public bool isPressed = false;
    
    void Start()
    {
        // Auto-find solid collider if not assigned
        if (stopCollider == null)
        {
            stopCollider = GetComponent<Collider>();
            if (stopCollider != null && stopCollider.isTrigger)
            {
                Debug.LogWarning($"[SpongeStop] {gameObject.name}: Collider should NOT be a trigger!");
            }
        }
        
        if (stopCollider == null)
            Debug.LogWarning($"[SpongeStop] {gameObject.name}: No collider found!");
    }
    
    /// <summary>
    /// Called by PressableObject when robot gripper touches the part
    /// </summary>
    public void DisableStop()
    {
        Debug.Log($"[SpongeStop] DisableStop called for {gameObject.name}");
        if (stopCollider != null)
        {
            stopCollider.enabled = false;
            isPressed = true;
            Debug.Log($"[SpongeStop] {stopCollider.name} collider DISABLED! (Enabled: {stopCollider.enabled})");
        }
        else
        {
            Debug.LogError($"[SpongeStop] Cannot disable - stopCollider is NULL on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Re-enable for next trial
    /// </summary>
    public void EnableStop()
    {
        if (stopCollider != null)
        {
            stopCollider.enabled = true;
            isPressed = false;
        }
    }
    
    public void Reset() => EnableStop();
    
    [ContextMenu("Disable Stop")]
    void DisableFromMenu() => DisableStop();
    
    [ContextMenu("Enable Stop")]
    void EnableFromMenu() => EnableStop();
}
