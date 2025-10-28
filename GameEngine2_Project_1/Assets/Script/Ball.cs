using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    private Rigidbody rb;

    public Transform Owner { get; private set; }
    public bool IsFree => Owner == null;

    public event Action<Transform> OnPossessionChanged;

    [Header("Rolling")]
    public float radius = 0.11f;
    public float rollThreshold = 0.05f;

    // Assist Follow (물리 보조로 Owner 앞을 따라감)
    [Header("Assist Follow")]
    public bool usePhysicsFollowWhenOwned = true;   // true면 소유 중에도 물리로 끌어당김(PD)
    public float assistK = 120f;                    // 스프링 K
    public float assistD = 20f;                     // 감쇠 D (<=0면 임계감쇠 자동)
    public float assistMaxAccel = 80f;              // 가속 클램프
    Transform assistTarget;                         // 보통 플레이어 transform
    Vector3 assistLocalOffset = new Vector3(0.5f, 0.0f, 0.5f); // (Right,Up,Forward)
    bool assistEnabled;

    // 타깃 속도에 따른 z 오프셋 동적 적용
    [Header("Assist Dynamic Z")]
    public float baseOffsetZ = 0.5f;
    public float speedToOffset = 0.12f;       // (m / (m/s)) : 속도 1m/s 당 추가 앞거리
    public float minOffsetZ = 0.40f;
    public float maxOffsetZ = 1.00f;
    public float offsetLerp = 12f;            // 오프셋 변화 보간 강도(초당)
    public float speedSmoothing = 10f;        // 속도 추정 저역 통과(초당)

    Vector3 _prevTargetPos;
    Vector3 _smoothedTargetVel;
    float _currentOffsetZ;

    // Legacy Follow (Kinematic) : 속도 비례 z 오프셋
    [Header("Legacy Follow (Kinematic)")]
    public float baseZ = 0.5f;
    public float rightX = 0.5f;
    public float speedToZ = 0.12f;
    public float minZ = 0.40f;
    public float maxZ = 1.00f;
    public float stopSpeed = 0.05f;
    public float zLerp = 12f;

    float _curZ;
    CharacterController _ownerCC;

    // Idle 미세 루트모션 억제(히스테리시스)
    [Header("Idle Suppress (Hysteresis)")]
    public float followStopSpeed = 0.55f;   // 이 이하 속도면 따라가기 중지(고정)
    public float followStartSpeed = 0.6f;   // 이 이상 속도면 따라가기 재개
    bool _followSuppressed = false;
    Vector3 _idleHoldPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 50f;
    }

    public void Kick(Vector3 direction, float force)
    {
        Release();
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    public Vector3 GetPosition() => transform.position;

    public void ResetPosition(Vector3 pos)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = pos;
        Release();
    }

    public void SetOwner(Transform newOwner)
    {
        if (Owner == newOwner) return;

        Owner = newOwner;
        OnPossessionChanged?.Invoke(Owner);

        bool kinematic = (Owner != null) && !usePhysicsFollowWhenOwned;
        rb.isKinematic = kinematic;

        if (rb.isKinematic)
            rb.angularVelocity = Vector3.zero;

        if (Owner == null)
        {
            DisableAssist();
            _ownerCC = null;
            _followSuppressed = false;
        }
        else
        {
            _ownerCC = Owner.GetComponent<CharacterController>();
            _followSuppressed = false;
        }
    }

    public void Release()
    {
        if (Owner == null) return;
        Owner = null;
        rb.isKinematic = false;
        DisableAssist();
        OnPossessionChanged?.Invoke(null);
        _ownerCC = null;
        _followSuppressed = false;
    }

    // ---- Assist API ----
    public void EnableAssist(Transform target, Vector3 localOffset)
    {
        assistTarget = target;
        assistLocalOffset = localOffset;

        _prevTargetPos = assistTarget.position;
        _smoothedTargetVel = Vector3.zero;
        _currentOffsetZ = assistLocalOffset.z;

        if (_ownerCC == null)
            _ownerCC = assistTarget.GetComponent<CharacterController>();

        assistEnabled = true;
    }

    public void DisableAssist()
    {
        assistEnabled = false;
        assistTarget = null;
    }

    void FixedUpdate()
    {
        // 소유 중
        if (Owner != null)
        {
            // 공통: 오너 수평 속도 + 히스테리시스 토글
            Vector3 ccVel = (_ownerCC != null) ? _ownerCC.velocity : Vector3.zero;
            float planarSpeed = new Vector3(ccVel.x, 0f, ccVel.z).magnitude;
            Debug.Log($"Ball FixedUpdate - planarSpeed: {planarSpeed}");

            if (_followSuppressed)
            {
                if (planarSpeed > followStartSpeed)
                    _followSuppressed = false;
            }
            else
            {
                if (planarSpeed < followStopSpeed)
                {
                    _followSuppressed = true;
                    _idleHoldPos = rb.position;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // PD Assist 모드
            if (usePhysicsFollowWhenOwned && assistEnabled && assistTarget && !rb.isKinematic)
            {
                if (_followSuppressed)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(_idleHoldPos.x, rb.position.y, _idleHoldPos.z));
                    return;
                }

                float dt = Time.fixedDeltaTime;

                Vector3 rawVel;
                if (_ownerCC != null)
                {
                    rawVel = ccVel;
                }
                else
                {
                    Vector3 targetPosNow = assistTarget.position;
                    rawVel = (targetPosNow - _prevTargetPos) / Mathf.Max(1e-6f, dt);
                    _prevTargetPos = targetPosNow;
                }

                float a = 1f - Mathf.Exp(-speedSmoothing * dt);
                _smoothedTargetVel = Vector3.Lerp(_smoothedTargetVel, rawVel, a);
                float targetSpeed = new Vector3(_smoothedTargetVel.x, 0f, _smoothedTargetVel.z).magnitude;

                float targetZ = Mathf.Clamp(baseOffsetZ + speedToOffset * targetSpeed, minOffsetZ, maxOffsetZ);
                float b = 1f - Mathf.Exp(-offsetLerp * dt);
                _currentOffsetZ = Mathf.Lerp(_currentOffsetZ, targetZ, b);

                Vector3 dynOffset = assistLocalOffset;
                dynOffset.z = _currentOffsetZ;

                Vector3 targetPos = assistTarget.TransformPoint(dynOffset);

                Vector3 posErr = targetPos - rb.position;
                Vector3 flatErr = Vector3.ProjectOnPlane(posErr, Vector3.up);

                const float holdRadius = 0.05f;
                const float holdVel = 0.05f;
                if (flatErr.sqrMagnitude <= holdRadius * holdRadius && rb.linearVelocity.sqrMagnitude < holdVel * holdVel)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(targetPos.x, rb.position.y, targetPos.z));
                    return;
                }

                float m = Mathf.Max(0.0001f, rb.mass);
                float K = assistK;
                float D = (assistD <= 0f) ? (2f * Mathf.Sqrt(K * m)) : assistD;

                Vector3 vd = new Vector3(_smoothedTargetVel.x, 0f, _smoothedTargetVel.z);
                Vector3 velErr = vd - rb.linearVelocity;

                Vector3 accel = (K * flatErr) + (D * velErr);
                accel.y = 0f;

                accel = Vector3.ClampMagnitude(accel, assistMaxAccel);
                rb.AddForce(accel, ForceMode.Acceleration);

                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                // 레거시 스냅 팔로우
                if (_followSuppressed)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(_idleHoldPos.x, rb.position.y, _idleHoldPos.z));
                    return;
                }

                if (_ownerCC == null) _ownerCC = Owner.GetComponent<CharacterController>();

                Vector3 vel = (_ownerCC != null) ? _ownerCC.velocity : Vector3.zero;
                float spd = new Vector3(vel.x, 0f, vel.z).magnitude;

                if (spd < stopSpeed)
                {
                    Vector3 targetLocal = new Vector3(rightX, 0f, baseZ);
                    Vector3 targetWorld = Owner.TransformPoint(targetLocal);

                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(targetWorld.x, rb.position.y, targetWorld.z));
                }
                else
                {
                    float targetZ = Mathf.Clamp(baseZ + speedToZ * spd, minZ, maxZ);
                    float t = 1f - Mathf.Exp(-zLerp * Time.fixedDeltaTime);
                    _curZ = Mathf.Lerp(_curZ, targetZ, t);

                    Vector3 targetLocal = new Vector3(rightX, 0f, _curZ);
                    Vector3 targetWorld = Owner.TransformPoint(targetLocal);

                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(targetWorld.x, rb.position.y, targetWorld.z));
                }
            }
            return;
        }

        // 자유 상태: 실제처럼 굴러가기
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed > rollThreshold && radius > 1e-4f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, v).normalized;
            rb.angularVelocity = axis * (speed / radius);
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
        }
    }
}
