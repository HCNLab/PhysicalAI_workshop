using UnityEngine;
using System;

/// <summary>
/// Detects when assembly objects are correctly placed using TAGS
/// Attach to: Frame object (assembly target area)
/// 
/// Unity Tags needed: "Base", "Cylinder", "Cube"
/// </summary>
public class AssemblyDetector : MonoBehaviour
{
    [Header("Slot Positions")]
    [Tooltip("Where base should be placed")]
    public Transform baseSlot;
    [Tooltip("Where cylinder should be placed (on base)")]
    public Transform cylinderSlot;
    [Tooltip("Where cube should be placed (on cylinder)")]
    public Transform cubeSlot;

    [Header("Tag Names")]
    public string baseTag = "Base";
    public string cylinderTag = "Cylinder";
    public string cubeTag = "Cube";

    [Header("Detection Settings")]
    public float positionTolerance = 0.05f;
    public float checkInterval = 0.3f;

    [Header("Experiment Reference")]
    public HRIExperimentManager experimentManager;
    
    [Header("Conveyor Belt")]
    public ObjectSpawner objectSpawner;

    private GameObject currentBase;
    private GameObject currentCylinder;
    private GameObject currentCube;
    
    private GameObject savedBase;
    private GameObject savedCylinder;
    private GameObject savedCube;
    
    private Vector3 savedCylinderLocalPos;
    private Vector3 savedCubeLocalPos;
    private Quaternion savedCylinderLocalRot;
    private Quaternion savedCubeLocalRot;

    public bool isBasePlaced { get; private set; }
    public bool isCylinderPlaced { get; private set; }
    public bool isCubePlaced { get; private set; }
    public bool isAssemblyComplete => isBasePlaced && isCylinderPlaced && isCubePlaced;

    public event Action<string> OnObjectPlaced;
    public event Action OnAssemblyComplete;

    private bool assemblyNotified = false;
    private float lastCheckTime;

    void Update()
    {
        if (Time.time - lastCheckTime < checkInterval) return;
        lastCheckTime = Time.time;
        CheckAssemblyState();
    }

    void CheckAssemblyState()
    {
        bool prevBase = isBasePlaced;
        bool prevCylinder = isCylinderPlaced;
        bool prevCube = isCubePlaced;

        currentBase = FindObjectNearSlot(baseSlot, baseTag);
        isBasePlaced = currentBase != null;

        if (isBasePlaced)
        {
            currentCylinder = FindObjectNearSlot(cylinderSlot, cylinderTag);
            isCylinderPlaced = currentCylinder != null;
        }
        else
        {
            currentCylinder = null;
            isCylinderPlaced = false;
        }

        if (isCylinderPlaced)
        {
            currentCube = FindObjectNearSlot(cubeSlot, cubeTag);
            isCubePlaced = currentCube != null;
        }
        else
        {
            currentCube = null;
            isCubePlaced = false;
        }

        if (isBasePlaced && !prevBase)
        {
            OnObjectPlaced?.Invoke("Base");
            currentBase.GetComponent<PressableObject>()?.OnAssembled();
            
            if (experimentManager != null && 
                experimentManager.currentState == HRIExperimentManager.ExperimentState.Cleanup)
            {
                experimentManager.OnCleanupComplete();
            }
        }
        
        if (!isBasePlaced && prevBase)
        {
            if (currentCylinder != null && currentCylinder.transform.parent != null)
                currentCylinder.transform.SetParent(null);
            if (currentCube != null && currentCube.transform.parent != null)
                currentCube.transform.SetParent(null);
            
            if (assemblyNotified)
                assemblyNotified = false;
        }
        
        if (isCylinderPlaced && !prevCylinder)
        {
            OnObjectPlaced?.Invoke("Cylinder");
            
            // Link SpongeStop at runtime
            var cylinderPressable = currentCylinder.GetComponent<PressableObject>();
            if (cylinderPressable != null)
            {
                cylinderPressable.OnAssembled();
                
                // Find CylinderStop on Base
                if (currentBase != null)
                {
                    var cylinderStop = currentBase.transform.Find("CylinderStop");
                    if (cylinderStop != null)
                    {
                        cylinderPressable.spongeStop = cylinderStop.GetComponent<SpongeStop>();
                        if (cylinderPressable.spongeStop != null)
                            Debug.Log("[AssemblyDetector] Successfully linked Cylinder to CylinderStop!");
                        else
                            Debug.LogError("[AssemblyDetector] CylinderStop found but no SpongeStop script attached!");
                    }
                    else
                    {
                        Debug.LogError("[AssemblyDetector] Could not find 'CylinderStop' child on Base!");
                    }
                }
            }
        }
        
        if (isCubePlaced && !prevCube)
        {
            OnObjectPlaced?.Invoke("Cube");
            
            // Link SpongeStop at runtime
            var cubePressable = currentCube.GetComponent<PressableObject>();
            if (cubePressable != null)
            {
                cubePressable.OnAssembled();
                
                // Find CubeStop on Base
                if (currentBase != null)
                {
                    var cubeStop = currentBase.transform.Find("CubeStop");
                    if (cubeStop != null)
                    {
                        cubePressable.spongeStop = cubeStop.GetComponent<SpongeStop>();
                        if (cubePressable.spongeStop != null)
                            Debug.Log("[AssemblyDetector] Successfully linked Cube to CubeStop!");
                        else
                            Debug.LogError("[AssemblyDetector] CubeStop found but no SpongeStop script attached!");
                    }
                    else
                    {
                        Debug.LogError("[AssemblyDetector] Could not find 'CubeStop' child on Base!");
                    }
                }
            }
        }

        if (isAssemblyComplete && !assemblyNotified)
        {
            assemblyNotified = true;
            
            savedBase = currentBase;
            savedCylinder = currentCylinder;
            savedCube = currentCube;
            
            if (savedBase != null && savedCylinder != null)
            {
                savedCylinderLocalPos = savedBase.transform.InverseTransformPoint(savedCylinder.transform.position);
                savedCylinderLocalRot = Quaternion.Inverse(savedBase.transform.rotation) * savedCylinder.transform.rotation;
            }
            if (savedBase != null && savedCube != null)
            {
                savedCubeLocalPos = savedBase.transform.InverseTransformPoint(savedCube.transform.position);
                savedCubeLocalRot = Quaternion.Inverse(savedBase.transform.rotation) * savedCube.transform.rotation;
            }
            
            OnAssemblyComplete?.Invoke();
            
            if (experimentManager != null)
                experimentManager.OnAssemblyComplete();
        }
    }

