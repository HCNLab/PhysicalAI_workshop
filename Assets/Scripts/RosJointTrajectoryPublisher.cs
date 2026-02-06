using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Trajectory;
using RosMessageTypes.BuiltinInterfaces;
using System.Collections.Generic;

/// <summary>
/// Unity에서 실물 AR4 로봇을 직접 제어하기 위한 JointTrajectory Publisher
/// /joint_trajectory_controller/joint_trajectory 토픽으로 trajectory_msgs/JointTrajectory 메시지를 발행합니다.
/// </summary>
public class RosJointTrajectoryPublisher : MonoBehaviour
{
    // ROS 연결
    ROSConnection ros;
    public string topicName = "/joint_trajectory_controller/joint_trajectory";

    // 관절 이름 (ROS joint_trajectory_controller와 동일해야 함)
    public string[] jointNames = new string[] {
        "joint_1", "joint_2", "joint_3", 
        "joint_4", "joint_5", "joint_6",
    };

    [Header("Robot Reference")]
    [Tooltip("로봇의 Root Transform (ArticulationBody가 있는 로봇 최상위 오브젝트)")]
    public Transform robotRoot;
    
    // ArticulationBody 참조 (자동 검색)
    private Dictionary<string, ArticulationBody> jointMap = new Dictionary<string, ArticulationBody>();

    [Header("Manual Control")]
    public bool enableManualControl = true;
    [Range(-3.14f, 3.14f)] public float joint1_cmd;
    [Range(-3.14f, 3.14f)] public float joint2_cmd;
    [Range(-3.14f, 3.14f)] public float joint3_cmd;
    [Range(-3.14f, 3.14f)] public float joint4_cmd;
    [Range(-3.14f, 3.14f)] public float joint5_cmd;
    [Range(-3.14f, 3.14f)] public float joint6_cmd;
    [Range(0, 0.014f)] public float gripper_cmd;

    [Header("Visual Smoothing")]
    public float smoothSpeed = 10f;  // 클수록 빠름

    [Header("Trajectory Settings")]
    [Tooltip("목표 도달까지 걸리는 시간 (초)")]
    public float trajectoryDuration = 0.5f;
    
    [Tooltip("메시지 전송 주기 (Hz)")]
    public float publishRate = 10f;
    
    private float timeElapsed;
    private float[] lastSentPositions = new float[6];
    private bool initialized = false;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<JointTrajectoryMsg>(topicName);
        
        // Robot에서 모든 ArticulationBody 찾기
        if (robotRoot != null)
        {
            var articulationBodies = robotRoot.GetComponentsInChildren<ArticulationBody>();
            
            Debug.Log($"[RosJointTrajectoryPublisher] Found {articulationBodies.Length} ArticulationBodies");

            foreach (var ab in articulationBodies)
            {
                foreach (string jointName in jointNames)
                {
                    string abNameLower = ab.name.ToLower();
                    bool isMatch = false;

                    // 매칭 로직 (기존 RosJointPublisher와 동일)
                    if (abNameLower.Contains(jointName.ToLower())) isMatch = true;
                    else if (abNameLower.Contains(jointName.Replace("_", "").ToLower())) isMatch = true;
                    else if (jointName.Contains("joint"))
                    {
                        string linkName = jointName.Replace("joint", "link");
                        if (abNameLower.Contains(linkName.ToLower())) isMatch = true;
                        if (abNameLower.Contains(linkName.Replace("_", "").ToLower())) isMatch = true;
                    }

                    if (isMatch && !jointMap.ContainsKey(jointName))
                    {
                        jointMap[jointName] = ab;
                        Debug.Log($"[RosJointTrajectoryPublisher] Mapped: {jointName} -> {ab.name}");
                    }
                }
            }
            
            Debug.Log($"[RosJointTrajectoryPublisher] Mapped {jointMap.Count} joints for trajectory control");
            initialized = true;
        }
        else
        {
            Debug.LogWarning("[RosJointTrajectoryPublisher] robotRoot not set! Please assign in Inspector.");
        }

