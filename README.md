# HRI Digital Twin Factory Setting

Unity VR 기반 Human-Robot Interaction (HRI) 실험을 위한 디지털 트윈 프로젝트입니다.

## 📋 프로젝트 개요

이 프로젝트는 VR 환경에서 사용자와 로봇 팔(AR4)의 상호작용을 연구하기 위한 실험 플랫폼입니다.

### 주요 기능
- 🏭 공장 환경 디지털 트윈
- 🤖 AR4 로봇 팔 실시간 제어 및 시각화
- 🧠 EEG 데이터 동기화 (LSL 마커 시스템)
- 🎮 VR 조립 작업 실험

## 🛠️ 기술 스택

| 구성요소 | 기술 |
|----------|------|
| 게임 엔진 | Unity 6.0+ (HDRP) |
| VR | Meta Quest (OpenXR) |
| 로봇 통신 | ROS2 + ROS-TCP-Connector |
| EEG 동기화 | Lab Streaming Layer (LSL) |

## 📁 프로젝트 구조

```
unity/
├── Assets/
│   ├── Scripts/           # C# 스크립트
│   │   ├── HRIExperimentManager.cs  # 실험 관리
│   │   ├── LSLManager.cs            # EEG 마커 전송
│   │   ├── AssemblyDetector.cs      # 조립 감지
│   │   ├── PressableObject.cs       # 누르기 감지
│   │   └── ROS.cs                   # ROS 통신
│   ├── UnityFactorySceneHDRP/       # 공장 씬 에셋
│   └── TextMesh Pro/                # UI 폰트
└── .gitattributes          # Git LFS 설정
```

## 🚀 시작하기

### 요구사항
- Unity 6.0 LTS 이상
- ROS2 Humble
- Meta Quest (VR 테스트용)

### 설치
```bash
git clone https://github.com/HCNLab/HRI-ditial-twin-factory-setting.git
cd HRI-ditial-twin-factory-setting
git lfs pull  # 대용량 파일 다운로드
```

### Unity 프로젝트 열기
1. Unity Hub에서 프로젝트 추가
2. Unity 6.0 버전으로 열기
3. `Scenes/Main` 씬 로드

## 📊 EEG 마커 코드

| 코드 | 이벤트 |
|------|--------|
| 1 | Trial Start |
| 11 | Assembly Complete |
| 21-22 | Robot Press (Cylinder/Cube) |
| 121-127 | Robot Error Actions |
| 41-42 | Feedback (Error/Correct) |

## 🔗 관련 저장소

- ROS2 워크스페이스: (별도 저장소)

## 📝 라이선스

HCN Lab - Internal Use

## 👥 기여자

- HCN Lab
