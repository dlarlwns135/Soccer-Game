using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ball Reference")]
    public Ball ball;

    [Header("Field Settings")]
    public Transform ballSpawnPoint;

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
        if (!_inPlay) return;
        _inPlay = false;

        Debug.Log($"GOAL! Side = {side}");
        // TODO: 점수 갱신, UI 연출 등
        StartCoroutine(RestartRoutine("KickOff", restartDelay));
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
