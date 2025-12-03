using UnityEngine;
using System.Collections;
using TMPro;

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

    // ===== Score =====
    [Header("Score Settings")]
    [SerializeField] int playerScore = 0;
    [SerializeField] int goalReward = 2;         // 골 넣으면 +1
    [SerializeField] int outOfPlayPenalty = 1;   // 그냥 아웃이면 -1
    [SerializeField] int savePenalty = 1;        // 골키퍼 세이브면 -2

    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    public enum TestMovePreset
    {
        Right,
        Middle,
        Left
    }

    [Header("Test Move (Y key)")]
    public float testMoveSpeed = 10f;

    public TestMovePreset testPreset = TestMovePreset.Right;

    public Vector3 testTargetPos = new Vector3(0f, 0.3f, 0f);

    void OnValidate()
    {
        switch (testPreset)
        {
            case TestMovePreset.Right:
                testTargetPos = new Vector3(1796.56f, 7f, 1820f);
                break;

            case TestMovePreset.Middle:
                testTargetPos = new Vector3(1796.56f, 7f, 1821.6f);
                break;

            case TestMovePreset.Left:
                testTargetPos = new Vector3(1796.56f, 7f, 1823f);
                break;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (ball != null && ballSpawnPoint != null)
            ball.ResetPosition(ballSpawnPoint.position);

        UpdateScoreUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (ball != null)
            {
                Vector3 toTarget = (testTargetPos - ball.transform.position);

                // 높이 보정은 옵션
                //toTarget.y = 0.1f;

                float dist = toTarget.magnitude;

                // 거리 기반으로 적절한 힘을 주는 예시
                float power = Mathf.Clamp(dist * 1.2f, 5f, 25f);

                ball.Kick(toTarget.normalized, power);

                Debug.Log($"[TestKick] Kick toward {testTargetPos} with power {power}");
            }
        }

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

        AddScore(-outOfPlayPenalty);

        Debug.Log($"Out of play at {at}");
        StartCoroutine(RestartRoutine("ThrowIn/GoalKick (simple)", restartDelay));
    }

    public void OnGoalScored(GoalTrigger.Side side, Ball b)
    {
        //if (!_inPlay) return;
        //_inPlay = false;

        Debug.Log($"GOAL! Side = {side}");
        AddScore(goalReward);
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

    public void OnGoalkeeperSaved(Transform keeper)
    {
        Debug.Log("Goalkeeper Saved!");
        if (ball != null)
        {
            ball.SetOwner(keeper.transform);
            ball.SetKeeperOwned(true);

            AddScore(-savePenalty);

            StartCoroutine(CoKeeperReset());
        }
    }

    IEnumerator CoKeeperReset()
    {
        yield return new WaitForSeconds(1f);

        // 공 소유 해제 + 리셋
        if (ball != null && ballSpawnPoint != null)
        {
            ball.SetKeeperOwned(false);
            ball.Release();
            ball.ResetPosition(ballSpawnPoint.position);
        }

        Debug.Log("Ball reset after goalkeeper save");
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {playerScore}";
    }

    void AddScore(int delta)
    {
        playerScore += delta;
        UpdateScoreUI();
    }
}
