#!/usr/bin/env python3

"""
AR4 Joint Sequence Controller
Executes predefined joint sequences for automated robot movements
"""

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Header
import time
import math
import threading

class JointSequenceController(Node):
    def __init__(self):
        super().__init__('joint_sequence_controller')
        
        # Publisher
        self.joint_pub = self.create_publisher(JointState, '/joint_command', 10)
        
        # Joint names
        self.joint_names = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
        
        # Predefined poses (in radians)
        self.poses = {
            'home': [0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
            'ready': [0.0, -0.5, 0.5, 0.0, 0.0, 0.0],
            'pick_approach': [0.3, -0.8, 1.0, 0.0, 0.5, 0.0],
            'pick_down': [0.3, -1.0, 1.2, 0.0, 0.8, 0.0],
            'pick_up': [0.3, -0.8, 1.0, 0.0, 0.5, 0.0],
            'place_approach': [-0.3, -0.8, 1.0, 0.0, 0.5, 0.0],
            'place_down': [-0.3, -1.0, 1.2, 0.0, 0.8, 0.0],
            'place_up': [-0.3, -0.8, 1.0, 0.0, 0.5, 0.0],
        }
        
        # Movement parameters
        self.movement_duration = 2.0  # seconds per movement
        self.interpolation_steps = 50  # steps per movement
        
        self.get_logger().info('Joint Sequence Controller initialized')
        self.print_menu()
        
    def print_menu(self):
        """Print available commands"""
        print("\n" + "="*50)
        print("AR4 Joint Sequence Controller")
        print("="*50)
        print("Available commands:")
        print("1. demo_sequence() - Run full pick & place demo")
        print("2. go_to_pose('pose_name') - Move to specific pose")
        print("3. smooth_move(start_pose, end_pose, duration) - Smooth interpolated movement")
        print("4. wave_motion() - Continuous waving motion")
        print("5. Available poses:", list(self.poses.keys()))
        print("="*50)
        
    def publish_joint_command(self, positions):
        """Publish joint command"""
        msg = JointState()
        msg.header = Header()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = 'ar4'
        msg.name = self.joint_names
        msg.position = positions
        msg.velocity = []
        msg.effort = []
        
        self.joint_pub.publish(msg)
        
    def go_to_pose(self, pose_name, duration=None):
        """Move to a predefined pose"""
        if pose_name not in self.poses:
            self.get_logger().error(f"Unknown pose: {pose_name}")
            return False
            
        if duration is None:
            duration = self.movement_duration
            
        target_pose = self.poses[pose_name]
        
        self.get_logger().info(f"Moving to pose '{pose_name}' over {duration:.1f}s")
        self.publish_joint_command(target_pose)
        time.sleep(duration)
        return True
        
    def smooth_move(self, start_pose_name, end_pose_name, duration=None):
        """Smooth interpolated movement between two poses"""
        if start_pose_name not in self.poses or end_pose_name not in self.poses:
            self.get_logger().error("Unknown pose names")
            return False
            
        if duration is None:
            duration = self.movement_duration
            
        start_pose = self.poses[start_pose_name]
        end_pose = self.poses[end_pose_name]
        
        self.get_logger().info(f"Smooth move: {start_pose_name} → {end_pose_name} over {duration:.1f}s")
        
        # Interpolate between poses
        for i in range(self.interpolation_steps + 1):
            t = i / self.interpolation_steps  # 0 to 1
            # Use cubic interpolation for smoother motion
            t_smooth = t * t * (3.0 - 2.0 * t)
            
            interpolated_pose = []
            for j in range(len(start_pose)):
                pos = start_pose[j] + t_smooth * (end_pose[j] - start_pose[j])
                interpolated_pose.append(pos)
                
            self.publish_joint_command(interpolated_pose)
            time.sleep(duration / self.interpolation_steps)
            
        return True
        
    def demo_sequence(self):
        """Execute full pick & place demonstration sequence"""
        self.get_logger().info("Starting Pick & Place Demo Sequence")
        
        sequence = [
            ('home', 2.0),
            ('ready', 2.0),
            ('pick_approach', 3.0),
            ('pick_down', 2.0),
            ('pick_up', 2.0),
            ('place_approach', 3.0),
            ('place_down', 2.0),
            ('place_up', 2.0),
            ('ready', 2.0),
            ('home', 2.0)
        ]
        
        for pose_name, duration in sequence:
            self.get_logger().info(f"→ Moving to {pose_name}")
            if not self.go_to_pose(pose_name, duration):
                break
                
        self.get_logger().info("Demo sequence completed!")
        
    def wave_motion(self, duration=10.0):
        """Continuous waving motion with joint 1"""
        self.get_logger().info(f"Starting wave motion for {duration:.1f}s")
        
        start_time = time.time()
        base_pose = self.poses['ready'].copy()
        
        while (time.time() - start_time) < duration:
            # Create sine wave motion on joint 1
            t = time.time() - start_time
            wave_angle = 0.5 * math.sin(2 * math.pi * 0.5 * t)  # 0.5 Hz, ±0.5 rad amplitude
            
            current_pose = base_pose.copy()
            current_pose[0] = wave_angle  # joint_1
            
            self.publish_joint_command(current_pose)
            time.sleep(0.05)  # 20 Hz update rate
            
        # Return to ready position
        self.go_to_pose('ready', 1.0)
        self.get_logger().info("Wave motion completed!")
        
    def custom_sequence(self, pose_sequence, durations=None):
        """Execute custom pose sequence"""
        if durations is None:
            durations = [self.movement_duration] * len(pose_sequence)
        elif len(durations) != len(pose_sequence):
            self.get_logger().error("Duration list length must match pose sequence length")
            return False
            
        self.get_logger().info(f"Starting custom sequence: {pose_sequence}")
        
        for pose_name, duration in zip(pose_sequence, durations):
            self.get_logger().info(f"→ Moving to {pose_name}")
            if not self.go_to_pose(pose_name, duration):
                break
                
        self.get_logger().info("Custom sequence completed!")
        return True

def main():
    rclpy.init()
    
    controller = JointSequenceController()
    
    # Example usage in a separate thread to allow interactive control
    def run_demo():
        time.sleep(2)  # Give time to see the menu
        print("\n🤖 Starting demo sequence in 3 seconds...")
        time.sleep(3)
        controller.demo_sequence()
        
        print("\n🌊 Starting wave motion in 2 seconds...")
        time.sleep(2)
        controller.wave_motion(5.0)
        
        print("\n✅ Demo completed!")
    
    # Start demo in background
    demo_thread = threading.Thread(target=run_demo, daemon=True)
    demo_thread.start()
    
    try:
        rclpy.spin(controller)
    except KeyboardInterrupt:
        print("\nShutting down Joint Sequence Controller...")
    finally:
        controller.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()