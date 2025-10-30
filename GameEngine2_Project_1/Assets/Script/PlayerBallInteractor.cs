using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Animator))]
public class PlayerBallInteractor : MonoBehaviour
{
    [Header("Refs")]
    public Ball ball;
    public Transform foot;

    public bool HasBall => ball && ball.Owner == transform;
    public Transform BallTransform => ball ? ball.transform : null;

    [Header("Distances")]
    public float pickUpRadius = 0.7f;
    public float stealRadius = 0.6f;

    [Header("Tuning")]
    public float stealCooldown = 0.25f;
    public LayerMask obstructionMask = 0;

    [Header("Pickup/Kick Control")]
    public float pickupBlockAfterKick = 0.25f;
    float pickupBlockedUntil = -1f;

    Animator anim;
    float lastStealTime = -999f;

    // 캐시
    Collider[] playerSolidCols;   // isTrigger == false 만
    Collider[] ballSolidCols;     // isTrigger == false 만

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (!foot) foot = transform;
        if (!ball) ball = FindObjectOfType<Ball>(true);

        CacheColliders();
    }

    void OnEnable()
    {
        if (ball) ball.OnPossessionChanged += OnBallOwnerChanged;
    }

    void OnDisable()
    {
        if (ball) ball.OnPossessionChanged -= OnBallOwnerChanged;
        // 안전 복구
        TogglePlayerBallCollision(false);
    }

    void CacheColliders()
    {
        // 플레이어: 자식 포함, 트리거 제외
        playerSolidCols = GetComponentsInChildren<Collider>(true)
            .Where(c => c && c.enabled && !c.isTrigger)
            .ToArray();

        // 공: 자식 포함, 트리거 제외
        ballSolidCols = ball
            ? ball.GetComponentsInChildren<Collider>(true)
                  .Where(c => c && c.enabled && !c.isTrigger)
                  .ToArray()
            : new Collider[0];
    }

    void FixedUpdate()
    {
        if (!ball) return;

        // 혹시 런타임에 콜라이더 구성/활성 상태가 바뀌면 필요 시 다시 캐시
        // (부하를 줄이려면 조건부로만 호출)
        if (playerSolidCols == null || ballSolidCols == null) CacheColliders();

        Vector3 bp = ball.transform.position;
        float distSqr = (bp - foot.position).sqrMagnitude;

        if (ball.IsFree)
        {
            if (Time.time >= pickupBlockedUntil &&
                distSqr <= pickUpRadius * pickUpRadius &&
                NotObstructed(bp))
            {
                ball.SetOwner(transform);
                return;
            }
        }
        else if (ball.Owner != transform)
        {
            if (Time.time - lastStealTime >= stealCooldown &&
                distSqr <= stealRadius * stealRadius &&
                NotObstructed(bp))
            {
                ball.SetOwner(transform);
                lastStealTime = Time.time;
            }
        }
    }

    // 트리거(발)에서 호출: 레이스 해소용 빠른 소유 + 킥
    public void TriggerKickFromFoot()
    {
        if (ball == null) { Debug.LogWarning("[Interactor] no ball"); return; }

        if (HasBall) { OnKickContact(); return; }

        Vector3 me = foot ? foot.position : transform.position;
        float dist = Vector3.Distance(ball.transform.position, me);

        if (ball.IsFree && dist <= pickUpRadius * 1.1f && NotObstructed(ball.transform.position))
        {
            ball.SetOwner(transform);
            OnKickContact();
            return;
        }

        Debug.LogWarning("[Interactor] TriggerKickFromFoot ignored: not owner and not in pickup range");
    }

    void OnKickContact()
    {
        if (!ball) { Debug.LogWarning("[Interactor] Kick: no ball"); return; }
        if (!HasBall) { Debug.LogWarning("[Interactor] Kick blocked: not owner"); return; }

        pickupBlockedUntil = Time.time + pickupBlockAfterKick;

        ball.DisableAssist();

        Vector3 dir = (transform.forward + Vector3.up * 0.5f - transform.right * 0.1f).normalized;
        float impulse = 18f;
        Debug.Log($"[Interactor] Kick impulse={impulse}");
        ball.Kick(dir, impulse);
    }

    bool NotObstructed(Vector3 ballPos)
    {
        if (obstructionMask == 0) return true;
        Vector3 dir = (ballPos - foot.position);
        float d = dir.magnitude;
        return !Physics.Raycast(foot.position, dir.normalized, d - 0.05f, obstructionMask, QueryTriggerInteraction.Ignore);
    }

    void OnBallOwnerChanged(Transform owner)
    {
        // 콜라이더 캐시 최신화
        CacheColliders();

        if (anim)
        {
            int hHasBall = Animator.StringToHash("HasBall");
            if (hHasBall != 0) anim.SetBool(hHasBall, owner == transform);
        }
        if (ball == null) return;

        if (owner == transform)
        {
            // 소유 시작: 본체(비-트리거) ↔ 공(비-트리거) 충돌 무시
            TogglePlayerBallCollision(true);

            Vector3 localOffset = new Vector3(0.2f, 0.0f, 0.5f);
            ball.EnableAssist(transform, localOffset);
        }
        else
        {
            // 소유 해제: 충돌 복구
            TogglePlayerBallCollision(false);
            ball.DisableAssist();
        }
    }

    void TogglePlayerBallCollision(bool ignore)
    {
        if (playerSolidCols == null || ballSolidCols == null) return;

        foreach (var pc in playerSolidCols)
        {
            if (!pc) continue;
            foreach (var bc in ballSolidCols)
            {
                if (!bc) continue;
                // 트리거는 애초에 캐시에서 제외되어 있음
                Physics.IgnoreCollision(pc, bc, ignore);
            }
        }
    }
}
