using System.Collections;
using UnityEngine;

public class Goalkeeper : MonoBehaviour
{
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Vector3 _startForward;

    private Animator _animator;
    private Rigidbody _ballRb;
    private CharacterController _characterController;

    [Header("Ball Reference")]
    public Transform ball;

    [Header("Looking Settings")]
    public float lookSpeed = 3f;
    public float viewAngle = 180f;

    [Header("Dive Settings")]
    public float minApproachSpeed = 14f;
    public float triggerDistance = 20f;
    public float maxPredictTime = 1.0f;
    public float sideDeadZone = 0.3f;

    [Header("CharacterController (Dive)")]
    public float diveHeight = 1.0f;
    public Vector3 diveCenter = new Vector3(0f, 1.8074f, 0f);

    private bool _isDiving = false;

    private float _origHeight;
    private Vector3 _origCenter;

    void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;

        _startForward = transform.forward;
        _startForward.y = 0f;
        _startForward.Normalize();

        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();

        if (_characterController != null)
        {
            _origHeight = _characterController.height;
            _origCenter = _characterController.center;
        }

        if (ball != null)
            _ballRb = ball.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (_isDiving)
            return;

        if (ball == null)
        {
            RotateBackToStart();
            return;
        }

        Vector3 toBall = ball.position - transform.position;
        toBall.y = 0f;

        if (toBall.sqrMagnitude < 0.001f)
        {
            RotateBackToStart();
            return;
        }

        toBall.Normalize();

        float dot = Vector3.Dot(_startForward, toBall);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        if (angle <= viewAngle)
            LookAtBall(toBall);
        else
            RotateBackToStart();

        CheckDive();
    }

    void LookAtBall(Vector3 dir)
    {
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            lookSpeed * Time.deltaTime
        );
    }

    void RotateBackToStart()
    {
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            _startRotation,
            lookSpeed * Time.deltaTime
        );
    }

    void CheckDive()
    {
        if (_ballRb == null)
            return;

        Vector3 v = _ballRb.linearVelocity;   // 버전 따라 velocity일 수도 있음
        float speed = v.magnitude;

        if (speed < minApproachSpeed)
            return;

        Vector3 toKeeper = transform.position - ball.position;
        float distance = toKeeper.magnitude;

        if (distance > triggerDistance || distance < 0.1f)
            return;

        Vector3 vFlat = v;
        vFlat.y = 0f;
        Vector3 toKeeperFlat = toKeeper;
        toKeeperFlat.y = 0f;

        if (vFlat.sqrMagnitude < 0.0001f || toKeeperFlat.sqrMagnitude < 0.0001f)
            return;

        float approachDot = Vector3.Dot(vFlat.normalized, toKeeperFlat.normalized);
        if (approachDot < 0.5f)
            return;

        float timeToKeeper = Mathf.Clamp(distance / speed, 0f, maxPredictTime);
        Vector3 predictedPos = ball.position + v * timeToKeeper;

        Vector3 localHit = transform.InverseTransformPoint(predictedPos);
        float sideX = localHit.x;

        BeginDive();

        if (sideX < -sideDeadZone)
        {
            _animator.SetTrigger("DiveLeft");
        }
        else if (sideX > sideDeadZone)
        {
            _animator.SetTrigger("DiveRight");
        }
        else
        {
            _animator.SetTrigger("DiveMiddle");
        }
    }

    void BeginDive()
    {
        _isDiving = true;

        if (_characterController != null)
        {
            _characterController.height = diveHeight;
            _characterController.center = diveCenter;
        }
    }

    public void ResetToStart()
    {
        StartCoroutine(ResetCoroutine());
    }

    private IEnumerator ResetCoroutine()
    {
        // 1) 혹시 모를 루트 모션 영향 끄기 (필요하다면)
        _animator.applyRootMotion = false;

        // 2) 캐릭터컨트롤러가 있으면 Move로 강제 이동
        if (_characterController != null)
        {
            // 다이브 때 줄였던 캡슐 되돌리고
            _characterController.height = _origHeight;
            _characterController.center = _origCenter;

            // 한 프레임 쉬어주고 (애니/물리 정리)
            yield return null;

            // 현재 위치에서 스타트 위치까지의 차이만큼 한 번에 이동
            Vector3 delta = _startPosition - transform.position;
            _characterController.Move(delta);
        }
        else
        {
            // CC 없으면 그냥 위치 대입
            transform.position = _startPosition;
            yield return null;
        }

        // 3) 회전 리셋
        transform.rotation = _startRotation;

        _isDiving = false;
        _animator.Play("Goalkeeper Idle");

        _animator.applyRootMotion = true;
    }


    public void OnDiveFinished()
    {
        ResetToStart();
    }
}
