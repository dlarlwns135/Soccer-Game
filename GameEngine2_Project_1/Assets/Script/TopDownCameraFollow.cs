using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;      // 따라갈 오브젝트

    [Header("Position")]
    public float height = 10.2f;  // 고정 Y 높이
    public Vector3 offset = new Vector3(0, 0, -4f); // 타겟 기준 Z축 오프셋

    [Header("Follow Smoothness")]
    public float smoothTime = 0.15f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (!target) return;

        // 타겟의 위치 기준으로 원하는 위치
        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            height,
            target.position.z + offset.z
        );

        // 부드럽게 이동
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        // X축을 70도로 고정, 나머지는 그대로
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }
}
