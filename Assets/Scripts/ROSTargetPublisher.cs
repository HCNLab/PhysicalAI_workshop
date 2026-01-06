using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;

public class RosTargetPublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/vr_target_pose";
    public string frameId = "base_link";
    public Transform robotBase; // ⭐ 로봇의 기준점 (base_link) 추가
    public float publishRate = 10f;

    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(topicName);
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > 1.0f / publishRate)
        {
            PublishPose();
            timeElapsed = 0;
        }
    }

    void PublishPose()
    {
        // ⭐ 로봇 베이스 기준의 상대 좌표로 변환
        Vector3 relativePos = robotBase.InverseTransformPoint(transform.position);
        Quaternion relativeRot = Quaternion.Inverse(robotBase.rotation) * transform.rotation;

        PoseMsg poseMsg = new PoseMsg
        {
            // 상대 좌표를 ROS 좌표계로 변환
            position = relativePos.To<FLU>(),
            orientation = relativeRot.To<FLU>()
        };

        PoseStampedMsg msg = new PoseStampedMsg();
        msg.header.frame_id = frameId;
        msg.header.stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg();
        msg.pose = poseMsg;

        ros.Publish(topicName, msg);
    }
}