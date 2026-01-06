using UnityEngine;

/// <summary>
/// Detects when an object is picked up from the pickup zone.
/// Attach to: PickupZone object with BoxCollider (IsTrigger = true)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PickupZoneTrigger : MonoBehaviour
{
    [Header("References")]
    public ObjectSpawner objectSpawner;
    
    [Header("Settings")]
    [Tooltip("Tags to detect as pickable objects")]
    public string[] pickableTags = { "Base", "Cylinder", "Cube" };
    
    private GameObject objectInZone;
    private bool hasNotifiedPickup = false;
    
    void Start()
    {
        // Ensure collider is trigger
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!IsPickableObject(other.gameObject)) return;
        
        objectInZone = other.gameObject;
        hasNotifiedPickup = false;
        
        // Debug.Log($"[PickupZoneTrigger] Object entered: {other.gameObject.name}");
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject != objectInZone) return;
        
        // Object was picked up (left the zone)
        if (!hasNotifiedPickup)
        {
            hasNotifiedPickup = true;
            
            // Debug.Log($"[PickupZoneTrigger] Object picked up: {other.gameObject.name}");
            
            // Re-enable physics for the picked object
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
            
            // Notify spawner to spawn next object
            if (objectSpawner != null)
            {
                objectSpawner.OnObjectPickedUp();
            }
        }
        
        objectInZone = null;
    }
    
    bool IsPickableObject(GameObject obj)
    {
        foreach (string tag in pickableTags)
        {
            if (obj.CompareTag(tag))
                return true;
        }
        return false;
    }
}
