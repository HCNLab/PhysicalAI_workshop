using UnityEngine;
using System.Collections;
using Oculus.Interaction;

/// <summary>
/// Spawns assembly objects on conveyor belt one at a time.
/// Objects: Base → Cylinder → Cube (in order)
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform spawnPoint;       // Where objects spawn
    public Transform pickupZone;       // Where objects stop for pickup
    
    [Header("Object Prefabs")]
    public GameObject basePrefab;
    public GameObject cylinderPrefab;
    public GameObject cubePrefab;
    
    [Header("Spawn Rotations (Euler Angles)")]
    [Tooltip("Rotation for Base when spawned (e.g. 90,0,0 to lay flat)")]
    public Vector3 baseRotation = new Vector3(90, 0, 0);  // Lay flat by default
    public Vector3 cylinderRotation = Vector3.zero;
    public Vector3 cubeRotation = Vector3.zero;
    
    [Header("Timing")]
    public float spawnDelay = 0.5f;    // Delay before spawning
    public float stopDistance = 0.1f;  // How close to pickup zone to stop (increased to prevent overshoot)
    public float spawnHeightOffset = 0.05f; // Lift object slightly to prevent clipping
    
    [Header("Conveyor Control")]
    public ConveyorBeltMover conveyorBelt;  // Physics mover
    public ConveyorBeltScroll conveyorScroll;  // Visual texture scroll
    
    [Header("Current State")]
    public int currentObjectIndex = 0; // 0=Base, 1=Cylinder, 2=Cube
    public GameObject currentObject;
    public bool isWaitingForPickup = false;
    
    private string[] objectNames = { "Base", "Cylinder", "Cube" };
    
    void Start()
    {
        if (spawnPoint == null)
            Debug.LogWarning("[ObjectSpawner] SpawnPoint not set!");
        if (pickupZone == null)
            Debug.LogWarning("[ObjectSpawner] PickupZone not set!");
    }
    
    /// <summary>
    /// Start spawning sequence for a new trial
    /// </summary>
    public void StartNewTrial()
    {
        Debug.Log("[ObjectSpawner] StartNewTrial() called");
        currentObjectIndex = 0;
        isWaitingForPickup = false;
        objectAtPickupZone = false;
        SpawnNextObject();
    }
    
    /// <summary>
    /// Spawn the next object in sequence
    /// </summary>
    public void SpawnNextObject()
    {
        if (currentObjectIndex >= 3)
        {
            // Debug.Log("[ObjectSpawner] All objects spawned for this trial");
            return;
        }
        
        StartCoroutine(SpawnWithDelay());
    }
    
    IEnumerator SpawnWithDelay()
    {
        Debug.Log($"[ObjectSpawner] SpawnWithDelay started for index={currentObjectIndex} ({objectNames[currentObjectIndex]})");
        yield return new WaitForSeconds(spawnDelay);

        GameObject prefab = GetCurrentPrefab();
        Debug.Log($"[ObjectSpawner] Prefab for {objectNames[currentObjectIndex]}: {(prefab != null ? prefab.name : "NULL")}");
        if (prefab == null)
        {
            Debug.LogError($"[ObjectSpawner] Prefab for {objectNames[currentObjectIndex]} is null!");
            yield break;
        }
        
        // Spawn at spawn point with custom rotation + height offset
        Vector3 spawnPos = spawnPoint.position + new Vector3(0, spawnHeightOffset, 0);
        Quaternion spawnRotation = Quaternion.Euler(GetCurrentRotation());
        currentObject = Instantiate(prefab, spawnPos, spawnRotation);
        currentObject.name = objectNames[currentObjectIndex];
        
        // Apply appropriate tag and layer
        currentObject.tag = objectNames[currentObjectIndex];
        currentObject.layer = 0; // Default layer (ensure interaction)
        
        // Fix Grabbable rigidbody reference after instantiation
        FixGrabbableRigidbody(currentObject);

        isWaitingForPickup = true;

        Debug.Log($"[ObjectSpawner] Spawned: {objectNames[currentObjectIndex]} at {spawnPos}");
    }
    
    GameObject GetCurrentPrefab()
    {
        switch (currentObjectIndex)
        {
            case 0: return basePrefab;
            case 1: return cylinderPrefab;
            case 2: return cubePrefab;
            default: return null;
        }
    }
    
    Vector3 GetCurrentRotation()
    {
        switch (currentObjectIndex)
        {
            case 0: return baseRotation;
            case 1: return cylinderRotation;
            case 2: return cubeRotation;
            default: return Vector3.zero;
        }
    }
    
    void Update()
    {
        if (currentObject == null || pickupZone == null)
        {
            // Debug: 왜 Update가 early return 하는지 확인
            if (currentObject == null && currentObjectIndex < 3)
                Debug.LogWarning($"[ObjectSpawner] currentObject is NULL! index={currentObjectIndex}");
            return;
        }

        float distance = Vector3.Distance(currentObject.transform.position, pickupZone.position);

        // Check if object reached pickup zone
        if (isWaitingForPickup && distance < stopDistance)
        {
            StopObjectAtPickupZone();
        }

        // Check if object was picked up (moved away from pickup zone)
        if (objectAtPickupZone && distance > pickupDetectionDistance)
        {
            OnPickedUpFromZone();
        }

        // Debug: 현재 상태 주기적 출력 (1초마다)
        if (Time.frameCount % 60 == 0 && objectAtPickupZone)
        {
            Debug.Log($"[ObjectSpawner] Waiting pickup: {objectNames[currentObjectIndex]}, distance={distance:F2}, needDistance>{pickupDetectionDistance}");
        }
    }

    
    private bool objectAtPickupZone = false;
    [Header("Pickup Detection")]
    public float pickupDetectionDistance = 0.3f;  // Distance to consider "picked up"

    [Header("Stabilization")]
    public float stabilizationDrag = 20f;  // High drag when at pickup zone
    private float originalDrag = 0f;
    private float originalAngularDrag = 0.05f;

    void StopObjectAtPickupZone()
    {
        if (currentObject == null || objectAtPickupZone) return;

        objectAtPickupZone = true;
        Debug.Log($"[ObjectSpawner] {objectNames[currentObjectIndex]} arrived at pickup zone - conveyor stopped");

        // Stop conveyor belt (physics + visual)
        if (conveyorBelt != null)
            conveyorBelt.isRunning = false;
        if (conveyorScroll != null)
            conveyorScroll.isRunning = false;

        // Apply high drag to stabilize, save original values
        Rigidbody rb = currentObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
            rb.linearDamping = stabilizationDrag;
            rb.angularDamping = stabilizationDrag;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    void OnPickedUpFromZone()
    {
        Debug.Log($"[ObjectSpawner] {objectNames[currentObjectIndex]} picked up! Next index will be {currentObjectIndex + 1}");

        // Restore original drag values
        if (currentObject != null)
        {
            Rigidbody rb = currentObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping = originalDrag;
                rb.angularDamping = originalAngularDrag;
            }
        }

        // Resume conveyor belt (physics + visual)
        if (conveyorBelt != null)
            conveyorBelt.isRunning = true;
        if (conveyorScroll != null)
            conveyorScroll.isRunning = true;

        objectAtPickupZone = false;
        isWaitingForPickup = false;
        currentObjectIndex++;

        // Spawn next object
        if (currentObjectIndex < 3)
        {
            SpawnNextObject();
        }
    }
    
    /// <summary>
    /// Called when object is picked up by participant
    /// </summary>
    public void OnObjectPickedUp()
    {
        if (!isWaitingForPickup) return;
        
        // Re-enable physics
        if (currentObject != null)
        {
            Rigidbody rb = currentObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
        
        isWaitingForPickup = false;
        currentObjectIndex++;
        
        // Debug.Log($"[ObjectSpawner] Object picked up. Next index: {currentObjectIndex}");
        
        // Spawn next object if not done
        if (currentObjectIndex < 3)
        {
            SpawnNextObject();
        }
    }
    
    /// <summary>
    /// Fix Grabbable rigidbody reference after instantiation
    /// </summary>
    void FixGrabbableRigidbody(GameObject obj)
    {
        var grabbable = obj.GetComponent<Grabbable>();
        if (grabbable != null)
        {
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                grabbable.InjectOptionalRigidbody(rb);
                // Debug.Log($"[ObjectSpawner] Fixed Grabbable rigidbody for {obj.name}");
            }
        }
    }
    
    /// <summary>
    /// Reset spawner for next trial
    /// </summary>
    public void Reset()
    {
        currentObjectIndex = 0;
        isWaitingForPickup = false;
        currentObject = null;
    }
}
