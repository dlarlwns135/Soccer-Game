using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerBallInteractor : MonoBehaviour
{
    [Header("Refs")]
    public Ball ball;                
    public Transform foot;           

    public bool HasBall => ball && ball.Owner == transform;
    public Transform BallTransform => ball ? ball.transform : null;

    [Header("Distances")]
    public float pickUpRadius = 0.7f; // 공이 자유일 때 소유
    public float stealRadius = 0.6f; // 남이 들고 있을 때 뺏기

    [Header("Tuning")]
    public float stealCooldown = 0.25f;  // 연속 뺏기 방지
    public LayerMask obstructionMask = 0; // 선택: 사이에 벽/장애물 있으면 차단

    [Header("Pickup/Kick Control")]
    public float pickupBlockAfterKick = 0.25f; // 킥 후 이 시간 동안은 줍기 금지
    float pickupBlockedUntil = -1f;

    Animator anim;
    float lastStealTime = -999f;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (!foot) foot = transform; // 없으면 본체 기준
        if (!ball) ball = FindObjectOfType<Ball>(true);
    }

    void OnEnable()
    {
        if (ball) ball.OnPossessionChanged += OnBallOwnerChanged;
    }
    void OnDisable()
    {
        if (ball) ball.OnPossessionChanged -= OnBallOwnerChanged;
    }

    void FixedUpdate()
    {
        if (!ball) return;

        Vector3 bp = ball.transform.position;
        float distSqr = (bp - foot.position).sqrMagnitude;

        // 1) 공이 자유면: 주워오기 (쿨다운 체크 추가!)
        if (ball.IsFree)
        {
            if (Time.time >= pickupBlockedUntil &&               // ★ 추가
                distSqr <= pickUpRadius * pickUpRadius &&
                NotObstructed(bp))
            {
                ball.SetOwner(transform);
                return;
            }
        }
        // 2) 소유자가 있는데 내가 뺏을 수 있나?
        else if (ball.Owner != transform)
        {
            if (Time.time - lastStealTime >= stealCooldown &&
                distSqr <= stealRadius * stealRadius && NotObstructed(bp))
            {
                ball.SetOwner(transform);
                lastStealTime = Time.time;
            }
        }
    }

    void OnKickContact()
    {
        if (HasBall && ball != null)
        {
            // 킥 직후 일정 시간은 줍기 금지
            pickupBlockedUntil = Time.time + pickupBlockAfterKick;

            // 바라보는 방향 + 살짝 위로
            Vector3 dir = (transform.forward + Vector3.up * 0.1f).normalized;
            float impulse = 10f; // 필요에 맞게 조정
            ball.Kick(dir, impulse);
        }
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
        // 선택: 애니메이터에 HasBall 같은 파라미터를 쓴다면 반영
        if (anim)
        {
            int hHasBall = Animator.StringToHash("HasBall");
            if (hHasBall != 0) anim.SetBool(hHasBall, owner == transform);
        }
    }
}
