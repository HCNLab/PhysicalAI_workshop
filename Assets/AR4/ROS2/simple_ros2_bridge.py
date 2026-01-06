#!/usr/bin/env python3

"""
Simple ROS2-Unity JSON Bridge
A simplified bridge for Unity-ROS2 communication using JSON over TCP
"""

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Header
import socket
import json
import threading
import time

class SimpleUnityROS2Bridge(Node):
    def __init__(self):
        super().__init__('simple_unity_ros2_bridge')
        
        # Parameters
        self.unity_ip = '0.0.0.0'
        self.unity_port = 10000
        self.joint_names = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
        
        # Publishers and Subscribers
        self.joint_state_pub = self.create_publisher(JointState, '/joint_states', 10)
        self.joint_command_sub = self.create_subscription(
            JointState, '/joint_command', self.joint_command_callback, 10
        )
        
        # TCP Socket for Unity
        self.socket = None
        self.client_socket = None
        self.setup_tcp_server()
        
        self.get_logger().info(f'Simple Unity ROS2 Bridge started on {self.unity_ip}:{self.unity_port}')
        
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
        """Handle incoming JSON messages from Unity"""
        buffer = ""
        while True:
            try:
                data = client_socket.recv(1024).decode('utf-8')
                if not data:
                    break
                
                buffer += data
                while '\n' in buffer:
                    line, buffer = buffer.split('\n', 1)
                    if line.strip():
                        self.process_unity_message(line.strip())
                        
            except Exception as e:
                self.get_logger().error(f'Error handling Unity message: {e}')
                break
        
        client_socket.close()
        self.client_socket = None
        self.get_logger().info('Unity disconnected')
    
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
                self.get_logger().info(f'Published joint states: {joint_state.position[:3]}...')
                
        except Exception as e:
            self.get_logger().error(f'Error processing Unity message: {e}')
    
    def joint_command_callback(self, msg):
        """Callback for joint commands from ROS2"""
        if self.client_socket is None:
            return
        
        try:
            # Convert ROS2 message to Unity JSON format
            command_data = {
                'topic': '/joint_command',
                'frame_id': msg.header.frame_id,
                'position': list(msg.position),
                'velocity': list(msg.velocity) if msg.velocity else [],
                'effort': list(msg.effort) if msg.effort else []
            }
            
            # Send JSON message to Unity
            json_msg = json.dumps(command_data) + '\n'
            self.client_socket.send(json_msg.encode('utf-8'))
            
            self.get_logger().info(f'Sent joint command to Unity: {msg.position[:3] if msg.position else []}...')
            
        except Exception as e:
            self.get_logger().error(f'Error sending command to Unity: {e}')

def main():
    rclpy.init()
    
    try:
        bridge = SimpleUnityROS2Bridge()
        rclpy.spin(bridge)
        
    except KeyboardInterrupt:
        pass
    finally:
        if 'bridge' in locals():
            bridge.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()