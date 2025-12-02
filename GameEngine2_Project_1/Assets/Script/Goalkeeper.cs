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
    public Ball ballLogic;

    [Header("Looking Settings")]
    public float lookSpeed = 3f;
    public float viewAngle = 180f;

    [Header("Dive Settings")]
    public float minApproachSpeed = 8f;
    public float triggerDistance = 20f;
    public float maxPredictTime = 1.0f;
    public float sideDeadZone = 0.3f;

    [Header("CharacterController (Dive)")]
    public float diveHeight = 1.0f;
    public Vector3 diveCenter = new Vector3(0f, 1.8074f, 0f);

    private bool _isDiving = false;

    private float _origHeight;
    private Vector3 _origCenter;

    [Header("IK Settings")]
    public bool enableIK = true;
    public float ikActivateDistance = 20.5f;
    public float ikPositionLerpSpeed = 100f;

    // 기본 IK weight 속도
    public float ikWeightLerpSpeed = 50f;
    // DiveMiddle일 때만 사용할 느린 속도
    public float ikWeightLerpSpeedMiddle = 10f;

    public Vector3 ikOffset = new Vector3(0f, 0.2f, 0f);

    float _currentIKWeight = 0f;

    Vector3 _debugLeftIKPos;
    Vector3 _debugRightIKPos;

    [Header("Hand IK Curve Data (Right Dive / 기본)")]
    public FootCurveData leftHandCurve;        // 기본 혹은 DiveRight용
    public FootCurveData rightHandCurve;

    [Header("Hand IK Curve Data (Left / Middle Dive)")]
    public FootCurveData leftHandCurve_Left;
    public FootCurveData rightHandCurve_Left;
    public FootCurveData leftHandCurve_Middle;
    public FootCurveData rightHandCurve_Middle;

    // 현재 재생 중인 다이브에 사용할 커브(실제 IK/기즈모는 이걸 사용)
    FootCurveData _activeLeftCurve;
    FootCurveData _activeRightCurve;

    // 지금 다이브가 Middle인지 여부
    bool _currentDiveIsMiddle = false;

    [Header("Hand IK Anim")]
    public string diveStateTag = "Dive";

    private bool _diveIkEnabled = false;

    [Header("Hand Curve Gizmo")]
    public bool drawHandCurves = true;
    public int handCurveSteps = 24;
    public Color leftHandCurveColor = Color.magenta;
    public Color rightHandCurveColor = Color.cyan;
    public float handCurvePointRadius = 0.02f;

    [Header("Manual Dive Limit")]
    public float minBallWorldX = 1796.56f;

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
        {
            _ballRb = ball.GetComponent<Rigidbody>();
            if (ballLogic == null)
                ballLogic = ball.GetComponent<Ball>();
        }

        // 기본값: 아무 설정 안 하면 오른쪽 다이브 커브(기존 필드)를 active로 사용
        _activeLeftCurve = leftHandCurve;
        _activeRightCurve = rightHandCurve;
        _currentDiveIsMiddle = false;
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

        if (ballLogic != null && !ballLogic.IsFree)
            return;

        Vector3 v = _ballRb.linearVelocity;
        float speed = v.magnitude;

        if (speed < minApproachSpeed)
            return;

        Vector3 toKeeper = transform.position - ball.position;
        float distance = toKeeper.magnitude;

        if (distance > triggerDistance || distance < 0.1f)
            return;

        if (ball.position.x < minBallWorldX)
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

        // 여기서 다이브 방향 + 커브 세팅 + Middle 여부를 동시에 결정
        if (sideX < -sideDeadZone)
        {
            _animator.SetTrigger("DiveLeft");
            _activeLeftCurve = leftHandCurve_Left;
            _activeRightCurve = rightHandCurve_Left;
            _currentDiveIsMiddle = false;
        }
        else if (sideX > sideDeadZone)
        {
            _animator.SetTrigger("DiveRight");
            _activeLeftCurve = leftHandCurve;        // 오른쪽은 기본 커브 사용
            _activeRightCurve = rightHandCurve;
            _currentDiveIsMiddle = false;
        }
        else
        {
            _animator.SetTrigger("DiveMiddle");
            _activeLeftCurve = leftHandCurve_Middle;
            _activeRightCurve = rightHandCurve_Middle;
            _currentDiveIsMiddle = true;             // Middle 다이브 표시
        }
    }

    void BeginDive()
    {
        _isDiving = true;
        _diveIkEnabled = false;

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
        _animator.applyRootMotion = false;

        if (_characterController != null)
        {
            _characterController.height = _origHeight;
            _characterController.center = _origCenter;

            yield return null;

            Vector3 delta = _startPosition - transform.position;
            _characterController.Move(delta);
        }
        else
        {
            transform.position = _startPosition;
            yield return null;
        }

        transform.rotation = _startRotation;

        _isDiving = false;
        _currentDiveIsMiddle = false;   // 상태 리셋
        _animator.Play("Goalkeeper Idle");

        _animator.applyRootMotion = true;
    }

    public void OnDiveFinished()
    {
        ResetToStart();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!enableIK) return;
        if (_animator == null) return;
        if (ball == null) return;

        // Middle일 때만 더 느린 속도 사용
        float currentLerpSpeed = _currentDiveIsMiddle ? ikWeightLerpSpeedMiddle : ikWeightLerpSpeed;

        if (!_isDiving || !_diveIkEnabled)
        {
            _currentIKWeight = Mathf.MoveTowards(
                _currentIKWeight,
                0f,
                currentLerpSpeed * Time.deltaTime
            );

            if (_currentIKWeight <= 0.001f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            }

            return;
        }

        var st = _animator.GetCurrentAnimatorStateInfo(layerIndex);

        if (!st.IsTag(diveStateTag))
        {
            return;
        }

        float norm = st.normalizedTime % 1f;

        // 현재 다이브에 대한 커브가 없으면 단순 IK로 대체
        if (_activeLeftCurve == null || _activeRightCurve == null)
        {
            ApplySimpleHandIK(currentLerpSpeed);
            return;
        }

        Vector3 leftLocal = new Vector3(
            _activeLeftCurve.curveX.Evaluate(norm),
            _activeLeftCurve.curveY.Evaluate(norm),
            _activeLeftCurve.curveZ.Evaluate(norm)
        );
        Vector3 rightLocal = new Vector3(
            _activeRightCurve.curveX.Evaluate(norm),
            _activeRightCurve.curveY.Evaluate(norm),
            _activeRightCurve.curveZ.Evaluate(norm)
        );

        Vector3 leftBase = transform.TransformPoint(leftLocal);
        Vector3 rightBase = transform.TransformPoint(rightLocal);

        Vector3 targetPos = ball.position + ikOffset;

        Vector3 toBall = ball.position - transform.position;
        toBall.y = 0f;
        if (toBall.sqrMagnitude < 0.0001f) toBall = transform.forward;
        Quaternion handRot = Quaternion.LookRotation(toBall, Vector3.up);

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 toBallFromLeft = ball.position - leftBase;
        Vector3 toBallFromRight = ball.position - rightBase;

        float projL = Vector3.Dot(toBallFromLeft, forward);
        float projR = Vector3.Dot(toBallFromRight, forward);

        bool ballPastHands = (projL < 0f && projR < 0f);

        float dist = Vector3.Distance(transform.position, ball.position);
        float targetWeight = 0f;

        if (!ballPastHands && dist <= ikActivateDistance)
            targetWeight = 1f;
        else
            targetWeight = 0f;

        _currentIKWeight = Mathf.MoveTowards(
            _currentIKWeight,
            targetWeight,
            currentLerpSpeed * Time.deltaTime
        );

        if (_currentIKWeight <= 0.001f)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        Vector3 leftFinal = Vector3.Lerp(
            leftBase,
            targetPos,
            _currentIKWeight
        );
        Vector3 rightFinal = Vector3.Lerp(
            rightBase,
            targetPos,
            _currentIKWeight
        );

        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _currentIKWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _currentIKWeight);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _currentIKWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _currentIKWeight);

        _animator.SetIKPosition(AvatarIKGoal.LeftHand, leftFinal);
        _animator.SetIKRotation(AvatarIKGoal.LeftHand, handRot);
        _animator.SetIKPosition(AvatarIKGoal.RightHand, rightFinal);
        _animator.SetIKRotation(AvatarIKGoal.RightHand, handRot);

        _debugLeftIKPos = leftFinal;
        _debugRightIKPos = rightFinal;
    }

    void ApplySimpleHandIK(float currentLerpSpeed)
    {
        Transform leftHandTr = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHandTr = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        bool ballPastHands = false;

        if (leftHandTr != null && rightHandTr != null)
        {
            float projL = Vector3.Dot(ball.position - leftHandTr.position, forward);
            float projR = Vector3.Dot(ball.position - rightHandTr.position, forward);
            ballPastHands = (projL < 0f && projR < 0f);
        }

        float dist = Vector3.Distance(transform.position, ball.position);
        float targetWeight = 0f;

        if (!ballPastHands && dist <= ikActivateDistance)
            targetWeight = 1f;
        else
            targetWeight = 0f;

        _currentIKWeight = Mathf.MoveTowards(
            _currentIKWeight,
            targetWeight,
            currentLerpSpeed * Time.deltaTime
        );

        if (_currentIKWeight <= 0.001f)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        Vector3 targetPos = ball.position + ikOffset;

        Vector3 toBall = ball.position - transform.position;
        toBall.y = 0f;
        if (toBall.sqrMagnitude < 0.0001f) toBall = transform.forward;
        Quaternion handRot = Quaternion.LookRotation(toBall, Vector3.up);

        Vector3 leftPos = _animator.GetIKPosition(AvatarIKGoal.LeftHand);
        Vector3 rightPos = _animator.GetIKPosition(AvatarIKGoal.RightHand);

        leftPos = Vector3.Lerp(leftPos, targetPos, ikPositionLerpSpeed * Time.deltaTime);
        rightPos = Vector3.Lerp(rightPos, targetPos, ikPositionLerpSpeed * Time.deltaTime);

        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _currentIKWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _currentIKWeight);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _currentIKWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _currentIKWeight);

        _animator.SetIKPosition(AvatarIKGoal.LeftHand, leftPos);
        _animator.SetIKRotation(AvatarIKGoal.LeftHand, handRot);
        _animator.SetIKPosition(AvatarIKGoal.RightHand, rightPos);
        _animator.SetIKRotation(AvatarIKGoal.RightHand, handRot);

        _debugLeftIKPos = leftPos;
        _debugRightIKPos = rightPos;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_debugLeftIKPos, 0.1f);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(_debugRightIKPos, 0.1f);
        }

        if (!drawHandCurves)
            return;

        // 플레이 중에는 현재 다이브에 해당하는 커브만 시각화
        if (Application.isPlaying)
        {
            if (_activeLeftCurve != null)
                DrawHandCurveGizmo(_activeLeftCurve, leftHandCurveColor);

            if (_activeRightCurve != null)
                DrawHandCurveGizmo(_activeRightCurve, rightHandCurveColor);
        }
        else
        {
            // 에디터에서 미리 보고 싶으면 기본 오른쪽 다이브 커브를 사용
            if (leftHandCurve != null)
                DrawHandCurveGizmo(leftHandCurve, leftHandCurveColor);

            if (rightHandCurve != null)
                DrawHandCurveGizmo(rightHandCurve, rightHandCurveColor);
        }
    }

    void DrawHandCurveGizmo(FootCurveData curveData, Color color)
    {
        if (curveData == null)
            return;

        int steps = Mathf.Max(4, handCurveSteps);

        Gizmos.color = color;

        Vector3 prev = Vector3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;

            Vector3 localP = new Vector3(
                curveData.curveX.Evaluate(t),
                curveData.curveY.Evaluate(t),
                curveData.curveZ.Evaluate(t)
            );

            Vector3 worldP = transform.TransformPoint(localP);

            Gizmos.DrawSphere(worldP, handCurvePointRadius);

            if (hasPrev)
                Gizmos.DrawLine(prev, worldP);

            prev = worldP;
            hasPrev = true;
        }
    }

    public void AnimEvent_DiveIKOn()
    {
        _diveIkEnabled = true;
    }

    public void AnimEvent_DiveIKOff()
    {
        _diveIkEnabled = false;
    }
}
