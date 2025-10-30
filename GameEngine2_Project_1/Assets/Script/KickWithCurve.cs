using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerBallInteractor))]
public class KickWithCurve : MonoBehaviour
{
    public FootCurveData footData;
    public AvatarIKGoal kickingFoot = AvatarIKGoal.RightFoot;
    public Vector3 contactOffset = new Vector3(0f, 0f, 0.05f);

    [Range(0, 1)] public float peakWeight = 1f;

    [Header("Approach Control")]
    public float riseSpeed = 3f;
    public float maxIKStep = 0.08f;

    [Header("Stabilize")]
    public float targetSmooth = 30f;
    public float maxTargetStep = 0.15f;
    public float maxAnkleRise = 0.10f;

    [Header("IK Activation (Ball Proximity)")]
    [Tooltip("이 거리 이내로 공이 들어오면 IK 활성")]
    public float enableRadius = 1.0f;
    [Tooltip("이 거리 밖으로 공이 나가면 IK 비활성(페이드아웃 시작)")]
    public float disableRadius = 1.2f;

    [Header("Knee Hint (optional)")]
    public Transform kneeHint;

    [Header("Failsafe: Forced Kick")]
    public bool useForcedKick = true;
    public float forceKickDistance = 0.09f;
    [Range(0f, 1f)] public float minWeightToForce = 0.55f;
    public float forceKickCooldown = 0.10f;
    public UnityEvent onForcedKick;

    [Header("Debug/Gizmos")]
    public bool drawDebug = true;
    public Color curveColor = Color.yellow;
    public Color targetColor = Color.red;
    public Color correctedColor = Color.cyan;
    public int curveSteps = 24;

    Animator anim;
    PlayerBallInteractor interactor;

    bool _hadBallPrev = false;
    bool _fadingOut = false;
    float _releaseStartNorm = 0f;
    float _prevNorm = 0f;

    float _riseWeight = 0f;

    Vector3 _smoothedTarget;
    bool _hasSmoothed = false;

    Vector3 _gizmoBaseFoot, _gizmoTarget, _gizmoCorrected;
    float _lastForceTime = -999f;

    // 공 근접 여부(히스테리시스)
    bool _nearBall = false;

    void Awake()
    {
        anim = GetComponent<Animator>();
        interactor = GetComponent<PlayerBallInteractor>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!footData) { ResetAll(); return; }

        var st = anim.GetCurrentAnimatorStateInfo(layerIndex);
        if (!st.IsTag("Action")) { ResetAll(); return; }

        float norm = st.normalizedTime % 1f;

        if (norm < _prevNorm)
        {
            _fadingOut = false;
            _hasSmoothed = false;
            _riseWeight = 0f;
        }
        _prevNorm = norm;

        Vector3 localFoot = new Vector3(
            footData.curveX.Evaluate(norm),
            footData.curveY.Evaluate(norm),
            footData.curveZ.Evaluate(norm)
        );
        Vector3 baseFoot = transform.TransformPoint(localFoot);
        _gizmoBaseFoot = baseFoot;

        // 공 타깃(없으면 IK를 켜지 않도록 '근접 판정'에서만 사용, target은 그래도 계산)
        bool hasBallTf = interactor.BallTransform != null;
        Vector3 baseContact = transform.TransformPoint(footData.contactPosition);
        Vector3 ballPos = hasBallTf ? interactor.BallTransform.position : baseContact;

        Vector3 rawTarget = ballPos + contactOffset;
        rawTarget.y = Mathf.Min(rawTarget.y, baseFoot.y + maxAnkleRise);

        float dt = Time.deltaTime;
        float aSmooth = 1f - Mathf.Exp(-targetSmooth * dt);
        if (!_hasSmoothed) { _smoothedTarget = rawTarget; _hasSmoothed = true; }
        else { _smoothedTarget = Vector3.Lerp(_smoothedTarget, rawTarget, aSmooth); }
        Vector3 target = Vector3.MoveTowards(_smoothedTarget, rawTarget, maxTargetStep);
        _gizmoTarget = target;

        // 공 근접 히스테리시스 업데이트(공 Transform 없으면 근접 false 유지)
        if (hasBallTf)
        {
            float dist = Vector3.Distance(baseFoot, ballPos);
            if (!_nearBall && dist <= enableRadius) _nearBall = true;
            else if (_nearBall && dist >= disableRadius)
            {
                _nearBall = false;
                BeginFadeOut(norm);
            }
        }
        else
        {
            if (_nearBall) { _nearBall = false; BeginFadeOut(norm); }
        }

