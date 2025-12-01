using UnityEngine;

public class FootKickTrigger : MonoBehaviour
{
    public PlayerBallInteractor owner;
    public LayerMask ballLayer;
    public float rehitDelay = 0.10f;

    [Header("Animator Gate")]
    public Animator animator;              // 비우면 부모에서 탐색
    public int animLayer = 0;              // 킥이 재생되는 레이어
    public string requiredTag = "Action";  // 해당 태그 상태에서만
    [Range(0, 1)] public float windowStart = 0.25f;
    [Range(0, 1)] public float windowEnd = 0.45f;

    [Header("Options")]
    public bool allowOtherIsTrigger = false;
    public bool verboseLog = true;

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
        if (verboseLog)
            Debug.Log($"[FootKickTrigger:{name}] Awake owner={owner?.name}, animator={animator?.name}, myCollider.isTrigger={GetComponent<Collider>()?.isTrigger}");
    }

    void OnTriggerEnter(Collider other)
    {
        //if (verboseLog)
            //Debug.Log($"[FootKickTrigger:{name}] OnTriggerEnter {other.name} | layer={other.gameObject.layer} | tag={other.tag} | isTrigger={other.isTrigger} | rb={(other.attachedRigidbody ? other.attachedRigidbody.name : "null")}");

        if (!owner) { Warn("Blocked: owner null"); return; }
        if (other.isTrigger && !allowOtherIsTrigger) { Warn("Blocked: other.isTrigger"); return; }

        // 1) 레이어 필터
        if ((ballLayer.value & (1 << other.gameObject.layer)) == 0)
        {
            Warn($"Blocked: layer mismatch. mask={System.Convert.ToString(ballLayer.value, 2)} other={other.gameObject.layer}");
            return;
        }

        // 2) 애니메이션 게이트
        if (!PassesAnimationGate(out float norm, out bool inTrans, out bool hasTag))
        {
            Warn($"Gate blocked: hasTag={hasTag}, inTransition={inTrans}, norm={norm:0.000}, layer={animLayer}");
            return;
        }

        // 3) Ball 컴포넌트
        Ball ball = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Ball>()
                                            : other.GetComponent<Ball>();
        if (!ball) { Warn("Blocked: Ball component not found"); return; }

        // 4) 쿨다운
        float dt = Time.time - lastTime;
        if (dt < rehitDelay) { Warn($"Blocked: cooldown {dt:0.000}/{rehitDelay:0.000}"); return; }

        lastTime = Time.time;
        if (verboseLog) Debug.Log($"[FootKickTrigger:{name}] Kick! norm={norm:0.000} layer={animLayer}");
        owner.TriggerKickFromFoot();
    }

    bool PassesAnimationGate(out float norm, out bool inTransition, out bool hasTag)
    {
        norm = 0f; inTransition = false; hasTag = false;
        if (!animator) return false;

        inTransition = animator.IsInTransition(animLayer);
        if (inTransition) return false;

        var info = animator.GetCurrentAnimatorStateInfo(animLayer);
        norm = info.normalizedTime % 1f;
        hasTag = string.IsNullOrEmpty(requiredTag) || info.IsTag(requiredTag);

        bool inWindow = (norm >= windowStart && norm <= windowEnd);
       // if (verboseLog)
            //Debug.Log($"[FootKickTrigger:{name}] GateCheck: hasTag={hasTag}, norm={norm:0.000}, stateHash={info.fullPathHash}, loop={Mathf.FloorToInt(info.normalizedTime)}, inWindow={inWindow}, layer={animLayer}");
        return hasTag && inWindow;
    }

    void Warn(string msg)
    {
        //if (verboseLog) Debug.LogWarning($"[FootKickTrigger:{name}] {msg}");
    }
}
