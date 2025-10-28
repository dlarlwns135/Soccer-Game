using UnityEngine;

public class FieldBoundaryTrigger : MonoBehaviour
{
    [SerializeField] GameManager gm;

    void OnTriggerExit(Collider other)
    {
        var ball = other.GetComponentInParent<Ball>();
        if (!ball) return;

        // 공이 필드 트리거 바깥으로 나감 = 아웃 오브 플레이
        if (gm != null) gm.OnBallOutOfPlay(ball, other.transform.position);
    }
}
