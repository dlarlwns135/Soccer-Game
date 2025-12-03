using UnityEngine;
using UnityEngine.VFX;
using System;
using System.Collections;
using System.Collections.Generic;

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
    public float baseOffsetZ = 0.2f;
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

    // ===== Debug =====
    [Header("Debug")]
    public bool debugOffsets = false;
    public float debugEvery = 0.20f;   // 초
    float _nextDbgTime = 0f;

    // 디버그용 캐시값 (Gizmos)
    Vector3 _dbgTargetWorld;    // 목표 위치(assist/legacy에 따라)
    Vector3 _dbgDesiredWorld;   // 계산된 이상적 목표(예: PD targetPos)
    Vector3 _dbgOffsetWorld;    // Owner 기준 로컬 오프셋의 월드 결과
    bool _dbgValid = false;

    [Header("FX")]
    [SerializeField] Transform fxRoot;                 // 빈 오브젝트(자식에 파티클 4개가 달려있음)
    List<ParticleSystem> fxList = new List<ParticleSystem>();

    // ===== VFX Graph (dissolve/flight) =====
    [Header("Disappear VFX (VFX Graph)")]
    [SerializeField] private VisualEffect disappearVFX;   // 그래프 레퍼런스
    [SerializeField] private bool useSpawnRateParam = true;
    [SerializeField] private string spawnRateParam = "SpawnRate";
    [SerializeField] private float hideDelay = 0.15f;     // 그래프 시작 후 공 숨기기까지 지연
    [SerializeField] private float spawnDuration = 0.6f;  // 생성 유지 시간
    [SerializeField] private Renderer[] renderersToToggle; // 비워두면 자동 수집
    [SerializeField] private float effectDuration = 5f;
    [SerializeField] private float followSyncDuration = 2.5f; // 시작 후 몇 초 동안만 추적
    private float disappearStartTime;

    bool isDisappearing;
    int spawnRateID;

    // === 골키퍼 전용 ===
    [Header("Goalkeeper Hold")]
    [SerializeField] private Transform keeperLeftHand;
    [SerializeField] private Transform keeperRightHand;
    bool ownedByKeeper = false;

    public void SetKeeperOwned(bool owned)
    {
        ownedByKeeper = owned;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 50f;
        CacheFx();

        if (renderersToToggle == null || renderersToToggle.Length == 0)
            renderersToToggle = GetComponentsInChildren<Renderer>(true);

        if (!string.IsNullOrEmpty(spawnRateParam))
            spawnRateID = Shader.PropertyToID(spawnRateParam);
    }

    void Update()
    {
        // 테스트 트리거
        if (Input.GetKeyDown(KeyCode.T))
            TriggerDisappear();
    }

    void LateUpdate()
    {
        if (!disappearVFX) return;

        // 사라지는 중에만 따라가게 하려면 조건 유지
        if (isDisappearing)
        {
            if (Time.time - disappearStartTime <= followSyncDuration)
            {
                var t = disappearVFX.transform;
                // 공 기준 로컬 오프셋을 월드로 변환
                Vector3 pos = transform.TransformPoint(Vector3.zero);
                t.SetPositionAndRotation(pos, transform.rotation);
            }
        }
    }

    void CacheFx()
    {
        fxList.Clear();
        if (!fxRoot) return;
        fxList.AddRange(fxRoot.GetComponentsInChildren<ParticleSystem>(true));
    }

    public void TriggerDisappear()
    {
        if (!disappearVFX || isDisappearing) return;
        StartCoroutine(CoDisappear());
    }

    IEnumerator CoDisappear()
    {
        isDisappearing = true;
        disappearStartTime = Time.time;

        // VFX를 공 위치/회전으로 맞춤
        var vfxTr = disappearVFX.transform;
        vfxTr.SetPositionAndRotation(transform.position, transform.rotation);

        disappearVFX.Reinit();
        disappearVFX.Play();

        if (useSpawnRateParam && disappearVFX.HasFloat(spawnRateID))
            disappearVFX.SetFloat(spawnRateID, 1f);

        float elapsed = 0f;

        // 1) 일정 시간 후 공 숨기기
        if (hideDelay > 0f)
        {
            while (elapsed < hideDelay) { elapsed += Time.deltaTime; yield return null; }
        }
        SetBallVisible(false);

        // 2) 스폰 유지 후 끄기
        if (spawnDuration > 0f)
        {
            float target = elapsed + spawnDuration;
            while (elapsed < target) { elapsed += Time.deltaTime; yield return null; }
        }

        if (useSpawnRateParam && disappearVFX.HasFloat(spawnRateID))
            disappearVFX.SetFloat(spawnRateID, 0f);

        // 3) 총 5초(effectDuration)까지 대기
        while (elapsed < effectDuration) { elapsed += Time.deltaTime; yield return null; }

        // 마무리
        SetBallVisible(true);
        isDisappearing = false;
        disappearVFX.Stop();
    }


    void SetBallVisible(bool visible)
    {
        if (renderersToToggle == null) return;
        for (int i = 0; i < renderersToToggle.Length; ++i)
        {
            var r = renderersToToggle[i];
            if (r) r.enabled = visible;
        }
    }

    void PlayKickFX(Vector3 pos, Vector3 dir)
    {
        if (fxList == null || fxList.Count == 0) return;

        // 방향 없으면 현재 전방 사용
        Vector3 fwd = (dir.sqrMagnitude > 1e-6f) ? dir.normalized : transform.forward;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        foreach (var ps in fxList)
        {
            if (!ps) continue;

            // 파티클 위치/회전 동기화
            var t = ps.transform;
            t.position = pos;
            t.rotation = rot;

            // 잔여 입자 제거 후 재생
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    public void Kick(Vector3 direction, float force)
    {
        Release();
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        PlayKickFX(transform.position, direction);
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

        if (debugOffsets)
        {
            Debug.Log($"[Ball] SetOwner: owner={(Owner ? Owner.name : "null")}, usePhysicsFollowWhenOwned={usePhysicsFollowWhenOwned}, kinematic={rb.isKinematic}");
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
        ownedByKeeper = false;   // 추가

        if (debugOffsets)
        {
            Debug.Log("[Ball] Release: owner cleared, assist disabled");
        }
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

        if (debugOffsets)
        {
            Debug.Log($"[Ball] EnableAssist: target={assistTarget.name}, localOffset={assistLocalOffset}, baseOffsetZ={baseOffsetZ}, speedToOffset={speedToOffset}");
        }
    }

    public void DisableAssist()
    {
        assistEnabled = false;
        assistTarget = null;
        if (debugOffsets) Debug.Log("[Ball] DisableAssist");
    }

    void FixedUpdate()
    {
        _dbgValid = false;

        // 소유 중
        if (Owner != null)
        {
            if (ownedByKeeper && keeperLeftHand != null && keeperRightHand != null)
            {
                Vector3 lh = keeperLeftHand.position;
                Vector3 rh = keeperRightHand.position;
                Vector3 mid = (lh + rh) * 0.5f;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.MovePosition(mid);   // 또는 transform.position = mid;

                _dbgTargetWorld = mid;
                _dbgDesiredWorld = mid;
                _dbgOffsetWorld = mid;
                _dbgValid = true;

                return; // 아래 Assist/Legacy 로직은 건너뜀
            }

            // 공통: 오너 수평 속도 + 히스테리시스 토글
            Vector3 ccVel = (_ownerCC != null) ? _ownerCC.velocity : Vector3.zero;
            float planarSpeed = new Vector3(ccVel.x, 0f, ccVel.z).magnitude;

            bool prevSuppressed = _followSuppressed;
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
            if (debugOffsets && prevSuppressed != _followSuppressed)
            {
                Debug.Log($"[Ball] Suppress toggle: {_followSuppressed} (planarSpeed={planarSpeed:F2})");
            }

            // PD Assist 모드
            if (usePhysicsFollowWhenOwned && assistEnabled && assistTarget && !rb.isKinematic)
            {
                if (_followSuppressed)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(_idleHoldPos.x, rb.position.y, _idleHoldPos.z));

                    _dbgTargetWorld = _idleHoldPos;
                    _dbgDesiredWorld = _idleHoldPos;
                    _dbgOffsetWorld = _idleHoldPos;
                    _dbgValid = true;

                    TryLogPD(planarSpeed, Vector3.zero, Vector3.zero, _idleHoldPos, _idleHoldPos, true);
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

                    _dbgTargetWorld = new Vector3(targetPos.x, rb.position.y, targetPos.z);
                    _dbgDesiredWorld = targetPos;
                    _dbgOffsetWorld = assistTarget.TransformPoint(assistLocalOffset);
                    _dbgValid = true;

                    TryLogPD(planarSpeed, _smoothedTargetVel, flatErr, targetPos, _dbgTargetWorld, false);
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

                _dbgTargetWorld = assistTarget.TransformPoint(new Vector3(assistLocalOffset.x, assistLocalOffset.y, _currentOffsetZ));
                _dbgDesiredWorld = assistTarget.TransformPoint(dynOffset);
                _dbgOffsetWorld = _dbgTargetWorld;
                _dbgValid = true;

                TryLogPD(planarSpeed, _smoothedTargetVel, flatErr, _dbgDesiredWorld, _dbgTargetWorld, false);
            }
            else
            {
                // 레거시 스냅 팔로우
                if (_followSuppressed)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(new Vector3(_idleHoldPos.x, rb.position.y, _idleHoldPos.z));

                    _dbgTargetWorld = _idleHoldPos;
                    _dbgDesiredWorld = _idleHoldPos;
                    _dbgOffsetWorld = _idleHoldPos;
                    _dbgValid = true;

                    TryLogLegacy(planarSpeed, 0f, _idleHoldPos);
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

                    _dbgTargetWorld = targetWorld;
                    _dbgDesiredWorld = targetWorld;
                    _dbgOffsetWorld = targetWorld;
                    _dbgValid = true;

                    TryLogLegacy(spd, baseZ, targetWorld);
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

                    _dbgTargetWorld = targetWorld;
                    _dbgDesiredWorld = targetWorld;
                    _dbgOffsetWorld = targetWorld;
                    _dbgValid = true;

                    TryLogLegacy(spd, _curZ, targetWorld);
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

    // ===== Debug Helpers =====
    void TryLogPD(float planarSpeed, Vector3 smVel, Vector3 flatErr, Vector3 desiredTarget, Vector3 snapTarget, bool holding)
    {
        //if (!debugOffsets || Time.time < _nextDbgTime) return;
        //_nextDbgTime = Time.time + debugEvery;

        //string holdStr = holding ? "HOLD" : "FOLLOW";
        //Debug.Log(
        //    $"[Ball/PD-{holdStr}] owner={(Owner ? Owner.name : "null")}, assist={assistEnabled}, " +
        //    $"localOffset={assistLocalOffset}, baseOffsetZ={baseOffsetZ:F2}, curZ={_currentOffsetZ:F2}, " +
        //    $"speedToOffset={speedToOffset:F2}, planarSpeed={planarSpeed:F2}, smVel=({smVel.x:F2},{smVel.z:F2}), " +
        //    $"flatErr=({flatErr.x:F2},{flatErr.z:F2})m, desiredTarget=({desiredTarget.x:F2},{desiredTarget.z:F2}), " +
        //    $"snapTarget=({snapTarget.x:F2},{snapTarget.z:F2}), K={assistK:F1}, D={(assistD <= 0f ? -1f : assistD):F1}"
        //);
    }

    void TryLogLegacy(float speed, float curZOrBaseZ, Vector3 targetWorld)
    {
        if (!debugOffsets || Time.time < _nextDbgTime) return;
        _nextDbgTime = Time.time + debugEvery;

        Debug.Log(
            $"[Ball/Legacy] owner={(Owner ? Owner.name : "null")}, rightX={rightX:F2}, baseZ={baseZ:F2}, " +
            $"speedToZ={speedToZ:F2}, curZ={curZOrBaseZ:F2}, speed={speed:F2}, target=({targetWorld.x:F2},{targetWorld.z:F2})"
        );
    }

    void OnDrawGizmosSelected()
    {
        if (!_dbgValid || Owner == null) return;

        // 타깃 지점
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_dbgTargetWorld, 0.06f);

        // 이상적 목표(특히 PD에서 dynOffset 적용된 위치)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_dbgDesiredWorld, 0.05f);

        // Owner 기준 로컬 오프셋 결과 위치
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(_dbgOffsetWorld, 0.05f);

        // 선으로 관계 표시
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, _dbgTargetWorld);   // 공 → 타깃

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawLine(Owner.position, _dbgTargetWorld);       // Owner → 타깃

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawLine(_dbgOffsetWorld, _dbgDesiredWorld);     // 오프셋 결과 ↔ 이상적 목표
    }
}
