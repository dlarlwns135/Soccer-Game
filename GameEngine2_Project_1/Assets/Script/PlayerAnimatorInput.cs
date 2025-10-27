using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimatorInput : MonoBehaviour
{
    [Header("Smoothing")]
    public float inputSmoothTime = 0.08f;
    public float releaseDecay = 6f;

    [Header("Charge (D)")]
    public float shootChargeTimeMax = 1.0f;
    public float heavyThreshold01 = 0.6f;

    [Header("Turn")]
    public float turnSmoothTime = 0.1f;
    float turnVel;

    [Header("Rotate Lock")]
    [SerializeField] string actionTag = "Action";
    int hTagAction;

    Animator anim;
    int hSpeed, hMoveX, hMoveY, hIsStrafe, hIsMoving, hIsSprinting, hShootType, hDoShoot;

    float moveX, moveY;
    float velX, velY;
    float chargeStart; bool charging;

    void Awake()
    {
        anim = GetComponent<Animator>();
        hSpeed = Animator.StringToHash("Speed");
        hMoveX = Animator.StringToHash("MoveX");
        hMoveY = Animator.StringToHash("MoveY");
        hIsStrafe = Animator.StringToHash("IsStrafe");
        hIsMoving = Animator.StringToHash("IsMoving");
        hIsSprinting = Animator.StringToHash("IsSprinting");
        hShootType = Animator.StringToHash("ShootType");
        hDoShoot = Animator.StringToHash("DoShoot");

        hTagAction = Animator.StringToHash(actionTag);
    }

    void Update()
    {
        // 1) 입력
        float xIn = Input.GetAxisRaw("Horizontal");
        float yIn = Input.GetAxisRaw("Vertical");
        Vector2 target = new Vector2(xIn, yIn);
        if (target.sqrMagnitude > 1f) target.Normalize();

        // 2) 스무딩
        if (target.sqrMagnitude > 1e-6f)
        {
            moveX = Mathf.SmoothDamp(moveX, target.x, ref velX, inputSmoothTime);
            moveY = Mathf.SmoothDamp(moveY, target.y, ref velY, inputSmoothTime);
        }
        else
        {
            moveX = Mathf.MoveTowards(moveX, 0f, releaseDecay * Time.deltaTime);
            moveY = Mathf.MoveTowards(moveY, 0f, releaseDecay * Time.deltaTime);
            velX = velY = 0f;
        }

        // 3) 파생값
        Vector2 mv = new Vector2(moveX, moveY);
        float speed01 = Mathf.Clamp01(mv.magnitude);
        bool isMoving = speed01 > 0.02f;

        bool isStrafe = Input.GetKey(KeyCode.C);
        anim.SetBool(hIsStrafe, isStrafe);
        anim.SetBool(hIsSprinting, Input.GetKey(KeyCode.E));

        // --- 핵심: C 눌림 + 이동 중이면 월드 입력을 현재 바라보는 방향 기준 로컬로 회전 ---
        if (isStrafe && isMoving)
        {
            // 월드 기준 이동 벡터(mv.x=world X, mv.y=world Z)를 Transform의 로컬(right/forward)로 투영
            Vector3 worldDir = new Vector3(mv.x, 0f, mv.y);
            float localX = Vector3.Dot(worldDir, transform.right);
            float localY = Vector3.Dot(worldDir, transform.forward);
            mv = new Vector2(localX, localY);
        }

        // 4) Animator에 전달
        anim.SetFloat(hMoveX, Mathf.Clamp(mv.x, -1f, 1f));
        anim.SetFloat(hMoveY, Mathf.Clamp(mv.y, -1f, 1f));
        anim.SetFloat(hSpeed, speed01);
        anim.SetBool(hIsMoving, isMoving);

        // 5) D키 차지/발사
        if (Input.GetKeyDown(KeyCode.D)) { charging = true; chargeStart = Time.time; }
        if (Input.GetKeyUp(KeyCode.D))
        {
            charging = false;
            float held = Mathf.Clamp01((Time.time - chargeStart) / Mathf.Max(0.0001f, shootChargeTimeMax));
            int shootType = (held >= heavyThreshold01) ? 2 : 1; // 1=Light, 2=Heavy
            anim.SetInteger(hShootType, shootType);
            anim.SetTrigger(hDoShoot);
        }

        // 6) 회전: C가 꺼져있을 때만 입력 방향을 바라보게
        if (!IsInAction()) 
        {
            if (!isStrafe && isMoving)
            {
                float targetYaw = Mathf.Atan2(moveX, moveY) * Mathf.Rad2Deg;
                float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVel, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }

    }

    bool IsInAction()
    {
        var s = anim.GetCurrentAnimatorStateInfo(0);
        if (s.tagHash == hTagAction) return true;

        if (anim.IsInTransition(0))
        {
            var n = anim.GetNextAnimatorStateInfo(0);
            if (n.tagHash == hTagAction) return true;
        }
        return false;
    }
}
