using UnityEngine;

public class HandTrigger : MonoBehaviour
{
    public Goalkeeper keeper;        // 골키퍼 본체
    public GameManager gameManager;  // 직접 참조
    public int ballLayer = 6;        // Ball 레이어 번호 (프로젝트에서 확인해서 넣어라)

    void OnTriggerEnter(Collider other)
    {
        // 레이어 체크
        if (other.gameObject.layer != ballLayer) return;

        // Dive 중이 아니면 무시
        if (!keeper.IsInDiveState) return;

        Ball ball = other.GetComponent<Ball>();
        if (ball == null) return;

        // GameManager에 알림
        gameManager.OnGoalkeeperSaved(keeper.transform);

        // 공의 Owner를 골키퍼로 설정
        ball.SetOwner(keeper.transform);
    }
}
