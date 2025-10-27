using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerBallInteractor))]
public class KickPoseWarp : MonoBehaviour
{
    [Header("Target Offset")]
    public Vector3 contactOffset = new Vector3(0f, -0.02f, 0.05f);

    [Header("Curves")]
    [Tooltip("임팩트 전 (0=클립 시작, 1=임팩트 시점)")]
    public AnimationCurve preCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.1f),
        new Keyframe(0.6f, 0.2f, 0f, 2f),
        new Keyframe(1f, 1f, 3f, 0f)
    );

    [Tooltip("임팩트 후 (0=임팩트, 1=클립 끝)")]
    public AnimationCurve postCurve = new AnimationCurve(
        new Keyframe(0f, 1f, -4f, -2f),
        new Keyframe(0.2f, 0.3f, -2f, 0f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    [Range(0f, 1f)]
    public float peakWeight = 1f;

    [Header("Limb")]
    public AvatarIKGoal kickingFoot = AvatarIKGoal.RightFoot;
    public Vector3 kneeHintOffset = new Vector3(0.15f, -0.1f, 0.05f);

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.9f, 1f, 0.8f);

    [Header("Runtime (ReadOnly)")]
    [SerializeField] private bool inKick = false;
    [SerializeField] private bool contacted = false;
    [SerializeField] private float contactNorm = 0f;
    [SerializeField] private Vector3 targetPosRuntime;
    [SerializeField] private float wLegRuntime = 0f;

    private Animator anim;
    private PlayerBallInteractor interactor;

    // ---- 애니메이션 이벤트 ----
    public void OnKickEnter()
    {
        inKick = true;
        contacted = false;
        wLegRuntime = 0f;
    }

    public void OnKickContact()
    {
        if (!inKick) inKick = true;
        contacted = true;

        var info = anim.GetCurrentAnimatorStateInfo(0);
        float norm = info.normalizedTime % 1f;
        contactNorm = Mathf.Clamp01(norm);

        // 즉시 최대치 도달
        wLegRuntime = peakWeight;
    }

    public void OnKickExit()
    {
        contacted = false;
        inKick = false;
        wLegRuntime = 0f;
    }
    // ---------------------------

    private void Awake()
    {
        anim = GetComponent<Animator>();
        interactor = GetComponent<PlayerBallInteractor>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!inKick || interactor == null || interactor.BallTransform == null)
        {
            ZeroIK();
            return;
        }

        Vector3 targetRaw = interactor.BallTransform.position + contactOffset;
        targetPosRuntime = targetRaw;

        var info = anim.GetCurrentAnimatorStateInfo(layerIndex);
        float nowNorm = info.normalizedTime % 1f;
        float wLeg = 0f;

        if (!contacted)
        {
            // 임팩트 전
            float prePhase = Mathf.InverseLerp(0f, contactNorm, nowNorm);
            wLeg = preCurve.Evaluate(prePhase) * peakWeight;
        }
        else
        {
            // 임팩트 후
            float postPhase = Mathf.InverseLerp(contactNorm, 1f, nowNorm);
            wLeg = postCurve.Evaluate(postPhase) * peakWeight;
        }

        // 적용
        anim.SetIKRotationWeight(kickingFoot, 0f);
        anim.SetIKPositionWeight(kickingFoot, wLeg);
        anim.SetIKPosition(kickingFoot, targetRaw);

        AvatarIKHint hint = (kickingFoot == AvatarIKGoal.RightFoot) ? AvatarIKHint.RightKnee : AvatarIKHint.LeftKnee;
        Transform thigh = anim.GetBoneTransform(
            (kickingFoot == AvatarIKGoal.RightFoot) ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg);
        Vector3 kneeHint = (thigh ? thigh.position : transform.position) + transform.TransformDirection(kneeHintOffset);
        anim.SetIKHintPositionWeight(hint, wLeg);
        anim.SetIKHintPosition(hint, kneeHint);

        wLegRuntime = wLeg;
    }

    private void ZeroIK()
    {
        anim.SetIKPositionWeight(kickingFoot, 0f);
        anim.SetIKRotationWeight(kickingFoot, 0f);
        AvatarIKHint hint = (kickingFoot == AvatarIKGoal.RightFoot) ? AvatarIKHint.RightKnee : AvatarIKHint.LeftKnee;
        anim.SetIKHintPositionWeight(hint, 0f);
        wLegRuntime = 0f;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(targetPosRuntime, 0.03f);
    }
}
