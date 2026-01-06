## 단계 6: 자동화된 관절 시퀀스 실행

Unity-ROS2 연동이 완료되었다면, 이제 복잡한 로봇 동작 시퀀스를 자동화할 수 있습니다.

### 관절 시퀀스 컨트롤러 설정

프로젝트의 ROS2 폴더에 다음 스크립트를 추가하여 자동화된 Pick & Place 동작을 구현합니다.

```
Assets/Robots/AR4/ROS2/
└── joint_sequence_controller.py    # 자동화된 관절 시퀀스 스크립트
```

### 실행 방법

**1. 기본 ROS2 브릿지 실행 (터미널 1):**
```bash
cd /mnt/c/Users/user/Documents/Unity/My\ project\ \(1\)/Assets/Robots/AR4/ROS2/
python3 ros_tcp_endpoint_setup.py
```

**2. Unity Play 모드 시작**

**3. 자동 시퀀스 실행 (터미널 2):**
```bash
cd /mnt/c/Users/user/Documents/Unity/My\ project\ \(1\)/Assets/Robots/AR4/ROS2/
python3 joint_sequence_controller.py
```

### 사용 가능한 시퀀스

#### 1. Pick & Place 데모
```python
controller.demo_sequence()
```
- 홈 → 준비 → 픽업 접근 → 픽업 → 배치 접근 → 배치 → 홈 복귀
- 총 소요시간: 약 20초

#### 2. 웨이브 모션
```python
controller.wave_motion(duration=10.0)
```
- Joint 1을 사인파 형태로 연속 움직임
- 로봇이 손을 흔드는 듯한 동작

#### 3. 개별 포즈 이동
```python
controller.go_to_pose('ready')     # 준비 자세
controller.go_to_pose('home')      # 홈 포지션
```

#### 4. 부드러운 보간 움직임
```python
controller.smooth_move('home', 'ready', duration=3.0)
```
- 두 포즈 간 cubic interpolation으로 부드러운 전환
- 50단계로 나누어 자연스러운 움직임

### 커스텀 시퀀스 생성

새로운 포즈나 시퀀스를 추가하려면 `poses` 딕셔너리를 수정합니다:

```python
self.poses = {
    'custom_pose': [0.5, -0.3, 1.2, 0.2, 0.8, -0.1],  # 라디안 단위
    # ... 기존 포즈들
}

# 커스텀 시퀀스 실행
custom_sequence = ['home', 'custom_pose', 'ready', 'home']
controller.custom_sequence(custom_sequence, durations=[2.0, 3.0, 2.0, 2.0])
```

### 실시간 모니터링

WSL2에서 Unity의 관절 상태를 실시간으로 모니터링:

```bash
# Unity → ROS2 데이터 스트림 확인
ros2 topic echo /joint_states

# 관절 명령 로그 확인
ros2 topic echo /joint_command
```

### 응용 예시

이 자동화 시스템을 바탕으로 다음과 같은 고급 기능들을 구현할 수 있습니다:

- **산업 자동화 시뮬레이션**: 컨베이어 벨트 상의 물체 픽업
- **로봇 댄스**: 음악에 맞춘 관절 시퀀스
- **장애물 회피**: 동적 경로 계획 알고리즘 테스트
- **강화학습 환경**: Unity ML-Agents와 연동

> 💡 **팁**: `joint_sequence_controller.py`의 매개변수들(`movement_duration`, `interpolation_steps`)을 조정하여 로봇의 움직임 속도와 부드러움을 커스터마이즈할 수 있습니다.