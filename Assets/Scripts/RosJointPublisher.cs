using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System.Collections.Generic;

public class RosJointPublisher : MonoBehaviour
{
    // ROS 연결
    ROSConnection ros;
    public string topicName = "/unity_joint_states";

    // 관절 이름 (ROS와 동일해야 함)
    public string[] jointNames = new string[] {
        "joint_1", "joint_2", "joint_3", 
        "joint_4", "joint_5", "joint_6", 
        "gripper_jaw1_joint", "gripper_jaw2_joint"
    };

    [Header("Robot Reference")]
    [Tooltip("로봇의 Root Transform (mk3와 같은 로봇 최상위 오브젝트)")]
    public Transform robotRoot;
    
    // ArticulationBody 참조 (자동 검색)
    private ArticulationBody[] articulationBodies;
    private Dictionary<string, ArticulationBody> jointMap = new Dictionary<string, ArticulationBody>();

    // 메시지 전송 주기
    public float publishRate = 10f; // 10Hz
    float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<JointStateMsg>(topicName);
        
        // Robot에서 모든 ArticulationBody 찾기
        if (robotRoot != null)
        {
            articulationBodies = robotRoot.GetComponentsInChildren<ArticulationBody>();
            
            Debug.Log($"[RosJointPublisher] Found {articulationBodies.Length} ArticulationBodies in children.");

            foreach (var ab in articulationBodies)
            {
                Debug.Log($"[RosJointPublisher] AB Found: '{ab.name}'");

                // 조인트 이름과 매핑
                foreach (string jointName in jointNames)
                {
                    bool isMatch = false;
                    string abNameLower = ab.name.ToLower();

                    // 1. 단순 포함 (joint_1 이 이름에 있는 경우)
                    if (abNameLower.Contains(jointName.ToLower())) isMatch = true;

                    // 2. 언더스코어 제외 (joint1)
                    else if (abNameLower.Contains(jointName.Replace("_", "").ToLower())) isMatch = true;
                    
                    // 3. Link 이름 매핑 (joint_1 -> link_1, link1)
                    else if (jointName.Contains("joint"))
                    {
                        string linkName = jointName.Replace("joint", "link"); // joint_1 -> link_1
                        if (abNameLower.Contains(linkName.ToLower())) isMatch = true;
                        
                        string linkNameNoUnder = linkName.Replace("_", ""); // link1
                        if (abNameLower.Contains(linkNameNoUnder.ToLower())) isMatch = true;
                    }
                    
                    // 4. Gripper 매핑 (gripper_jaw1_joint -> gripper_jaw1_link)
                    else if (jointName.Contains("gripper"))
                    {
                         string linkName = jointName.Replace("joint", "link");
                         if (abNameLower.Contains(linkName.ToLower())) isMatch = true;
                    }

                    if (isMatch)
                    {
                        if (!jointMap.ContainsKey(jointName))
                        {
                            jointMap[jointName] = ab;
                            Debug.Log($"[RosJointPublisher] Mapped: {jointName} -> {ab.name}");
                        }
                    }
                }
            }
            
            Debug.Log($"[RosJointPublisher] Found {jointMap.Count} joints to publish");
        }
        else
        {
            Debug.LogWarning("[RosJointPublisher] robotRoot not set! Please assign in Inspector.");
        }
    }

    // Manual Control Sliders
    [Header("Manual Control")]
    public bool enableManualControl = true;
    [Range(-3.14f, 3.14f)] public float joint1_cmd;
    [Range(-3.14f, 3.14f)] public float joint2_cmd;
    [Range(-3.14f, 3.14f)] public float joint3_cmd;
    [Range(-3.14f, 3.14f)] public float joint4_cmd;
    [Range(-3.14f, 3.14f)] public float joint5_cmd;
    [Range(-3.14f, 3.14f)] public float joint6_cmd;
    [Range(0, 0.014f)] public float gripper_cmd;

    void Update()
    {
        if (robotRoot == null) return;
        
        // 1. Manual Control: Apply slider values to ArticulationBodies
        if (enableManualControl)
        {
            ApplyManualControl();
        }

        // 2. Publish Current State to ROS
        timeElapsed += Time.deltaTime;
        if (timeElapsed > 1.0f / publishRate)
        {
            PublishJointState();
            timeElapsed = 0;
        }
    }

    void ApplyManualControl()
    {
        SetJointTarget("joint_1", joint1_cmd * Mathf.Rad2Deg);
        SetJointTarget("joint_2", joint2_cmd * Mathf.Rad2Deg);
        SetJointTarget("joint_3", joint3_cmd * Mathf.Rad2Deg);
        SetJointTarget("joint_4", joint4_cmd * Mathf.Rad2Deg);
        SetJointTarget("joint_5", joint5_cmd * Mathf.Rad2Deg);
        SetJointTarget("joint_6", joint6_cmd * Mathf.Rad2Deg);
        
        SetJointTarget("gripper_jaw1_joint", gripper_cmd); // Gripper usually linear (meters)
        SetJointTarget("gripper_jaw2_joint", gripper_cmd);
    }

    void SetJointTarget(string name, float target)
    {
        if (jointMap.ContainsKey(name))
        {
            ArticulationBody ab = jointMap[name];
            var drive = ab.xDrive;
            drive.target = target;
            ab.xDrive = drive;
        }
    }

    void PublishJointState()
    {
        JointStateMsg msg = new JointStateMsg();
        msg.header.frame_id = "base_link";
        msg.header.stamp.sec = (int)Time.time;
        msg.name = jointNames;

        double[] positions = new double[jointNames.Length];
        
        for (int i = 0; i < jointNames.Length; i++)
        {
            string jointName = jointNames[i];
            
            if (jointMap.ContainsKey(jointName))
            {
                ArticulationBody ab = jointMap[jointName];
                // Publish the CURRENT state (not just target)
                // If using xDrive.target, use that. If using physical position, use jointPosition[0]
                // Here we stick to xDrive.target as requested earlier for consistency
                positions[i] = ab.xDrive.target * Mathf.Deg2Rad;
            }
            else
            {
                positions[i] = 0;
            }
        }

        msg.position = positions;
        ros.Publish(topicName, msg);
    }
}