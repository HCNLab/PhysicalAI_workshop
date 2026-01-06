using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

public class RosImagePublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/camera/image_raw";
    public Camera targetCamera;
    public int resolutionWidth = 640;
    public int resolutionHeight = 480;
    public float publishRate = 10f; // 10Hz (너무 높으면 느려질 수 있음)

    private Texture2D texture2D;
    private RenderTexture renderTexture;
    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImageMsg>(topicName);

        // 텍스처 초기화
        texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24);
        targetCamera.targetTexture = renderTexture;
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > 1.0f / publishRate)
        {
            PublishImage();
            timeElapsed = 0;
        }
    }

    void PublishImage()
    {
        // 1. 카메라 렌더링
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        texture2D.Apply();

        // 2. 이미지 데이터 변환
        byte[] rawData = texture2D.GetRawTextureData();

        // 3. ROS 메시지 생성
        ImageMsg imgMsg = new ImageMsg();
        imgMsg.header.frame_id = "camera_link"; // TF 프레임 이름
        imgMsg.height = (uint)resolutionHeight;
        imgMsg.width = (uint)resolutionWidth;
        imgMsg.encoding = "rgb8";
        imgMsg.step = (uint)(resolutionWidth * 3);
        imgMsg.data = rawData;

        // 4. 전송
        ros.Publish(topicName, imgMsg);
    }
}