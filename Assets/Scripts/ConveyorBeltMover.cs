using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(BoxCollider))]
public class ConveyorBeltMover : MonoBehaviour
{
    [Header("Belt Direction & Speed")]
    public Vector3 beltDirection = Vector3.right;
    public float beltSpeed = 1.5f;
    public float stickiness = 1.0f;

    [Header("Control")]
    public bool isRunning = true;  // Can be toggled to stop/start belt

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isRunning) return;  // Skip if belt is stopped

        Rigidbody rb = other.attachedRigidbody;

        if (rb == null || rb.isKinematic)
            return;

        // Skip if object is being grabbed or hovered
        var grabbable = rb.GetComponent<Grabbable>();
        if (grabbable != null && grabbable.SelectingPointsCount > 0)
            return;

        // 벨트의 월드 방향 기준 속도
        Vector3 targetVelocity = transform.TransformDirection(beltDirection.normalized) * beltSpeed;

        // 현재 속도에서 벨트 진행 방향 성분만 추출
        Vector3 currentAlongBelt = Vector3.Project(rb.linearVelocity, transform.TransformDirection(beltDirection.normalized));

        // 목표 속도와의 차이를 보정
        Vector3 delta = targetVelocity - currentAlongBelt;

        // VelocityChange = 질량 무시하고 즉시 가속
        rb.AddForce(delta * stickiness, ForceMode.VelocityChange);

        // Debug.Log($"[ConveyorBelt] Moving {other.name}, velocity: {rb.linearVelocity}, delta: {delta}");
    }
}
