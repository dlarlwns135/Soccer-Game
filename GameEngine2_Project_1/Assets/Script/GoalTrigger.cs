using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public enum Side { Home, Away }
    [SerializeField] Side side;
    [SerializeField] GameManager gm;

    void OnTriggerEnter(Collider other)
    {
        var ball = other.GetComponentInParent<Ball>();
        if (!ball) return;

        // (선택) 필드방향에서 들어왔는지 간단 체크: 골문 forward 기준
        // if (Vector3.Dot(ball.Velocity, transform.forward) <= 0f) return;

        gm?.OnGoalScored(side, ball);
    }
}
