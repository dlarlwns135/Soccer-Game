using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ball Reference")]
    public Ball ball;

    [Header("Field Settings")]
    public Transform ballSpawnPoint;

    // 경기장 바닥 메쉬(또는 콜라이더)에 붙은 Renderer
    [SerializeField] private Renderer fieldRenderer;

    [Header("Restart")]
    public float restartDelay = 1.0f;

    bool _inPlay = true;   // 중복 판정 방지 플래그

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (ball != null && ballSpawnPoint != null)
            ball.ResetPosition(ballSpawnPoint.position);
    }

    void Update()
    {
        // Q 키를 누르면 공 리셋
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (ball != null && ballSpawnPoint != null)
            {
                ball.ResetPosition(ballSpawnPoint.position);
                Debug.Log("Ball reset manually by Q key");
            }
        }

        // 경기 중일 때 공이 필드 밖으로 나갔는지 체크
        if (_inPlay && ball != null && fieldRenderer != null)
        {
            Vector3 pos = ball.transform.position;
            if (!IsBallOverField(pos))
            {
                // 한 번이라도 bounds 밖으로 나간 프레임에 바로 아웃 처리
                OnBallOutOfPlay(ball, pos);
            }
        }
    }

    // 경기장 bounds 안에 있는지(x,z 기준) 체크
    bool IsBallOverField(Vector3 worldPos)
    {
        Bounds b = fieldRenderer.bounds;

        bool insideX = worldPos.x >= b.min.x && worldPos.x <= b.max.x;
        bool insideZ = worldPos.z >= b.min.z && worldPos.z <= b.max.z;

        return insideX && insideZ;
    }

    // ====== 외부에서 호출 ======
    public void OnBallOutOfPlay(Ball b, Vector3 at)
    {
        if (!_inPlay) return;
        _inPlay = false;

        Debug.Log($"Out of play at {at}");
        StartCoroutine(RestartRoutine("ThrowIn/GoalKick (simple)", restartDelay));
    }

    public void OnGoalScored(GoalTrigger.Side side, Ball b)
    {
        //if (!_inPlay) return;
        //_inPlay = false;

        Debug.Log($"GOAL! Side = {side}");
        //StartCoroutine(RestartRoutine("KickOff", restartDelay));
    }

    IEnumerator RestartRoutine(string reason, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ballSpawnPoint != null && ball != null)
            ball.ResetPosition(ballSpawnPoint.position);

        _inPlay = true;
        Debug.Log($"Restart: {reason}");
    }

    // 기존 테스트용 수동 리셋
    public void OnGoal()
    {
        Debug.Log("Goal! (manual)");
        if (ballSpawnPoint != null && ball != null)
            ball.ResetPosition(ballSpawnPoint.position);
    }
}
