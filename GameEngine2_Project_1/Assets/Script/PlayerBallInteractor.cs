using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerBallInteractor : MonoBehaviour
{
    [Header("Refs")]
    public Ball ball;                 // GameManager에서 주입하거나 에디터에서 드래그
    public Transform foot;            // 발(또는 공과 거리 잴 기준점)

    [Header("Distances")]
    public float pickUpRadius = 0.7f; // 공이 자유일 때 소유
    public float stealRadius = 0.6f; // 남이 들고 있을 때 뺏기

    [Header("Tuning")]
    public float stealCooldown = 0.25f;  // 연속 뺏기 방지
    public LayerMask obstructionMask = 0; // 선택: 사이에 벽/장애물 있으면 차단

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

        // 1) 공이 자유면: 주워오기
        if (ball.IsFree)
        {
            if (distSqr <= pickUpRadius * pickUpRadius && NotObstructed(bp))
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
        // 3) 내가 소유 중이면(선택) 애니메이터/드리블 보조 등 처리 가능
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
