using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ball Reference")]
    public Ball ball; // 에디터에서 드래그해서 연결

    [Header("Field Settings")]
    public Transform ballSpawnPoint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (ball != null && ballSpawnPoint != null)
        {
            ball.ResetPosition(ballSpawnPoint.position);
        }
    }

    // 골, 리셋 같은 게임 이벤트 관리
    public void OnGoal()
    {
        Debug.Log("Goal!");
        if (ballSpawnPoint != null)
        {
            ball.ResetPosition(ballSpawnPoint.position);
        }
    }
}
