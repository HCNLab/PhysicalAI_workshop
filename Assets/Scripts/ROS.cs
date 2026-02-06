using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor; // JointStateMsg
using System.Collections.Generic;

public class ros : MonoBehaviour
{
    public string topic = "/joint_states";
    
    [Header("Robot Joints (ArticulationBody)")]
    public ArticulationBody[] armJoints = new ArticulationBody[6];
    public ArticulationBody gripperJaw1;
    public ArticulationBody gripperJaw2;
    
    [Header("Auto Find")]
    public bool autoFindJoints = true;
    public Transform robotRoot;

    void Start()
    {
        var conn = ROSConnection.GetOrCreateInstance();
        conn.Subscribe<JointStateMsg>(topic, OnJS);
        Debug.Log($"[Unity] Subscribed: {topic}");

        if (autoFindJoints && robotRoot != null)
        {
            FindJointsAutomatically();
        }
    }

    void FindJointsAutomatically()
    {
        var allBodies = robotRoot.GetComponentsInChildren<ArticulationBody>();
        
        foreach (var body in allBodies)
        {
            string name = body.gameObject.name.ToLower();
            
            if (name.Contains("joint_1") || name.Contains("joint1") || name.Contains("link_1")) armJoints[0] = body;
            else if (name.Contains("joint_2") || name.Contains("joint2") || name.Contains("link_2")) armJoints[1] = body;
            else if (name.Contains("joint_3") || name.Contains("joint3") || name.Contains("link_3")) armJoints[2] = body;
            else if (name.Contains("joint_4") || name.Contains("joint4") || name.Contains("link_4")) armJoints[3] = body;
            else if (name.Contains("joint_5") || name.Contains("joint5") || name.Contains("link_5")) armJoints[4] = body;
            else if (name.Contains("joint_6") || name.Contains("joint6") || name.Contains("link_6")) armJoints[5] = body;
            else if (name.Contains("jaw1")) gripperJaw1 = body;
            else if (name.Contains("jaw2")) gripperJaw2 = body;
        }
        
        Debug.Log("[ROS] Auto-found joints from robot hierarchy");
    }

    void OnJS(JointStateMsg msg)
    {
        if (msg.name.Length != msg.position.Length) return;

        // 1. Find joint indices from message
        int[] armIndices = new int[6] { -1, -1, -1, -1, -1, -1 };
        int jaw1Index = -1;
        int jaw2Index = -1;

        

        /*
        if (msg.name != null)
        {
            Debug.Log(msg.position);
            
            foreach (var name_cur in msg.name)
            {
                Debug.Log(name_cur);
            }
            
        }
        */
        

        for (int i = 0; i < msg.name.Length; i++)
        {
            string jointName = msg.name[i];

            if (jointName.Contains("joint_1")) armIndices[0] = i;
            else if (jointName.Contains("joint_2")) armIndices[1] = i;
            else if (jointName.Contains("joint_3")) armIndices[2] = i;
            else if (jointName.Contains("joint_4")) armIndices[3] = i;
            else if (jointName.Contains("joint_5")) armIndices[4] = i;
            else if (jointName.Contains("joint_6")) armIndices[5] = i;
            else if (jointName.Contains("jaw1")) jaw1Index = i;
            else if (jointName.Contains("jaw2")) jaw2Index = i;
        }

        // 2. Move arm joints
        for (int i = 0; i < 6; i++)
        {
            if (armIndices[i] != -1 && armJoints[i] != null)
            {
                float targetRad = (float)msg.position[armIndices[i]];
                SetJointTarget(armJoints[i], targetRad * Mathf.Rad2Deg);
            }
        }

        // 3. Move gripper
        if (jaw1Index != -1 && gripperJaw1 != null)
        {
            float val = (float)msg.position[jaw1Index];
            // Debug.Log("gripper 1: " + val);
            SetJointTarget(gripperJaw1, val); // Already in correct units
        }
        if (jaw2Index != -1 && gripperJaw2 != null)
        {
            float val = (float)msg.position[jaw2Index];
            // Debug.Log("gripper 2: " + val);
            SetJointTarget(gripperJaw2, val);
        }
    }

    void SetJointTarget(ArticulationBody joint, float targetDegrees)
    {
        if (joint == null) return;
        
        var drive = joint.xDrive;
        drive.target = targetDegrees;
        joint.xDrive = drive;
    }
}
