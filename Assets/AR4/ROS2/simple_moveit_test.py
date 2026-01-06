#!/usr/bin/env python3

"""
Simple MoveIt Test - Debug version
"""

import rclpy
from rclpy.node import Node
from moveit_msgs.srv import GetPlanningScene
from geometry_msgs.msg import Pose
from sensor_msgs.msg import JointState
import time

class SimpleMoveItTest(Node):
    def __init__(self):
        super().__init__('simple_moveit_test')
        
        # Service clients
        self.scene_client = self.create_client(GetPlanningScene, '/get_planning_scene')
        
        # Joint command publisher for Unity
        self.joint_pub = self.create_publisher(JointState, '/joint_command', 10)
        
        self.get_logger().info('Simple MoveIt Test initialized')
        
    def test_simple_joint_movement(self):
        """Test simple joint movement without MoveIt planning"""
        self.get_logger().info("Testing simple joint movements...")
        
        # Predefined safe joint positions (in radians)
        test_positions = [
            [0.0, 0.0, 0.0, 0.0, 0.0, 0.0],        # Home
            [0.5, -0.3, 0.5, 0.0, 0.0, 0.0],       # Test pose 1
            [-0.5, -0.3, 0.5, 0.0, 0.0, 0.0],      # Test pose 2  
            [0.0, -0.8, 1.0, 0.0, 0.5, 0.0],       # Reach forward
            [0.0, 0.0, 0.0, 0.0, 0.0, 0.0],        # Back to home
        ]
        
        joint_names = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
        
        for i, positions in enumerate(test_positions):
            self.get_logger().info(f"Moving to test position {i+1}/5")
            
            # Create joint state message
            joint_msg = JointState()
            joint_msg.header.stamp = self.get_clock().now().to_msg()
            joint_msg.header.frame_id = 'ar4'
            joint_msg.name = joint_names
            joint_msg.position = positions
            
            # Publish to Unity
            self.joint_pub.publish(joint_msg)
            
            # Wait before next movement
            time.sleep(3.0)
            
        self.get_logger().info("✅ Simple joint movement test completed!")
        
    def check_planning_scene(self):
        """Check MoveIt planning scene info"""
        if not self.scene_client.wait_for_service(timeout_sec=5.0):
            self.get_logger().error("Planning scene service not available")
            return False
            
        request = GetPlanningScene.Request()
        request.components.components = GetPlanningScene.Request().components.SCENE_SETTINGS
        
        try:
            future = self.scene_client.call_async(request)
            rclpy.spin_until_future_complete(self, future)
            
            if future.result() is not None:
                scene = future.result().scene
                self.get_logger().info(f"Robot name: {scene.robot_model_name}")
                self.get_logger().info(f"Planning frame: {scene.planning_frame}")
                return True
            else:
                self.get_logger().error("Failed to get planning scene")
                return False
                
        except Exception as e:
            self.get_logger().error(f"Error getting planning scene: {e}")
            return False

def main():
    rclpy.init()
    
    test_node = SimpleMoveItTest()
    
    # Wait a bit for connections
    time.sleep(2)
    
    # Check MoveIt status
    print("\n🔍 Checking MoveIt planning scene...")
    if test_node.check_planning_scene():
        print("✅ MoveIt planning scene accessible")
    else:
        print("❌ MoveIt planning scene not accessible")
    
    # Run simple joint movement test
    print("\n🤖 Running simple joint movement test...")
    test_node.test_simple_joint_movement()
    
    print("\n✨ Test completed!")
    
    test_node.destroy_node()
    rclpy.shutdown()

if __name__ == '__main__':
    main()