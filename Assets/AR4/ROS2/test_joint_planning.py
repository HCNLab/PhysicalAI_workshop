#!/usr/bin/env python3

"""
Test Joint Space Planning
Simple test for MoveIt joint space planning
"""

import rclpy
from rclpy.node import Node
from rclpy.action import ActionClient
from moveit_msgs.action import MoveGroup
from moveit_msgs.msg import Constraints, JointConstraint
from sensor_msgs.msg import JointState
from std_msgs.msg import Header
import time

class JointPlanningTest(Node):
    def __init__(self):
        super().__init__('joint_planning_test')
        
        # MoveIt Action Client
        self.move_group_client = ActionClient(self, MoveGroup, '/move_action')
        
        # Joint command publisher
        self.joint_pub = self.create_publisher(JointState, '/joint_command', 10)
        
        # Robot configuration
        self.group_name = 'ar_manipulator'
        self.joint_names = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
        
        self.get_logger().info('Joint Planning Test initialized')
        
        # Wait for MoveIt
        while not self.move_group_client.wait_for_server(timeout_sec=1.0):
            self.get_logger().info('Waiting for MoveGroup action server...')
        
        self.get_logger().info('MoveGroup action server available!')
        
    def plan_to_joint_state(self, joint_positions, joint_names=None):
        """Plan to specific joint state"""
        if joint_names is None:
            joint_names = self.joint_names
            
        self.get_logger().info(f"Planning to joint state: {joint_positions}")
        
        # Create MoveGroup action goal
        goal_msg = MoveGroup.Goal()
        goal_msg.request.group_name = self.group_name
        goal_msg.request.num_planning_attempts = 10
        goal_msg.request.allowed_planning_time = 10.0
        goal_msg.request.planner_id = "RRTConnect"
        
        # Set joint constraints
        joint_constraints = []
        for i, (name, pos) in enumerate(zip(joint_names, joint_positions)):
            constraint = JointConstraint()
            constraint.joint_name = name
            constraint.position = pos
            constraint.tolerance_above = 0.01
            constraint.tolerance_below = 0.01
            constraint.weight = 1.0
            joint_constraints.append(constraint)
        
        goal_msg.request.goal_constraints.append(Constraints())
        goal_msg.request.goal_constraints[0].joint_constraints = joint_constraints
        
        # Send goal
        self.get_logger().info("Sending joint planning request...")
        send_goal_future = self.move_group_client.send_goal_async(goal_msg)
        
        def plan_response_callback(future):
            goal_handle = future.result()
            if not goal_handle.accepted:
                self.get_logger().error("Planning goal rejected")
                return
                
            self.get_logger().info("Planning goal accepted, waiting for result...")
            result_future = goal_handle.get_result_async()
            result_future.add_done_callback(self.plan_result_callback)
        
        send_goal_future.add_done_callback(plan_response_callback)
        
    def plan_result_callback(self, future):
        """Handle planning result"""
        result = future.result().result
        
        if result.error_code.val == 1:  # SUCCESS
            self.get_logger().info("✅ Joint planning successful!")
            
            # Execute trajectory
            self.execute_trajectory(result.planned_trajectory)
        else:
            self.get_logger().error(f"❌ Joint planning failed with error code: {result.error_code.val}")
            
    def execute_trajectory(self, trajectory):
        """Execute planned trajectory"""
        if not trajectory.joint_trajectory.points:
            self.get_logger().error("Empty trajectory")
            return
            
        self.get_logger().info(f"🚀 Executing trajectory with {len(trajectory.joint_trajectory.points)} waypoints")
        
        for i, point in enumerate(trajectory.joint_trajectory.points):
            self.get_logger().info(f"Executing waypoint {i+1}/{len(trajectory.joint_trajectory.points)}")
            
            # Create and publish joint command
            joint_cmd = JointState()
            joint_cmd.header.stamp = self.get_clock().now().to_msg()
            joint_cmd.header.frame_id = 'ar4'
            joint_cmd.name = self.joint_names
            joint_cmd.position = list(point.positions)
            
            self.joint_pub.publish(joint_cmd)
            
            # Wait based on trajectory timing
            if i < len(trajectory.joint_trajectory.points) - 1:
                current_time = point.time_from_start.sec + point.time_from_start.nanosec * 1e-9
                next_time = trajectory.joint_trajectory.points[i+1].time_from_start.sec + trajectory.joint_trajectory.points[i+1].time_from_start.nanosec * 1e-9
                sleep_time = next_time - current_time
                time.sleep(max(0.2, sleep_time))  # At least 200ms between waypoints
                
        self.get_logger().info("✅ Trajectory execution completed!")

def main():
    rclpy.init()
    
    test_node = JointPlanningTest()
    
    # Wait a bit
    time.sleep(2)
    
    print("\n🤖 Testing joint space planning...")
    
    # Test joint configurations (safe positions in radians)
    test_configs = [
        [0.0, 0.0, 0.0, 0.0, 0.0, 0.0],           # Home position
        [0.5, 0.0, 0.0, 0.0, 0.0, 0.0],           # Joint 1 only
        [0.0, -0.3, 0.0, 0.0, 0.0, 0.0],          # Joint 2 only  
        [0.0, -0.3, 0.5, 0.0, 0.0, 0.0],          # Joints 2+3
        [0.3, -0.5, 0.8, 0.0, 0.3, 0.0],          # Multiple joints
        [0.0, 0.0, 0.0, 0.0, 0.0, 0.0],           # Back to home
    ]
    
    for i, config in enumerate(test_configs):
        print(f"\n📍 Test {i+1}/{len(test_configs)}: Planning to {config}")
        test_node.plan_to_joint_state(config)
        time.sleep(8)  # Wait for completion
    
    print("\n✨ Joint space planning test completed!")
    
    test_node.destroy_node()
    rclpy.shutdown()

if __name__ == '__main__':
    main()