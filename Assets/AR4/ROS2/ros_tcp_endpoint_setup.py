#!/usr/bin/env python3

"""
ROS TCP Endpoint setup for Unity-ROS2 communication
This script sets up the TCP endpoint for Unity to communicate with ROS2

Requirements:
- pip install roslibpy
- ROS2 environment setup
- ros-tcp-endpoint package

Usage:
python ros_tcp_endpoint_setup.py
"""

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Header
import socket
import json
import threading
import time
from datetime import datetime

class UnityROS2Bridge(Node):
    def __init__(self):
        super().__init__('unity_ros2_bridge')
        
        # Parameters - Listen on all interfaces for WSL2-Windows communication
        self.declare_parameter('unity_ip', '0.0.0.0')  # Listen on all interfaces
        self.declare_parameter('unity_port', 10000)
        self.declare_parameter('joint_names', ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6'])
        
        self.unity_ip = self.get_parameter('unity_ip').value
        self.unity_port = self.get_parameter('unity_port').value
        self.joint_names = self.get_parameter('joint_names').value
        
        # Publishers and Subscribers
        self.joint_state_pub = self.create_publisher(JointState, '/joint_states', 10)
        self.joint_command_sub = self.create_subscription(
            JointState, '/joint_command', self.joint_command_callback, 10
        )
        
        # TCP Socket for Unity
        self.socket = None
        self.client_socket = None
        self.setup_tcp_server()
        
        # State
        self.latest_joint_state = None
        
        self.get_logger().info(f'Unity ROS2 Bridge started on {self.unity_ip}:{self.unity_port}')
        
    def setup_tcp_server(self):
        """Setup TCP server to receive data from Unity"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.unity_ip, self.unity_port))
            self.socket.listen(1)
            
            # Start listening thread
            threading.Thread(target=self.listen_for_unity, daemon=True).start()
            self.get_logger().info(f'TCP server listening on {self.unity_ip}:{self.unity_port}')
        except Exception as e:
            self.get_logger().error(f'Failed to setup TCP server: {e}')
    
    def listen_for_unity(self):
        """Listen for connections from Unity"""
        while True:
            try:
                self.get_logger().info('Waiting for Unity connection...')
                client_socket, address = self.socket.accept()
                self.client_socket = client_socket
                self.get_logger().info(f'Unity connected from {address}')
                
                # Handle Unity messages
                self.handle_unity_connection(client_socket)
                
            except Exception as e:
                self.get_logger().error(f'Connection error: {e}')
                time.sleep(1)
    
    def handle_unity_connection(self, client_socket):
        """Handle incoming messages from Unity using ROS TCP Connector protocol"""
        import struct
        
        while True:
            try:
                # ROS TCP Connector sends messages with length prefix
                # First read 4 bytes for message length
                length_data = client_socket.recv(4)
                if not length_data or len(length_data) < 4:
                    break
                
                message_length = struct.unpack('<I', length_data)[0]
                
                # Read the actual message
                message_data = b''
                while len(message_data) < message_length:
                    chunk = client_socket.recv(message_length - len(message_data))
                    if not chunk:
                        break
                    message_data += chunk
                
                if len(message_data) == message_length:
                    self.process_ros_tcp_message(message_data)
                        
            except Exception as e:
                self.get_logger().error(f'Error handling Unity message: {e}')
                break
        
        client_socket.close()
        self.client_socket = None
        self.get_logger().info('Unity disconnected')
    
    def process_ros_tcp_message(self, message_data):
        """Process ROS TCP Connector message from Unity"""
        try:
            import struct
            
            # ROS TCP Connector message format:
            # Topic name length (4 bytes) + Topic name + Message data
            if len(message_data) < 4:
                return
            
            topic_length = struct.unpack('<I', message_data[:4])[0]
            if len(message_data) < 4 + topic_length:
                return
                
            topic_name = message_data[4:4+topic_length].decode('utf-8')
            ros_message_data = message_data[4+topic_length:]
            
            self.get_logger().info(f'Received message on topic: {topic_name}')
            
            if topic_name == '/joint_states':
                # Deserialize JointState message
                joint_state = self.deserialize_joint_state(ros_message_data)
                if joint_state:
                    # Publish to ROS2
                    self.joint_state_pub.publish(joint_state)
                    self.latest_joint_state = joint_state
                    self.get_logger().info(f'Published joint states with {len(joint_state.position)} joints')
                    
        except Exception as e:
            self.get_logger().error(f'Error processing ROS TCP message: {e}')
    
    def deserialize_joint_state(self, data):
        """Deserialize JointState message from ROS TCP Connector format"""
        try:
            import struct
            
            # This is a simplified deserializer - you may need to adjust based on actual message format
            # For now, we'll create a basic joint state with dummy data
            joint_state = JointState()
            joint_state.header = Header()
            joint_state.header.stamp = self.get_clock().now().to_msg()
            joint_state.header.frame_id = 'ar4'
            joint_state.name = self.joint_names
            
            # For now, use zeros - proper deserialization would parse the binary data
            joint_state.position = [0.0] * len(self.joint_names)
            joint_state.velocity = [0.0] * len(self.joint_names) 
            joint_state.effort = [0.0] * len(self.joint_names)
            
            return joint_state
            
        except Exception as e:
            self.get_logger().error(f'Error deserializing joint state: {e}')
            return None
    
    def process_unity_message(self, message):
        """Process JSON message from Unity"""
        try:
            data = json.loads(message)
            
            if data.get('topic') == '/joint_states':
                # Convert Unity joint state to ROS2 message
                joint_state = JointState()
                joint_state.header = Header()
                joint_state.header.stamp = self.get_clock().now().to_msg()
                joint_state.header.frame_id = data.get('frame_id', 'ar4')
                
                joint_state.name = self.joint_names
                joint_state.position = data.get('position', [0.0] * len(self.joint_names))
                joint_state.velocity = data.get('velocity', [0.0] * len(self.joint_names))
                joint_state.effort = data.get('effort', [0.0] * len(self.joint_names))
                
                # Publish to ROS2
                self.joint_state_pub.publish(joint_state)
                self.latest_joint_state = joint_state
                
        except Exception as e:
            self.get_logger().error(f'Error processing Unity message: {e}')
    
    def joint_command_callback(self, msg):
        """Callback for joint commands from ROS2"""
        if self.client_socket is None:
            return
        
        try:
            import struct
            
            # Serialize JointState message to ROS TCP Connector format
            topic_name = '/joint_command'
            
            # Create a basic serialized message (simplified)
            # In a full implementation, you'd properly serialize the ROS message
            serialized_msg = self.serialize_joint_state(msg)
            
            # ROS TCP Connector format: topic_length + topic_name + message_data
            topic_bytes = topic_name.encode('utf-8')
            topic_length = struct.pack('<I', len(topic_bytes))
            
            full_message = topic_length + topic_bytes + serialized_msg
            message_length = struct.pack('<I', len(full_message))
            
            # Send to Unity
            self.client_socket.send(message_length + full_message)
            
            self.get_logger().info(f'Sent joint command to Unity: {msg.position[:3] if msg.position else []}...')
            
        except Exception as e:
            self.get_logger().error(f'Error sending command to Unity: {e}')
    
    def serialize_joint_state(self, msg):
        """Serialize JointState message to binary format"""
        try:
            import struct
            
            # This is a simplified serializer
            # A full implementation would properly serialize all ROS message fields
            data = b''
            
            # Serialize positions (simplified - just pack as doubles)
            if msg.position:
                for pos in msg.position:
                    data += struct.pack('<d', pos)
            
            return data
            
        except Exception as e:
            self.get_logger().error(f'Error serializing joint state: {e}')
            return b''
    
    def publish_test_command(self):
        """Publish test joint command for testing"""
        if self.latest_joint_state is None:
            return
        
        # Create test command (small sine wave movement)
        test_cmd = JointState()
        test_cmd.header = Header()
        test_cmd.header.stamp = self.get_clock().now().to_msg()
        test_cmd.header.frame_id = 'ar4'
        test_cmd.name = self.joint_names
        
        import math
        t = time.time()
        test_positions = []
        for i in range(len(self.joint_names)):
            # Small sine wave around current position
            if self.latest_joint_state.position:
                base = self.latest_joint_state.position[i] if i < len(self.latest_joint_state.position) else 0.0
                offset = 0.1 * math.sin(t + i)  # 0.1 radian amplitude
                test_positions.append(base + offset)
            else:
                test_positions.append(0.1 * math.sin(t + i))
        
        test_cmd.position = test_positions
        
        # Send command (this will trigger joint_command_callback)
        self.joint_command_sub.callback(test_cmd)

def main():
    rclpy.init()
    
    try:
        bridge = UnityROS2Bridge()
        
        # Optional: Create test command publisher for demonstration
        def test_publisher():
            rate = bridge.create_rate(1.0)  # 1 Hz
            while rclpy.ok():
                bridge.publish_test_command()
                rate.sleep()
        
        # Uncomment to enable test commands
        # threading.Thread(target=test_publisher, daemon=True).start()
        
        rclpy.spin(bridge)
        
    except KeyboardInterrupt:
        pass
    finally:
        if 'bridge' in locals():
            bridge.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()