    GameObject FindObjectNearSlot(Transform slot, string tag)
    {
        if (slot == null) return null;

        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        
        foreach (GameObject obj in objects)
        {
            float distance = Vector3.Distance(obj.transform.position, slot.position);
            if (distance <= positionTolerance)
                return obj;
        }
        
        return null;
    }

    public void ResetAssembly()
    {
        isBasePlaced = false;
        isCylinderPlaced = false;
        isCubePlaced = false;
        assemblyNotified = false;
        currentBase = null;
        currentCylinder = null;
        currentCube = null;
    }

    [ContextMenu("Reset Assembly State")]
    void ResetFromMenu() => ResetAssembly();

    [ContextMenu("Force Check")]
    void ForceCheck() => CheckAssemblyState();
    
    public void FinalizeAssembly()
    {
        if (savedBase == null) return;
        
        Rigidbody baseRb = savedBase.GetComponent<Rigidbody>();
        if (baseRb == null)
        {
            Debug.LogError("[AssemblyDetector] Base has no Rigidbody! Cannot attach joints.");
            return;
        }
        
        // Helper function to attach joint
        void AttachToJoint(GameObject child)
        {
            if (child == null) return;
            
            // Ensure child has Rigidbody
            Rigidbody childRb = child.GetComponent<Rigidbody>();
            if (childRb == null)
                childRb = child.AddComponent<Rigidbody>();
                
            // Make sure it's NOT kinematic so physics joints work
            childRb.isKinematic = false;
            
            // Create FixedJoint
            FixedJoint joint = child.GetComponent<FixedJoint>();
            if (joint == null)
                joint = child.AddComponent<FixedJoint>();
                
            joint.connectedBody = baseRb;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            joint.enableCollision = false; // Don't collide with base
            
            // Do NOT parent the transform to avoid scale skewing
            // child.transform.SetParent(savedBase.transform); 
            
            Debug.Log($"[AssemblyDetector] Attached {child.name} to Base via FixedJoint");
        }
        
        if (savedCylinder != null) AttachToJoint(savedCylinder);
        if (savedCube != null) AttachToJoint(savedCube);
    }
    
    [ContextMenu("Finalize Assembly")]
    void FinalizeFromMenu() => FinalizeAssembly();
}
