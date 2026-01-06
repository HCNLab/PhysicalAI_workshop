using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    public float lookSpeed = 2.0f;
    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 2.0f;

    private float _rotationX = 0;
    private float _rotationY = 0;

    void Start()
    {
        // 초기 회전값 가져오기
        Vector3 rot = transform.localEulerAngles;
        _rotationX = rot.x;
        _rotationY = rot.y;
    }

    void Update()
    {
        // 우클릭 상태에서만 회전
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            _rotationY += mouseX;
            _rotationX -= mouseY;
            _rotationX = Mathf.Clamp(_rotationX, -90, 90);

            transform.localRotation = Quaternion.Euler(_rotationX, _rotationY, 0);

            // 이동 (WASD + QE)
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1.0f);
            float moveX = Input.GetAxis("Horizontal") * speed * Time.deltaTime; // A, D
            float moveZ = Input.GetAxis("Vertical") * speed * Time.deltaTime;   // W, S
            float moveY = 0;

            if (Input.GetKey(KeyCode.E)) moveY = speed * Time.deltaTime; // 위
            if (Input.GetKey(KeyCode.Q)) moveY = -speed * Time.deltaTime; // 아래

            transform.Translate(moveX, moveY, moveZ);
        }
    }
}
