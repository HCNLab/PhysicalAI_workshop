using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

/// <summary>
/// Commander for ROS2 Pick & Place from Unity.
/// Allows setting pick/place positions and executing pick & place remotely.
/// </summary>
public class ROSPickPlaceCommander : MonoBehaviour
{
    private ROSConnection ros;

    [Header("ROS Topics")]
    public string pickPoseTopic = "/unity/pick_pose";
    public string placePoseTopic = "/unity/place_pose";
    public string executeTopic = "/unity/execute_pnp";
    public string statusTopic = "/unity/pnp_status";

    [Header("References")]
    public Transform pickTarget;    // Assign pick position object
    public Transform placeTarget;   // Assign place position object
    public Transform robotBase;     // Robot base for coordinate transform

    [Header("Status")]
    public string lastStatus = "";
    public string lastMessage = "";
    public bool isExecuting = false;

    [Header("UI (Optional)")]
    public UnityEngine.UI.Text statusText;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        // Register publishers
        ros.RegisterPublisher<PoseStampedMsg>(pickPoseTopic);
        ros.RegisterPublisher<PoseStampedMsg>(placePoseTopic);
        ros.RegisterPublisher<BoolMsg>(executeTopic);

        // Subscribe to status feedback
        ros.Subscribe<StringMsg>(statusTopic, OnStatusReceived);

        Debug.Log("[ROSPickPlaceCommander] Initialized - Ready to send pick/place commands");
    }

    void OnStatusReceived(StringMsg msg)
    {
        // Parse status: "STATUS|message"
        string[] parts = msg.data.Split('|');
        if (parts.Length >= 2)
        {
            lastStatus = parts[0];
            lastMessage = parts[1];
        }
        else
        {
            lastStatus = msg.data;
            lastMessage = "";
        }

        // Update executing state
        isExecuting = lastStatus.StartsWith("STEP_") || lastStatus == "EXECUTING";

        // Update UI if available
        if (statusText != null)
        {
            statusText.text = $"[{lastStatus}] {lastMessage}";
        }

        Debug.Log($"[ROSPickPlaceCommander] Status: {lastStatus} - {lastMessage}");
    }

    /// <summary>
    /// Send current pick position to ROS
    /// </summary>
    public void SendPickPose()
    {
        if (pickTarget == null)
        {
            Debug.LogError("[ROSPickPlaceCommander] Pick target not assigned!");
            return;
        }

        PoseStampedMsg msg = CreatePoseStamped(pickTarget);
        ros.Publish(pickPoseTopic, msg);
        Debug.Log($"[ROSPickPlaceCommander] Sent pick pose: {pickTarget.position}");
    }

    /// <summary>
    /// Send current place position to ROS
    /// </summary>
    public void SendPlacePose()
    {
        if (placeTarget == null)
        {
            Debug.LogError("[ROSPickPlaceCommander] Place target not assigned!");
            return;
        }

        PoseStampedMsg msg = CreatePoseStamped(placeTarget);
        ros.Publish(placePoseTopic, msg);
        Debug.Log($"[ROSPickPlaceCommander] Sent place pose: {placeTarget.position}");
    }

    /// <summary>
    /// Send both pick and place poses
    /// </summary>
    public void SendBothPoses()
    {
        SendPickPose();
        SendPlacePose();
    }

    /// <summary>
    /// Execute pick and place command
    /// </summary>
    public void ExecutePickAndPlace()
    {
        if (isExecuting)
        {
            Debug.LogWarning("[ROSPickPlaceCommander] Already executing!");
            return;
        }

        // First send both poses to ensure they're current
        SendBothPoses();

        // Then send execute command
        ros.Publish(executeTopic, new BoolMsg(true));
        Debug.Log("[ROSPickPlaceCommander] Execute command sent!");
    }

    /// <summary>
    /// One-click: Send poses and execute
    /// </summary>
    public void SendAndExecute()
    {
        SendBothPoses();
        // Small delay before execute
        Invoke(nameof(ExecutePickAndPlace), 0.1f);
    }

    PoseStampedMsg CreatePoseStamped(Transform target)
    {
        Vector3 pos;
        Quaternion rot;

        // Transform to robot base coordinates if assigned
        if (robotBase != null)
        {
            pos = robotBase.InverseTransformPoint(target.position);
            rot = Quaternion.Inverse(robotBase.rotation) * target.rotation;
        }
        else
        {
            pos = target.position;
            rot = target.rotation;
        }

        PoseStampedMsg msg = new PoseStampedMsg();
        msg.header.frame_id = "base_link";
        msg.pose.position = pos.To<FLU>();
        msg.pose.orientation = rot.To<FLU>();

        return msg;
    }

    // Inspector buttons for testing
    [ContextMenu("Send Pick Pose")]
    void SendPickPoseFromMenu() => SendPickPose();

    [ContextMenu("Send Place Pose")]
    void SendPlacePoseFromMenu() => SendPlacePose();

    [ContextMenu("Execute Pick & Place")]
    void ExecuteFromMenu() => ExecutePickAndPlace();

    [ContextMenu("Send All & Execute")]
    void SendAndExecuteFromMenu() => SendAndExecute();
}
