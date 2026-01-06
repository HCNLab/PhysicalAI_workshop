#!/usr/bin/env python3

"""
Launch file for Unity-ROS2 bridge
"""

from launch import LaunchDescription
from launch_ros.actions import Node
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration

def generate_launch_description():
    return LaunchDescription([
        # Declare launch arguments
        DeclareLaunchArgument(
            'unity_ip',
            default_value='127.0.0.1',
            description='IP address for Unity connection'
        ),
        DeclareLaunchArgument(
            'unity_port',
            default_value='10000',
            description='Port for Unity connection'
        ),
        DeclareLaunchArgument(
            'robot_name',
            default_value='ar4',
            description='Name of the robot'
        ),
        
        # Unity ROS2 Bridge Node
        Node(
            package='unity_ar4_bridge',  # Replace with your package name
            executable='ros_tcp_endpoint_setup.py',
            name='unity_ros2_bridge',
            output='screen',
            parameters=[{
                'unity_ip': LaunchConfiguration('unity_ip'),
                'unity_port': LaunchConfiguration('unity_port'),
                'joint_names': ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
            }]
        ),
        
        # Robot State Publisher (optional - for visualization in RViz)
        Node(
            package='robot_state_publisher',
            executable='robot_state_publisher',
            name='robot_state_publisher',
            output='screen',
            parameters=[{
                'robot_description': ''  # Add URDF content here if needed
            }]
        ),
        
        # Joint State Publisher GUI (optional - for manual control)
        Node(
            package='joint_state_publisher_gui',
            executable='joint_state_publisher_gui',
            name='joint_state_publisher_gui',
            output='screen'
        )
    ])