using System;
using System.Collections.Generic;
using Unity.Robotics;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Control
{
    public class Controller : MonoBehaviour
    {
        // 실제로 움직이는 관절만 저장할 리스트
        private List<ArticulationBody> articulationChain = new List<ArticulationBody>();

        public float stiffness = 10000f;
        public float damping = 100f;
        public float forceLimit = 1000f;

        void Start()
        {
            // 모든 관절 가져오기
            var allBodies = this.GetComponentsInChildren<ArticulationBody>();
            
            foreach (ArticulationBody joint in allBodies)
            {
                // ⚠️ 핵심 수정: 고정 관절(Fixed)은 제어 목록에서 제외!
                if (joint.jointType != ArticulationJointType.FixedJoint)
                {
                    articulationChain.Add(joint);

                    // 물리 설정 추가
                    joint.gameObject.AddComponent<JointControl>();
                    joint.jointFriction = 10;
                    joint.angularDamping = 10;
                    
                    ArticulationDrive currentDrive = joint.xDrive;
                    currentDrive.forceLimit = forceLimit;
                    currentDrive.stiffness = stiffness;
                    currentDrive.damping = damping;
                    joint.xDrive = currentDrive;
                }
            }
        }

        public void MoveToTargets(float[] targets, bool inRadians = true, int offset = 0)
        {
            if (articulationChain.Count == 0) return;

            for (int i = 0; i < targets.Length; i++)
            {
                int jointIndex = i + offset;
                
                // 범위 체크
                if (jointIndex >= articulationChain.Count) continue;

                ArticulationBody joint = articulationChain[jointIndex];
                float targetPosition = targets[i];

                // 회전 관절 단위 변환 (라디안 -> 도)
                if (joint.jointType == ArticulationJointType.RevoluteJoint && inRadians)
                {
                    targetPosition *= Mathf.Rad2Deg;
                }

                // 목표 위치 설정
                ArticulationDrive drive = joint.xDrive;
                drive.target = targetPosition;
                joint.xDrive = drive;
            }
        }
    }
}