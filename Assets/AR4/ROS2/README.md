# Unity-ROS2 AR4 Robot Integration

## Setup Instructions

### 1. Unity Setup
- ROS TCP Connector package is already installed
- Add ROS2Manager script to your AR4 robot GameObject
- Configure the ROS2Manager with your ROS2 system IP address

### 2. ROS2 Environment Setup

#### Install Dependencies
```bash
# Install ROS2 TCP Endpoint
sudo apt install ros-<distro>-ros-tcp-endpoint

# Install Python dependencies
pip install roslibpy

# Install additional ROS2 packages (optional)
sudo apt install ros-<distro>-joint-state-publisher-gui
sudo apt install ros-<distro>-robot-state-publisher
```

#### Create ROS2 Workspace
```bash
# Create workspace
mkdir -p ~/unity_ar4_ws/src
cd ~/unity_ar4_ws/src

# Create package
ros2 pkg create unity_ar4_bridge --build-type ament_python

# Copy Python scripts to package
cp ros_tcp_endpoint_setup.py ~/unity_ar4_ws/src/unity_ar4_bridge/unity_ar4_bridge/
cp launch_unity_bridge.launch.py ~/unity_ar4_ws/src/unity_ar4_bridge/launch/

# Build workspace
cd ~/unity_ar4_ws
colcon build
source install/setup.bash
```

### 3. Running the System

#### Method 1: Launch File (Recommended)
```bash
# Terminal 1: Source ROS2 and launch bridge
cd ~/unity_ar4_ws
source install/setup.bash
ros2 launch unity_ar4_bridge launch_unity_bridge.launch.py

# Terminal 2: Start Unity project
# (Run your Unity project with ROS2Manager configured)
```

#### Method 2: Manual Nodes
```bash
# Terminal 1: ROS TCP Endpoint
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=0.0.0.0

# Terminal 2: Unity Bridge Script  
cd ~/unity_ar4_ws
source install/setup.bash
python3 src/unity_ar4_bridge/unity_ar4_bridge/ros_tcp_endpoint_setup.py

# Terminal 3: Start Unity project
```

### 4. Testing Communication

#### Check Topics
```bash
# List active topics
ros2 topic list

# Monitor joint states from Unity
ros2 topic echo /joint_states

# Send joint commands to Unity
ros2 topic pub /joint_command sensor_msgs/JointState '{
  header: {frame_id: "ar4"},
  name: ["joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6"],
  position: [0.0, 0.5, -0.5, 0.0, 0.0, 0.0]
}'
```

#### Monitor with RViz (Optional)
```bash
# Install RViz2
sudo apt install ros-<distro>-rviz2

# Launch RViz
ros2 run rviz2 rviz2
```

## Troubleshooting

### Common Issues

1. **Connection Failed**
   - Check IP addresses match between Unity and ROS2
   - Ensure firewall allows port 10000
   - Verify ROS2 environment is sourced

2. **No Joint States Received**
   - Verify AR4JoggerPanel is assigned in ROS2Manager
   - Check Unity Console for error messages
   - Monitor ROS2 topics: `ros2 topic list`

3. **Joint Commands Not Working**
   - Ensure joint names match between Unity and ROS2
   - Check joint limits in Unity ArticulationBodies
   - Verify message format with `ros2 topic echo /joint_command`

### Debug Commands
```bash
# Check ROS2 environment
printenv | grep ROS

# Test ROS TCP Endpoint
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1

# Monitor all topics
ros2 topic list
ros2 topic hz /joint_states
ros2 topic bw /joint_states
```

## Configuration

### Unity ROS2Manager Settings
- **ROS IP Address**: IP of your ROS2 system (default: 127.0.0.1)
- **ROS Port**: TCP port for communication (default: 10000) 
- **Publish Rate**: Joint state publishing frequency (default: 30 Hz)
- **Joint Names**: Must match ROS2 joint names exactly

### ROS2 Parameters
```bash
# Set parameters via command line
ros2 run unity_ar4_bridge ros_tcp_endpoint_setup.py --ros-args -p unity_ip:=192.168.1.100 -p unity_port:=10000
```