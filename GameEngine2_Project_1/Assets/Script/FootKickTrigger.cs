using UnityEngine;

public class FootKickTrigger : MonoBehaviour
{
    public PlayerBallInteractor owner;
    public LayerMask ballLayer;
    public float rehitDelay = 0.10f;

    [Header("Animator Tag Gate")]
    public Animator animator;   // 비워두면 부모에서 탐색
    public string requiredTag = "Action";   // 태그가 Action일 때만 발동

    float lastTime;

    void Reset()
    {
        owner = GetComponentInParent<PlayerBallInteractor>();
        animator = GetComponentInParent<Animator>();
    }

    void Awake()
    {
        if (!owner) owner = GetComponentInParent<PlayerBallInteractor>();
        if (!animator) animator = GetComponentInParent<Animator>();
        Debug.Log($"[FootKickTrigger] Awake: owner={owner}, animator={animator}");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!PassesAnimationGate())
        {
            Debug.Log("[FootKickTrigger] Gate blocked: not Action state or too early in clip");
            return;
        }

        // 레이어 필터
        if ((ballLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Ball 컴포넌트 확인
        Ball ball = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Ball>()
                                            : other.GetComponent<Ball>();
        if (!ball) return;

        // 연속 트리거 방지
        if (Time.time - lastTime < rehitDelay) return;
        lastTime = Time.time;

        if (owner)
        {
            Debug.Log("[FootKickTrigger] Kick!");
            owner.TriggerKickFromFoot();
        }
        else
        {
            Debug.LogWarning("[FootKickTrigger] owner missing");
        }
    }

    bool PassesAnimationGate()
    {
        if (!animator) return false;

        // 전이 중이면 무시
        if (animator.IsInTransition(0)) return false;

        // Base Layer 상태 가져오기
        var info = animator.GetCurrentAnimatorStateInfo(0);

        // 상태 태그가 Action이고, 진행도가 0.1 이상일 때만 허용
        return info.IsTag(requiredTag) && info.normalizedTime >= 0.1f;
    }
}