        // 초기 위치 저장
        for (int i = 0; i < 6; i++)
        {
            lastSentPositions[i] = float.NaN;
        }

    }

    void FixedUpdate()
    {
        if (!initialized || !enableManualControl) return;

        ArticulationBody root = robotRoot.GetComponentInChildren<ArticulationBody>();

        // 물리 solver 비활성화
        root.immovable = true;

        List<float> positions = new List<float>();
        root.GetJointPositions(positions);

        if (positions.Count >= 8)
        {
            positions[0] = joint1_cmd;
            positions[1] = joint2_cmd;
            positions[2] = joint3_cmd;
            positions[3] = joint4_cmd;
            positions[4] = joint5_cmd;
            positions[5] = joint6_cmd;
            positions[6] = gripper_cmd;
            positions[7] = gripper_cmd;
        }

        root.SetJointPositions(positions);
        root.SetJointVelocities(new List<float>(new float[positions.Count]));
        root.SetJointForces(new List<float>(new float[positions.Count]));
    }

    void Update()
    {
        if (!initialized || !enableManualControl) return;

        // ROS publish만 Update에서
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1.0f / publishRate)
        {
            timeElapsed = 0;
            if (HasPositionChanged()) PublishJointTrajectory();
        }
    }

    /*
    void Update()
    {
        if (!initialized || !enableManualControl) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            foreach (var kvp in jointMap)
            {
                Debug.Log($"{kvp.Key} -> {kvp.Value.name} | pos: {kvp.Value.jointPosition[0]:F4} | axis: {kvp.Value.anchorRotation.eulerAngles}");
            }
        }
        // Unity 로봇 모델에 Manual Control 적용 (시각적 피드백용)
        ApplyManualControlToModel();

        // 주기적으로 ROS에 Trajectory 발행
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1.0f / publishRate)
        {
            timeElapsed = 0;
            
            // 위치 변화가 있을 때만 발행
            if (HasPositionChanged())
            {
                PublishJointTrajectory();
            }
        }
    }
    */

    void ApplyManualControlToModel()
    {
        // 배열이 아니라 첫 번째 ArticulationBody (root) 찾기
        ArticulationBody root = robotRoot.GetComponentInChildren<ArticulationBody>();

        List<float> positions = new List<float>();
        root.GetJointPositions(positions);

        if (positions.Count >= 6)
        {
            positions[0] = joint1_cmd;
            positions[1] = joint2_cmd;
            positions[2] = joint3_cmd;
            positions[3] = joint4_cmd;
            positions[4] = joint5_cmd;
            positions[5] = joint6_cmd;
        }

        root.SetJointPositions(positions);

        List<float> velocities = new List<float>(new float[positions.Count]);
        root.SetJointVelocities(velocities);
    }

    /*
    void ApplyManualControlToModel()
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
    */

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

    bool HasPositionChanged()
    {
        float[] currentPositions = { joint1_cmd, joint2_cmd, joint3_cmd, joint4_cmd, joint5_cmd, joint6_cmd};
        float threshold = 0.001f; // ~0.06 degrees

        for (int i = 0; i < 6; i++)
        {
            if (float.IsNaN(lastSentPositions[i]) || Mathf.Abs(currentPositions[i] - lastSentPositions[i]) > threshold)
            {
                return true;
            }
        }
        return false;
    }

    void PublishJointTrajectory()
    {
        // JointTrajectory 메시지 생성
        JointTrajectoryMsg msg = new JointTrajectoryMsg();
        msg.header.frame_id = "";
        msg.header.stamp.sec = 0;
        msg.header.stamp.nanosec = 0;
        msg.joint_names = jointNames;

        // Trajectory Point 생성 (목표 위치 1개)
        JointTrajectoryPointMsg point = new JointTrajectoryPointMsg();
        point.positions = new double[] {
            joint1_cmd, joint2_cmd, joint3_cmd,
            joint4_cmd, joint5_cmd, joint6_cmd
        };
        
        // 속도는 0으로 설정 (목표 위치에서 정지)
        point.velocities = new double[] { 0, 0, 0, 0, 0, 0};
        point.accelerations = new double[] { 0, 0, 0, 0, 0, 0 };
        
        // time_from_start 설정
        int sec = (int)trajectoryDuration;
        uint nanosec = (uint)((trajectoryDuration - sec) * 1e9);
        point.time_from_start = new DurationMsg(sec, nanosec);

        msg.points = new JointTrajectoryPointMsg[] { point };

        ros.Publish(topicName, msg);

        // 마지막 전송 위치 저장
        lastSentPositions[0] = joint1_cmd;
        lastSentPositions[1] = joint2_cmd;
        lastSentPositions[2] = joint3_cmd;
        lastSentPositions[3] = joint4_cmd;
        lastSentPositions[4] = joint5_cmd;
        lastSentPositions[5] = joint6_cmd;

        Debug.Log($"[RosJointTrajectoryPublisher] Sent trajectory: [{joint1_cmd:F3}, {joint2_cmd:F3}, {joint3_cmd:F3}, {joint4_cmd:F3}, {joint5_cmd:F3}, {joint6_cmd:F3}]");
    }

    /// <summary>
    /// 외부에서 joint 값을 설정하는 메서드 (스크립트에서 제어할 때 사용)
    /// </summary>
    public void SetJointCommands(float[] jointPositions)
    {
        if (jointPositions.Length >= 6)
        {
            joint1_cmd = jointPositions[0];
            joint2_cmd = jointPositions[1];
            joint3_cmd = jointPositions[2];
            joint4_cmd = jointPositions[3];
            joint5_cmd = jointPositions[4];
            joint6_cmd = jointPositions[5];
        }
    }

    /// <summary>
    /// 즉시 trajectory를 발행하는 메서드 (rate 제한 없이)
    /// </summary>
    public void SendTrajectoryNow()
    {
        PublishJointTrajectory();
    }
}
