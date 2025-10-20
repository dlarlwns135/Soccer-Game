using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController_Arrow_YLocked : MonoBehaviour
{
    [Header("Control")] public bool cameraRelative = false; public Transform cam;
    [Header("RootMotion")] public float rootMotionScale = 1.0f;

    [Header("Input Smoothing")] public float inputSmoothTime = 0.02f;
    [Header("Turn Smoothing")] public float turnSmoothTime = 0.10f; public float turnDeadzone = 0.08f;
    [Header("Input Decay")] public float releaseDecay = 6f;

    [Header("Charge (D)")]
    public float shootChargeTimeMax = 1.0f;     // Ǯ���� �ð�
    public float heavyThreshold01 = 0.6f;     // >= �̸� Heavy
    public AnimationCurve chargeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public UnityEvent<float> OnShoot;           // �ɼ�: �Ŀ� �ݹ�

    // �б� ����
    public float Speed { get; private set; }
    public float MoveX { get; private set; }
    public float MoveY { get; private set; }
    public bool IsStrafe { get; private set; }
    public bool IsMoving { get; private set; }

    CharacterController cc; Animator animator; float baseY;

    // Animator hashes
    int hSpeed, hMoveX, hMoveY, hIsStrafe, hIsMoving, hIsSprinting, hShootType, hDoShoot;

    // buffers
    float moveXVel, moveYVel, yawVel;
    Vector2 smoothedInput;
    Vector3 lastWishDir = Vector3.zero;

    // charge
    bool charging = false; float chargeStartTime = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true;

        hSpeed = Animator.StringToHash("Speed");
        hMoveX = Animator.StringToHash("MoveX");
        hMoveY = Animator.StringToHash("MoveY");
        hIsStrafe = Animator.StringToHash("IsStrafe");
        hIsMoving = Animator.StringToHash("IsMoving");
        hIsSprinting = Animator.StringToHash("IsSprinting"); //  �� �Ķ����
        hShootType = Animator.StringToHash("ShootType");   //  �� �Ķ����
        hDoShoot = Animator.StringToHash("DoShoot");     //  �� �Ķ����(Trigger)
    }

    void OnEnable() { baseY = transform.position.y; }

    void Update()
    {
        // --- �Է� ---
        float xIn = Input.GetAxis("Horizontal");
        float zIn = Input.GetAxis("Vertical");
        IsStrafe = Input.GetKey(KeyCode.C);

        // E: ������Ʈ ����(Animator�� ����)
        bool isSprinting = Input.GetKey(KeyCode.E);
        animator.SetBool(hIsSprinting, isSprinting);

        // D: ���� ����/����/����
        if (Input.GetKeyDown(KeyCode.D)) { charging = true; chargeStartTime = Time.time; }
        if (Input.GetKeyUp(KeyCode.D))
        {
            charging = false;
            float held = Mathf.Max(0f, Time.time - chargeStartTime);
            float raw01 = Mathf.Clamp01(held / shootChargeTimeMax);
            float power01 = (chargeCurve != null) ? chargeCurve.Evaluate(raw01) : raw01;

            // ���� Ÿ�� ���� �� Animator �Ķ���͸� ����
            int shootType = (power01 >= heavyThreshold01) ? 2 : 1; // 1=Light, 2=Heavy
            animator.SetInteger(hShootType, shootType);
            animator.SetTrigger(hDoShoot);

            OnShoot?.Invoke(power01); // (�ɼ�) ���� �߻� ������ �Ŀ� ����
        }

        // --- �Է� ���� ---
        Vector2 targetInput = new Vector2(xIn, zIn);
        if (targetInput.sqrMagnitude > 1e-6f)
            smoothedInput = Vector2.MoveTowards(smoothedInput, targetInput, 99f * Time.deltaTime);
        else
            smoothedInput = Vector2.MoveTowards(smoothedInput, Vector2.zero, releaseDecay * Time.deltaTime);

        // --- wish(����/ī�޶� ����) ---
        Vector3 wish;
        if (!cameraRelative) wish = new Vector3(smoothedInput.x, 0f, smoothedInput.y);
        else
        {
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            wish = fwd * smoothedInput.y + right * smoothedInput.x;
        }
        if (wish.sqrMagnitude > 1e-6f) wish.Normalize();
        lastWishDir = wish;

        // --- MoveX/MoveY(�����) ---
        Vector3 fwdBasis = transform.forward;
        Vector3 rightBasis = Vector3.Cross(Vector3.up, fwdBasis);
        float targetMoveX = Vector3.Dot(wish, rightBasis);
        float targetMoveY = Vector3.Dot(wish, fwdBasis);
        MoveX = Mathf.SmoothDamp(MoveX, targetMoveX, ref moveXVel, inputSmoothTime);
        MoveY = Mathf.SmoothDamp(MoveY, targetMoveY, ref moveYVel, inputSmoothTime);
        IsMoving = (new Vector2(MoveX, MoveY).sqrMagnitude > 0.02f);

        // --- Animator �Ķ���� ����(���� ���̴� �׷����� ����) ---
        animator.SetBool(hIsStrafe, IsStrafe);
        animator.SetBool(hIsMoving, IsMoving);
        animator.SetFloat(hMoveX, MoveX);
        animator.SetFloat(hMoveY, MoveY);
        // Speed�� OnAnimatorMove���� ����
    }

    void OnAnimatorMove()
    {
        // �̵�
        Vector3 delta = animator.deltaPosition * rootMotionScale;
        delta.y = 0f;
        cc.Move(delta);

        // ȸ�� (��-��Ʈ������)
        if (!IsStrafe)
        {
            if (lastWishDir.magnitude > turnDeadzone)
            {
                float targetYaw = Mathf.Atan2(lastWishDir.x, lastWishDir.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVel, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }

        // Y ����
        var p = transform.position; p.y = baseY; transform.position = p;

        // Speed
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        Speed = new Vector3(delta.x, 0f, delta.z).magnitude / dt;
        animator.SetFloat(hSpeed, Speed);
    }
}
