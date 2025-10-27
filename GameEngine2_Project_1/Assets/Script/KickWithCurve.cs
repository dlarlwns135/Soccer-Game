using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerBallInteractor))]
public class KickWithCurve : MonoBehaviour
{
    public FootCurveData footData; // 베이크된 발 위치 데이터
    public AvatarIKGoal kickingFoot = AvatarIKGoal.RightFoot;
    public Vector3 contactOffset = new Vector3(0f, -0.02f, 0.05f);

    [Range(0, 1)] public float peakWeight = 1f;
    public float contactNorm = 0.31f; // 공에 닿는 시점 (normalizedTime)

    [SerializeField, Range(0f, 1f)]
    float releaseNorm = 0.4f;

    private Animator anim;
    private PlayerBallInteractor interactor;

    [Header("Debug")]
    public bool drawDebug = true;
    public Color curveColor = Color.yellow;
    public Color targetColor = Color.red;

    // ===== 디버그용 Inspector 노출 =====
    [SerializeField] private Transform thighBoneRef;   // 허벅지 본 Transform 캐싱
    [SerializeField] private Vector3 hipToFootDebug;   // 허벅지→발 벡터
    [SerializeField] private Vector3 hipToBallDebug;   // 허벅지→공 벡터
    [SerializeField] private float weightDebug;        // 현재 w 값
    [SerializeField] private Quaternion deltaDebug;    // FromToRotation 결과

    void Awake()
    {
        anim = GetComponent<Animator>();
        interactor = GetComponent<PlayerBallInteractor>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (footData == null || !interactor.HasBall) return;
        if (!anim.GetCurrentAnimatorStateInfo(layerIndex).IsTag("Action")) return;

        float normTime = anim.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime % 1f;

        // 원래 커브 발 위치
        Vector3 localFoot = new Vector3(
            footData.curveX.Evaluate(normTime),
            footData.curveY.Evaluate(normTime),
            footData.curveZ.Evaluate(normTime)
        );
        Vector3 baseFoot = transform.TransformPoint(localFoot);

        // 접촉 지점 차이
        Vector3 baseContact = transform.TransformPoint(footData.contactPosition);
        Vector3 target = interactor.BallTransform.position + contactOffset;
        Vector3 delta = target - baseContact;

        float w = 0f;
        if (normTime < footData.contactNorm)
        {
            // 접촉 전 : 0 → 1
            w = Mathf.InverseLerp(0f, footData.contactNorm, normTime) * peakWeight;
        }
        else if (normTime <= releaseNorm)
        {
            // 접촉 직후 : 1 → 0
            w = Mathf.Lerp(peakWeight, 0f, Mathf.InverseLerp(footData.contactNorm, releaseNorm, normTime));
        }
        else
        {
            // releaseNorm 이후 : 원래 커브 100%
            w = 0f;
        }

        Vector3 corrected = baseFoot + delta * w;

        anim.SetIKPositionWeight(kickingFoot, w);
        anim.SetIKPosition(kickingFoot, corrected);
        anim.SetIKRotationWeight(kickingFoot, 0f);

        weightDebug = w;
    }


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
            Gizmos.DrawSphere(interactor.BallTransform.position + contactOffset, 0.05f);
        }
    }
}