        // HasBall 전환 감지(실제 킥 순간)
        bool kickJustHappened = (_hadBallPrev && !interactor.HasBall);
        _hadBallPrev = interactor.HasBall;
        if (kickJustHappened) BeginFadeOut(norm);

        // weight 계산
        float w = 0f;
        if (_nearBall && !_fadingOut)
        {
            _riseWeight = Mathf.MoveTowards(_riseWeight, peakWeight, riseSpeed * dt);
            w = _riseWeight;
        }
        else if (_fadingOut)
        {
            float denom = Mathf.Max(1e-5f, 1f - _releaseStartNorm);
            float t = Mathf.Clamp01((norm - _releaseStartNorm) / denom);
            t = t * t * (3f - 2f * t);
            w = Mathf.Lerp(peakWeight, 0f, t);
            _riseWeight = w;
        }
        else
        {
            // 공이 멀리 있거나 없음 → 즉시 0으로 수렴
            _riseWeight = Mathf.MoveTowards(_riseWeight, 0f, riseSpeed * dt);
            w = _riseWeight;
        }

        // IK 보정(프레임당 이동량 제한)
        Vector3 toTarget = target - baseFoot;
        Vector3 desired = baseFoot + toTarget * w;
        Vector3 corrected = Vector3.MoveTowards(baseFoot, desired, maxIKStep);
        _gizmoCorrected = corrected;

        // 강제 킥: 근접 상태에서만, 충분한 weight일 때만
        if (useForcedKick && _nearBall && !_fadingOut && _riseWeight >= minWeightToForce && hasBallTf)
        {
            float dist = Vector3.Distance(corrected, ballPos);
            if (dist <= forceKickDistance && (Time.time - _lastForceTime) >= forceKickCooldown)
            {
                onForcedKick?.Invoke();
                BeginFadeOut(norm);
                _lastForceTime = Time.time;
                Debug.Log($"[KickWithCurve] Forced kick at dist {dist:F3}m");
            }
        }

        anim.SetIKPositionWeight(kickingFoot, w);
        anim.SetIKPosition(kickingFoot, corrected);
        anim.SetIKRotationWeight(kickingFoot, 0f);

        if (kneeHint)
        {
            var hintType = (kickingFoot == AvatarIKGoal.RightFoot) ? AvatarIKHint.RightKnee : AvatarIKHint.LeftKnee;
            anim.SetIKHintPositionWeight(hintType, w);
            anim.SetIKHintPosition(hintType, kneeHint.position);
        }
    }

    void BeginFadeOut(float normNow)
    {
        _fadingOut = true;
        _releaseStartNorm = normNow;
    }

    void ResetAll()
    {
        _fadingOut = false;
        _hadBallPrev = false;
        _hasSmoothed = false;
        _prevNorm = 0f;
        _riseWeight = 0f;
        _nearBall = false;

        anim.SetIKPositionWeight(kickingFoot, 0f);
        anim.SetIKRotationWeight(kickingFoot, 0f);
    }

    void OnDisable() => ResetAll();

    void OnDrawGizmos()
    {
        if (!drawDebug || footData == null) return;

        Gizmos.color = curveColor;
        int steps = Mathf.Max(4, curveSteps);
        Vector3 prev = Vector3.zero;
        bool hasPrev = false;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 localP = new Vector3(
                footData.curveX.Evaluate(t),
                footData.curveY.Evaluate(t),
                footData.curveZ.Evaluate(t)
            );
            Vector3 worldP = transform.TransformPoint(localP);
            Gizmos.DrawSphere(worldP, 0.01f);
            if (hasPrev) Gizmos.DrawLine(prev, worldP);
            prev = worldP; hasPrev = true;
        }

        if (Application.isPlaying)
        {
            Gizmos.color = targetColor;
            Gizmos.DrawSphere(_gizmoTarget, 0.04f);

            Gizmos.color = correctedColor;
            Gizmos.DrawSphere(_gizmoCorrected, 0.03f);
            Gizmos.DrawLine(_gizmoBaseFoot, _gizmoCorrected);
        }
    }
}
