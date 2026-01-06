#!/usr/bin/env python3

"""
MoveIt-Unity Bridge
Integrates MoveIt path planning with Unity visualization
"""

import rclpy
from rclpy.node import Node
from rclpy.action import ActionClient
from sensor_msgs.msg import JointState
from geometry_msgs.msg import Pose, PoseStamped, Point, Quaternion
from moveit_msgs.action import MoveGroup
from moveit_msgs.msg import MotionPlanRequest, Constraints, JointConstraint, PositionConstraint, OrientationConstraint
from moveit_msgs.msg import PlanningScene, CollisionObject
from moveit_msgs.srv import GetPositionFK, GetPositionIK
from shape_msgs.msg import SolidPrimitive
from std_msgs.msg import Header
import numpy as np
import time
from threading import Thread

class MoveItUnityBridge(Node):
    def __init__(self):
        super().__init__('moveit_unity_bridge')
        
        # MoveIt Action Client
        self.move_group_client = ActionClient(self, MoveGroup, '/move_action')
        
        # Publishers for Unity
        self.joint_pub = self.create_publisher(JointState, '/joint_command', 10)
        self.trajectory_pub = self.create_publisher(JointState, '/trajectory_preview', 10)
        
        # Services
        self.fk_client = self.create_client(GetPositionFK, '/compute_fk')
        self.ik_client = self.create_client(GetPositionIK, '/compute_ik')
        
        # Planning Scene Publisher
        self.scene_pub = self.create_publisher(PlanningScene, '/planning_scene', 10)
        
        # Robot configuration
        self.group_name = 'ar_manipulator'
        self.joint_names = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
        self.ee_link = 'link_6'
        self.base_frame = 'base_link'
        
        # Current robot state
        self.current_joint_state = [0.0] * 6
        
        # Predefined target poses (x, y, z, roll, pitch, yaw) - Conservative positions
        self.target_poses = {
            'front_close': [0.2, 0.0, 0.3, 0.0, 0.0, 0.0],         # Closer, safer front position
            'front_mid': [0.25, 0.0, 0.35, 0.0, 0.0, 0.0],         # Medium front reach
            'left_close': [0.1, 0.2, 0.3, 0.0, 0.0, 0.5],          # Less extreme left turn
            'right_close': [0.1, -0.2, 0.3, 0.0, 0.0, -0.5],       # Less extreme right turn  
            'up_center': [0.15, 0.0, 0.4, 0.0, 0.0, 0.0],          # Safe upward reach
            'home_ready': [0.0, 0.0, 0.2, 0.0, 0.0, 0.0]           # Near home position
        }
        
        self.get_logger().info('MoveIt-Unity Bridge initialized')
        self.print_menu()
        
        # Wait for MoveIt services
        self.wait_for_services()
        
    def wait_for_services(self):
        """Wait for MoveIt services to become available"""
        self.get_logger().info('Waiting for MoveIt services...')
        
        while not self.move_group_client.wait_for_server(timeout_sec=1.0):
            self.get_logger().info('MoveGroup action server not available, waiting...')
        
        while not self.fk_client.wait_for_service(timeout_sec=1.0):
            self.get_logger().info('FK service not available, waiting...')
            
        while not self.ik_client.wait_for_service(timeout_sec=1.0):
            self.get_logger().info('IK service not available, waiting...')
        
        self.get_logger().info('All MoveIt services are available!')
        
    def print_menu(self):
        """Print available commands"""
        print("\n" + "="*60)
        print("🤖 MoveIt-Unity Path Planning Bridge")
        print("="*60)
        print("Available commands:")
        print("1. plan_to_pose('pose_name') - Plan path to predefined pose")
        print("2. plan_to_joint_state([j1,j2,j3,j4,j5,j6]) - Plan to joint configuration")
        print("3. execute_plan() - Execute the last planned trajectory")
        print("4. plan_and_execute('pose_name') - Plan and execute in one step")
        print("5. add_box_obstacle(name, pose, size) - Add box obstacle")
        print("6. clear_obstacles() - Remove all obstacles")
        print("7. cartesian_path(waypoints) - Plan Cartesian path through waypoints")
        print(f"8. Available poses: {list(self.target_poses.keys())}")
        print("="*60)
        
    def create_pose_goal(self, pose_data):
        """Create PoseStamped from pose data [x,y,z,r,p,y]"""
        pose_stamped = PoseStamped()
        pose_stamped.header.frame_id = self.base_frame
        pose_stamped.header.stamp = self.get_clock().now().to_msg()
        
        # Position
        pose_stamped.pose.position.x = pose_data[0]
        pose_stamped.pose.position.y = pose_data[1] 
        pose_stamped.pose.position.z = pose_data[2]
        
        # Orientation (from RPY to quaternion)
        roll, pitch, yaw = pose_data[3], pose_data[4], pose_data[5]
        qx = np.sin(roll/2) * np.cos(pitch/2) * np.cos(yaw/2) - np.cos(roll/2) * np.sin(pitch/2) * np.sin(yaw/2)
        qy = np.cos(roll/2) * np.sin(pitch/2) * np.cos(yaw/2) + np.sin(roll/2) * np.cos(pitch/2) * np.sin(yaw/2)
        qz = np.cos(roll/2) * np.cos(pitch/2) * np.sin(yaw/2) - np.sin(roll/2) * np.sin(pitch/2) * np.cos(yaw/2)
        qw = np.cos(roll/2) * np.cos(pitch/2) * np.cos(yaw/2) + np.sin(roll/2) * np.sin(pitch/2) * np.sin(yaw/2)
        
        pose_stamped.pose.orientation = Quaternion(x=qx, y=qy, z=qz, w=qw)
        return pose_stamped
        
    def plan_to_pose(self, pose_name):
        """Plan motion to a named pose"""
        if pose_name not in self.target_poses:
            self.get_logger().error(f"Unknown pose: {pose_name}")
            return False
            
        self.get_logger().info(f"Planning path to pose: {pose_name}")
        
        # Create goal pose
        target_pose = self.create_pose_goal(self.target_poses[pose_name])
        
        # Create MoveGroup action goal
        goal_msg = MoveGroup.Goal()
        goal_msg.request.group_name = self.group_name
        goal_msg.request.num_planning_attempts = 10
        goal_msg.request.allowed_planning_time = 5.0
        goal_msg.request.planner_id = "RRTConnect"
        
        # Set pose goal
        position_constraint = PositionConstraint()
        position_constraint.header = target_pose.header
        position_constraint.link_name = self.ee_link
        position_constraint.constraint_region.primitives.append(SolidPrimitive())
        position_constraint.constraint_region.primitives[0].type = SolidPrimitive.SPHERE
        position_constraint.constraint_region.primitives[0].dimensions = [0.01]  # 1cm tolerance
        position_constraint.constraint_region.primitive_poses.append(target_pose.pose)
        position_constraint.weight = 1.0
        
        orientation_constraint = OrientationConstraint()
        orientation_constraint.header = target_pose.header
        orientation_constraint.link_name = self.ee_link
        orientation_constraint.orientation = target_pose.pose.orientation
        orientation_constraint.absolute_x_axis_tolerance = 0.1
        orientation_constraint.absolute_y_axis_tolerance = 0.1
        orientation_constraint.absolute_z_axis_tolerance = 0.1
        orientation_constraint.weight = 1.0
        
        goal_msg.request.goal_constraints.append(Constraints())
        goal_msg.request.goal_constraints[0].position_constraints.append(position_constraint)
        goal_msg.request.goal_constraints[0].orientation_constraints.append(orientation_constraint)
        
        # Send goal
        self.get_logger().info("Sending planning request...")
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
        return True
        
    def plan_result_callback(self, future):
        """Handle planning result"""
        result = future.result().result
        
        if result.error_code.val == 1:  # SUCCESS
            self.get_logger().info("✅ Planning successful!")
            
            # Store the planned trajectory
            self.last_trajectory = result.planned_trajectory
            
            # Preview trajectory in Unity
            self.preview_trajectory(result.planned_trajectory)
            
            self.get_logger().info("Trajectory preview sent to Unity. Use execute_plan() to execute.")
        else:
            self.get_logger().error(f"❌ Planning failed with error code: {result.error_code.val}")
            
    def preview_trajectory(self, trajectory):
        """Send trajectory waypoints to Unity for visualization"""
        if not trajectory.joint_trajectory.points:
            return
            
        self.get_logger().info(f"Previewing trajectory with {len(trajectory.joint_trajectory.points)} waypoints")
        
        for i, point in enumerate(trajectory.joint_trajectory.points):
            # Create JointState message for each waypoint
            joint_msg = JointState()
            joint_msg.header.stamp = self.get_clock().now().to_msg()
            joint_msg.header.frame_id = f'waypoint_{i}'
            joint_msg.name = self.joint_names
            joint_msg.position = list(point.positions)
            
            # Publish to trajectory preview topic
            self.trajectory_pub.publish(joint_msg)
            time.sleep(0.1)  # Small delay for visualization
            
    def execute_plan(self):
        """Execute the last planned trajectory"""
        if not hasattr(self, 'last_trajectory'):
            self.get_logger().error("No trajectory to execute. Plan first!")
            return False
            
        self.get_logger().info("🚀 Executing planned trajectory...")
        
        # Execute trajectory by publishing joint commands
        trajectory = self.last_trajectory.joint_trajectory
        
        for i, point in enumerate(trajectory.points):
            self.get_logger().info(f"Executing waypoint {i+1}/{len(trajectory.points)}")
            
            # Create and publish joint command
            joint_cmd = JointState()
            joint_cmd.header.stamp = self.get_clock().now().to_msg()
            joint_cmd.header.frame_id = 'ar4'
            joint_cmd.name = self.joint_names
            joint_cmd.position = list(point.positions)
            
            self.joint_pub.publish(joint_cmd)
            
            # Wait based on trajectory timing
            if i < len(trajectory.points) - 1:
                current_time = point.time_from_start.sec + point.time_from_start.nanosec * 1e-9
                next_time = trajectory.points[i+1].time_from_start.sec + trajectory.points[i+1].time_from_start.nanosec * 1e-9
                sleep_time = next_time - current_time
                time.sleep(max(0.1, sleep_time))  # At least 100ms between waypoints
                
        self.get_logger().info("✅ Trajectory execution completed!")
        return True
        
    def plan_and_execute(self, pose_name):
        """Plan and execute motion to pose in one step"""
        if self.plan_to_pose(pose_name):
            # Wait a bit for planning to complete
            time.sleep(2.0)
            return self.execute_plan()
        return False
        
    def add_box_obstacle(self, name, pose, size):
        """Add a box obstacle to the planning scene"""
        collision_object = CollisionObject()
        collision_object.header.frame_id = self.base_frame
        collision_object.id = name
        
        # Define box primitive
        box = SolidPrimitive()
        box.type = SolidPrimitive.BOX
        box.dimensions = size  # [x, y, z]
        
        collision_object.primitives.append(box)
        collision_object.primitive_poses.append(pose)
        collision_object.operation = CollisionObject.ADD
        
        # Create planning scene message
        planning_scene = PlanningScene()
        planning_scene.world.collision_objects.append(collision_object)
        planning_scene.is_diff = True
        
        self.scene_pub.publish(planning_scene)
        self.get_logger().info(f"Added box obstacle '{name}' to planning scene")
        
    def clear_obstacles(self):
        """Remove all obstacles from planning scene"""
        planning_scene = PlanningScene()
        planning_scene.is_diff = True
        planning_scene.world.collision_objects.append(CollisionObject())
        planning_scene.world.collision_objects[0].operation = CollisionObject.REMOVE
        
        self.scene_pub.publish(planning_scene)
        self.get_logger().info("Cleared all obstacles from planning scene")

def main():
    rclpy.init()
    
    bridge = MoveItUnityBridge()
    
    # Example usage in a separate thread
    def demo_sequence():
        time.sleep(3)  # Wait for initialization
        
        print("\n🎯 Starting MoveIt-Unity demo...")
        
        # Demo sequence - Conservative poses
        poses_to_visit = ['home_ready', 'front_close', 'left_close', 'up_center', 'right_close', 'front_mid']
        
        for pose_name in poses_to_visit:
            print(f"\n📍 Planning to {pose_name}...")
            if bridge.plan_and_execute(pose_name):
                print(f"✅ Reached {pose_name}")
                time.sleep(1)
            else:
                print(f"❌ Failed to reach {pose_name}")
        
        print("\n🎉 Demo sequence completed!")
    
    # Start demo in background
    demo_thread = Thread(target=demo_sequence, daemon=True)
    demo_thread.start()
    
    try:
        rclpy.spin(bridge)
    except KeyboardInterrupt:
        print("\nShutting down MoveIt-Unity Bridge...")
    finally:
        bridge.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()