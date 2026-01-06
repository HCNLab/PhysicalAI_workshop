using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

/// <summary>
/// VR Hand Tracking for Robot Control
/// Tracks hand position and pinch gesture for arm + gripper control
/// Compatible with Meta Quest 3
/// </summary>
public class VRHandTrackingController : MonoBehaviour
{
    private ROSConnection ros;

    [Header("Hand Tracking")]
    public OVRHand ovrHand;  // Assign the OVRHand component (LeftHand or RightHand)
    public OVRSkeleton ovrSkeleton;  // Assign the OVRSkeleton component
    
    [Header("Robot Base Reference")]
    public Transform robotBase;  // Robot base_link for coordinate transform

    [Header("ROS Topics")]
    public string targetPoseTopic = "/vr_target_pose";
    public string gripperCommandTopic = "/vr/gripper_command";

    [Header("Control Settings")]
    [Range(0.0f, 1.0f)]
    public float pinchThreshold = 0.7f;  // Pinch strength threshold (0-1)
    public float publishRate = 30f;  // Hz
    public bool useRightHand = true;

    [Header("Debug")]
    public bool logPinchState = false;
    public float currentPinchStrength = 0f;
    public bool isPinching = false;
    public Vector3 handPosition;

    private float lastPublishTime;
    private bool lastPinchState = false;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        // Register publishers
        ros.RegisterPublisher<PoseStampedMsg>(targetPoseTopic);
        ros.RegisterPublisher<Float32Msg>(gripperCommandTopic);

        Debug.Log("[VRHandTrackingController] Initialized - Hand tracking gripper control ready");
    }

    void Update()
    {
        if (ovrHand == null || ovrSkeleton == null)
        {
            Debug.LogWarning("[VRHandTrackingController] OVRHand or OVRSkeleton not assigned!");
            return;
        }

        // Check if hand is being tracked
        if (!ovrHand.IsTracked)
        {
            return;
        }

        // Get pinch strength (0 = no pinch, 1 = full pinch)
        currentPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        isPinching = currentPinchStrength > pinchThreshold;

        // Get hand position (wrist or palm)
        if (ovrSkeleton.Bones != null && ovrSkeleton.Bones.Count > 0)
        {
            // Use wrist bone position
            var wristBone = ovrSkeleton.Bones[(int)OVRSkeleton.BoneId.Hand_WristRoot];
            if (wristBone != null)
            {
                handPosition = wristBone.Transform.position;
            }
        }

        // Publish at specified rate
        if (Time.time - lastPublishTime >= 1f / publishRate)
        {
            lastPublishTime = Time.time;
            
            // Publish hand position for arm control
            PublishHandPose();
            
            // Publish gripper command on pinch state change
            if (isPinching != lastPinchState)
            {
                PublishGripperCommand();
                lastPinchState = isPinching;
                
                if (logPinchState)
                {
                    Debug.Log($"[VRHandTrackingController] Pinch: {isPinching}, Strength: {currentPinchStrength:F2}");
                }
            }
        }
    }

    void PublishHandPose()
    {
        Vector3 pos;
        Quaternion rot;

        // Transform to robot base coordinates if assigned
        if (robotBase != null)
        {
            pos = robotBase.InverseTransformPoint(handPosition);
            rot = Quaternion.Inverse(robotBase.rotation) * transform.rotation;
        }
        else
        {
            pos = handPosition;
            rot = transform.rotation;
        }

        PoseStampedMsg msg = new PoseStampedMsg();
        msg.header.frame_id = "base_link";
        msg.pose.position = pos.To<FLU>();
        msg.pose.orientation = rot.To<FLU>();

        ros.Publish(targetPoseTopic, msg);
    }

    void PublishGripperCommand()
    {
        // isPinching = true → close gripper (0.0)
        // isPinching = false → open gripper (1.0)
        float gripperValue = isPinching ? 0.0f : 1.0f;
        
        ros.Publish(gripperCommandTopic, new Float32Msg(gripperValue));
        
        Debug.Log($"[VRHandTrackingController] Gripper command: {(isPinching ? "CLOSE" : "OPEN")}");
    }

    // Inspector button for testing
    [ContextMenu("Test Gripper Open")]
    void TestGripperOpen()
    {
        ros.Publish(gripperCommandTopic, new Float32Msg(1.0f));
        Debug.Log("[VRHandTrackingController] Test: Gripper OPEN");
    }

    [ContextMenu("Test Gripper Close")]
    void TestGripperClose()
    {
        ros.Publish(gripperCommandTopic, new Float32Msg(0.0f));
        Debug.Log("[VRHandTrackingController] Test: Gripper CLOSE");
    }
}
