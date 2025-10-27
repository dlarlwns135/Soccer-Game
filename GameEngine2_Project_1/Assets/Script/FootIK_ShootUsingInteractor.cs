using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerBallInteractor))]
public class FootIK_ShootUsingInteractor : MonoBehaviour
{
    [Header("Foot")]
    public HumanBodyBones kickingFoot = HumanBodyBones.RightFoot;

    [Header("Targeting")]
    public float ballRadius = 0.11f;              // 축구공 반지름(m)
    public float contactOffset = 0.05f;           // 공 표면에서 발 위치까지 여유
    public Vector3 extraOffset = new Vector3(0f, -0.02f, 0f); // 미세 보정

    [Header("Weights")]
    [Range(0, 1)] public float ikPosWeight = 1f;
    [Range(0, 1)] public float ikRotWeight = 1f;

    [Header("Action Tag")]
    public string actionTag = "Action";           // 슈팅 상태(클립)에 태그

    [Header("Impact Window (Animation Event)")]
    public AnimationCurve impactWeight = AnimationCurve.EaseInOut(0, 0, 1, 1); // 0→1 커브
    public float defaultDuration = 0.12f;         // KickIKOn()에 인자 미전달 시
    public float blendIn = 0.04f;                 // 시작 페이드(초)
    public float blendOut = 0.06f;                // 끝 페이드(초)

    [Header("Debug / Override")]
    public bool forceIK = false;                  // 강제 IK 테스트용
    public Transform debugTarget;                 // 강제 대상(없으면 Ball)

    // ---- 인스펙터에서 보기 위한 런타임 상태 ----
    [Header("Debug (ReadOnly)")]
    [SerializeField] bool inAction;               // 태그 상태인지
    [SerializeField] bool hasBall;                // 공 소유 중인지
    [SerializeField] bool ikActive;               // 임팩트 윈도우 활성화 중인지
    [SerializeField] float ikTimer;               // 남은 시간
    [SerializeField] float ikDuration;            // 총 지속 시간
    [SerializeField] float ikT01;                 // 0~1 정규화 진행도
    [SerializeField] Vector3 targetPos;           // IK 목표 위치
    [SerializeField] Quaternion targetRot;        // IK 목표 회전

    Animator anim;
    PlayerBallInteractor interactor;
    int tagHash;

    void Awake()
    {
        anim = GetComponent<Animator>();
        interactor = GetComponent<PlayerBallInteractor>();
        tagHash = Animator.StringToHash(actionTag);
    }

    bool InAction()
    {
        var s = anim.GetCurrentAnimatorStateInfo(0);
        if (s.tagHash == tagHash) return true;
        if (anim.IsInTransition(0))
        {
            var n = anim.GetNextAnimatorStateInfo(0);
            if (n.tagHash == tagHash) return true;
        }
        return false;
    }

    void Update()
    {
        inAction = InAction();
        hasBall = interactor && interactor.HasBall;

        // 임팩트 윈도우 타이머
        if (ikActive)
        {
            ikTimer -= Time.deltaTime;
            if (ikTimer <= 0f)
            {
                ikActive = false;
                ikTimer = 0f;
            }
        }
        ikT01 = ikDuration > 0f ? 1f - Mathf.Clamp01(ikTimer / ikDuration) : 0f;
    }

    void OnAnimatorIK(int layerIndex)
    {
        var goal = GetGoal();

        // 강제 테스트 모드 (세팅 문제 분리 확인용)
        if (forceIK)
        {
            Transform t = debugTarget ? debugTarget : (interactor ? interactor.BallTransform : null);
            if (!t) { Zero(goal); return; }

            anim.SetIKPositionWeight(goal, ikPosWeight);
            anim.SetIKRotationWeight(goal, ikRotWeight);
            anim.SetIKPosition(goal, t.position);
            anim.SetIKRotation(goal, Quaternion.LookRotation((t.position - transform.position).normalized, Vector3.up));
            return;
        }

        // 기본 조건: 액션 상태 + 공 소유 + Ball 참조 + 임팩트 윈도우 활성
        if (!inAction || !hasBall || interactor.BallTransform == null || !ikActive)
        {
            Zero(goal);
            return;
        }

        // 가중치(0~1) 계산: 커브 * 페이드 인/아웃
        float w = impactWeight.Evaluate(ikT01);
        if (blendIn > 0 && ikT01 < blendIn) w *= (ikT01 / blendIn);
        if (blendOut > 0 && ikT01 > 1f - blendOut) w *= ((1f - ikT01) / blendOut);
        w = Mathf.Clamp01(w);

        // 목표 계산
        var footPos = anim.GetBoneTransform(
            kickingFoot == HumanBodyBones.RightFoot ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot
        ).position;

        Vector3 ballPos = interactor.BallTransform.position;
        Vector3 dir = (ballPos - footPos);
        if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
        dir.Normalize();

        targetPos = ballPos - dir * (ballRadius + contactOffset) + extraOffset;
        targetRot = Quaternion.LookRotation(dir, Vector3.up);

        // 적용
        anim.SetIKPositionWeight(goal, w * ikPosWeight);
        anim.SetIKRotationWeight(goal, w * ikRotWeight);
        anim.SetIKPosition(goal, targetPos);
        anim.SetIKRotation(goal, targetRot);
    }

    AvatarIKGoal GetGoal()
    {
        return (kickingFoot == HumanBodyBones.RightFoot) ? AvatarIKGoal.RightFoot : AvatarIKGoal.LeftFoot;
    }

    void Zero(AvatarIKGoal goal)
    {
        anim.SetIKPositionWeight(goal, 0f);
        anim.SetIKRotationWeight(goal, 0f);
        targetPos = Vector3.zero;
    }

    // ---------- 애니메이션 이벤트에서 호출 ----------
    // 임팩트 구간 시작 (duration 초 동안 IK 활성화)
    public void KickIKOn(float duration)  // 클립 이벤트로 duration 넘기기 권장
    {
        ikDuration = (duration > 0f) ? duration : defaultDuration;
        ikTimer = ikDuration;
        ikActive = true;
    }

    public void KickIKOn() => KickIKOn(defaultDuration);

    // 필요 시 조기 종료
    public void KickIKOff()
    {
        ikActive = false;
        ikTimer = 0f;
    }

    // ---------- 디버그 기즈모 ----------
    void OnDrawGizmosSelected()
    {
        if (targetPos != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(targetPos, 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(targetPos, targetRot * Vector3.forward * 0.3f);
        }
    }
}
