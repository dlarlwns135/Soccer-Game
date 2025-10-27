using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    private Rigidbody rb;

    public Transform Owner { get; private set; }
    public bool IsFree => Owner == null;

    public event Action<Transform> OnPossessionChanged;

    [Header("Rolling")]
    public float radius = 0.11f;         // 축구공 반지름(미터) 대략 0.11
    public float rollThreshold = 0.05f;  // 이 속도 이하에선 회전 안 줌

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 50f;     // 빠르게 굴러도 각속도 제한에 안 걸리게
    }

    public void Kick(Vector3 direction, float force)
    {
        Release();
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    public Vector3 GetPosition() => transform.position;

    public void ResetPosition(Vector3 pos)
    {
        rb.linearVelocity = Vector3.zero;          // <-- linearVelocity 아님
        rb.angularVelocity = Vector3.zero;
        transform.position = pos;
        Release();
    }

    public void SetOwner(Transform newOwner)
    {
        if (Owner == newOwner) return;

        Owner = newOwner;
        OnPossessionChanged?.Invoke(Owner);
        rb.isKinematic = (Owner != null);
        if (rb.isKinematic)
        {
            // 소유 시작 시 회전 잔상 제거
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void Release()
    {
        if (Owner == null) return;
        Owner = null;
        rb.isKinematic = false;
        OnPossessionChanged?.Invoke(null);
    }

    void FixedUpdate()
    {
        if (Owner != null)
        {
            // 소유 중: 발 앞을 따라가게 (원하면 스프링/보간로직로 대체)
            Vector3 followPos = Owner.position + Owner.forward * 1.0f;
            rb.MovePosition(followPos);
            return;
        }

        // 자유 상태: 속도 방향으로 굴러가도록 각속도 부여
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed > rollThreshold && radius > 1e-4f)
        {
            // 회전축: 위벡터와 이동방향의 외적 (바닥 위를 구르는 축)
            Vector3 axis = Vector3.Cross(Vector3.up, v).normalized;
            // 각속도 크기: ω = |v| / r
            rb.angularVelocity = axis * (speed / radius);
        }
        else
        {
            // 거의 멈춤: 떨림 방지
            rb.angularVelocity = Vector3.zero;
        }
    }
}
