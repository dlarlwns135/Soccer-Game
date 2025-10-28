using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerBallInteractor))]
public class KickWithCurve : MonoBehaviour
{
    public FootCurveData footData;
    public AvatarIKGoal kickingFoot = AvatarIKGoal.RightFoot;
    public Vector3 contactOffset = new Vector3(0f, -0.02f, 0.05f);

    [Range(0, 1)] public float peakWeight = 1f;

    [Header("Timing")]
    [Tooltip("공에 실제로 닿는 정규화 시점 (0~1)")]
    public float contactNorm = 0.31f;

    [SerializeField, Range(0f, 1f)]
    float releaseNorm = 0.40f;

    [Header("Latch")]
    [Tooltip("contactNorm - 이 값 시점에서 공 위치를 샘플링해 고정")]
    [Range(0.0f, 0.2f)]
    public float preContactSampleOffset = 0.01f;   // 예: contactNorm이 0.31이면 0.30 시점에서 고정
    [Tooltip("releaseNorm을 지난 뒤 이 시간만큼 지나면 래치 해제(루프 대비)")]
    public float unlatchDelay = 0.05f;

    private Animator anim;
    private PlayerBallInteractor interactor;

    // Latch 상태
    bool _latched = false;
    Vector3 _latchedBallPos;
    float _prevNorm = 0f;
    float _unlatchAt = -1f;

    [Header("Debug")]
    public bool drawDebug = true;
    public Color curveColor = Color.yellow;
    public Color targetColor = Color.red;
    [SerializeField] private float weightDebug;

    void Awake()
    {
        anim = GetComponent<Animator>();
        interactor = GetComponent<PlayerBallInteractor>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!footData) { ResetLatch(); return; }

        var st = anim.GetCurrentAnimatorStateInfo(layerIndex);
        if (!st.IsTag("Action")) { ResetLatch(); return; }

        float norm = st.normalizedTime % 1f;

        // 킥 윈도우(래치 유지 용도)
        const float postWindow = 0.08f;
        bool inKickWindow = norm <= (releaseNorm + postWindow);

        // 루프 감지
        if (norm < _prevNorm) ResetLatch();
        _prevNorm = norm;

        // 래치 시점
        float sampleNorm = Mathf.Clamp01(contactNorm - preContactSampleOffset);

        // 킥 윈도우가 끝나면 래치 해제
        if (_latched && norm > (releaseNorm + postWindow))
            ResetLatch();

        // 공 위치 래치(한 번만)
        if (!_latched && interactor.HasBall && norm >= sampleNorm && norm <= contactNorm)
        {
            if (interactor.BallTransform)
            {
                _latchedBallPos = interactor.BallTransform.position;
                _latched = true;
            }
        }

        // ====== 여기부터 "IK 적용 최소 조건"을 강화 ======
        // 공을 안 들고 있고, 래치도 없으면 -> IK 아예 중단
        if (!interactor.HasBall && !_latched)
        {
            // 안전: weight 0으로 명시해 두면 더 깔끔
            anim.SetIKPositionWeight(kickingFoot, 0f);
            anim.SetIKRotationWeight(kickingFoot, 0f);
            return;
        }

        // 베이크 발/컨택트 위치
        Vector3 localFoot = new Vector3(
            footData.curveX.Evaluate(norm),
            footData.curveY.Evaluate(norm),
            footData.curveZ.Evaluate(norm)
        );
        Vector3 baseFoot = transform.TransformPoint(localFoot);
        Vector3 baseContact = transform.TransformPoint(footData.contactPosition);

        // 타깃 공 위치(래치 우선)
        Vector3 ballPosForIK = (_latched && norm >= sampleNorm)
            ? _latchedBallPos
            : (interactor.BallTransform ? interactor.BallTransform.position : baseContact);

        Vector3 target = ballPosForIK + contactOffset;
        Vector3 delta = target - baseContact;

        // --- 보정량 안전장치 ---
        // 1) 과도한 보정량 클램프(예: 20cm)
        float maxCorrection = 0.20f;
        if (delta.magnitude > maxCorrection)
            delta = delta.normalized * maxCorrection;

        // 2) 발-타깃이 너무 멀면 weight를 강제로 줄이기 (예: 40cm 이상이면 감소)
        float targetDist = Vector3.Distance(baseFoot, target);
        float farStart = 0.40f, farEnd = 0.60f; // 40~60cm 사이에서 선형 감쇠
        float farFactor = 1f - Mathf.InverseLerp(farStart, farEnd, targetDist);
        farFactor = Mathf.Clamp01(farFactor);

        // --- weight 곡선(홀드 + 이즈아웃) ---
        float w = 0f;
        const float holdAfterContact = 0.06f;
        float holdEnd = Mathf.Min(contactNorm + holdAfterContact, releaseNorm);

        if (norm < contactNorm)
        {
            w = Mathf.InverseLerp(0f, contactNorm, norm) * peakWeight;
        }
        else if (norm <= holdEnd)
        {
            w = peakWeight;
        }
        else if (norm <= releaseNorm)
        {
            float t = Mathf.InverseLerp(holdEnd, releaseNorm, norm);
            t = t * t * (3f - 2f * t); // smoothstep
            w = Mathf.Lerp(peakWeight, 0f, t);
        }
        else
        {
            w = 0f;
        }

        // 멀면 감쇠
        w *= farFactor;

        // 최종 보정 적용
        Vector3 corrected = baseFoot + delta * w;

        anim.SetIKPositionWeight(kickingFoot, w);
        anim.SetIKPosition(kickingFoot, corrected);
        anim.SetIKRotationWeight(kickingFoot, 0f);
        weightDebug = w;
    }

    void ResetLatch()
    {
        _latched = false;
        _unlatchAt = -1f;
        _prevNorm = 0f;
    }

    void OnDisable() => ResetLatch();

    private void OnDrawGizmos()
    {
        if (!drawDebug || footData == null) return;

        Gizmos.color = curveColor;
        int steps = 20;
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

            prev = worldP;
            hasPrev = true;
        }

        if (interactor != null && interactor.BallTransform != null)
        {
            Gizmos.color = targetColor;
            // 현재 프레임에 IK가 참조하는 좌표를 시각화(래치 여부 반영)
            // 실행 중엔 OnDrawGizmos 시점에 래치 정보가 없을 수 있어 단순히 현재 공 위치를 그림
            Gizmos.DrawSphere(interactor.BallTransform.position + contactOffset, 0.05f);
        }
    }
}
