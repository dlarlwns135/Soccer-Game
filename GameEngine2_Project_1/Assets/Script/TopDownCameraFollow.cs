using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    public enum CameraMode
    {
        Normal,
        Back
    }

    [Header("Mode")]
    public CameraMode mode = CameraMode.Normal;

    [Header("Target")]
    public Transform target;

    [Header("Normal Mode Settings")]
    public float normalXOffset = 5f;         // 타겟 기준 +X
    public float normalHeightOffset = 3f;    // 타겟 기준 +Y
    public float normalLookDownAngle = -15f; // X축으로 아래로 기울이는 각도

    [Header("Back Mode Settings")]
    public float backDistance = 5f;
    public float backHeight = 3f;
    public float backLookDownAngle = 15f;

    [Header("Follow Smoothness")]
    public float smoothTime = 0.15f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (!target) return;

        switch (mode)
        {
            case CameraMode.Normal:
                UpdateNormalMode();
                break;
            case CameraMode.Back:
                UpdateBackMode();
                break;
        }
    }

    // -----------------------
    // NORMAL MODE : 항상 Y=-90도, 타겟 기준 +X,+Y 위치에서 살짝 아래를 봄
    // -----------------------
    void UpdateNormalMode()
    {
        Vector3 desiredPosition = new Vector3(
            target.position.x + normalXOffset,           // +X 쪽으로 이동
            target.position.y + normalHeightOffset,      // 타겟 기준 위로
            target.position.z                            // Z는 동일
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime
        );

        // Y는 항상 -90도, X축으로는 살짝 아래를 보게
        transform.rotation = Quaternion.Euler(
            normalLookDownAngle,   // 위/아래 기울기
            -90f,                  // 항상 Y축 -90도
            0f
        );
    }

    // -----------------------
    // BACK MODE (기존 그대로)
    // -----------------------
    void UpdateBackMode()
    {
        Vector3 backPos =
            target.position
            - target.forward * backDistance
            + Vector3.up * backHeight;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            backPos,
            ref velocity,
            smoothTime
        );

        Quaternion lookRot = Quaternion.LookRotation(target.position - transform.position);
        Quaternion tiltRot = Quaternion.Euler(backLookDownAngle, lookRot.eulerAngles.y, 0f);

        transform.rotation = tiltRot;
    }
}
