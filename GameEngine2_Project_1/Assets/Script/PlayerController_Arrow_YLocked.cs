using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController_Arrow_YLocked : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;      // 이동 속도
    public float accel = 14f;         // 가속 보간
    public float rotateLerp = 16f;    // 회전 스무딩

    [Header("Camera-Relative (옵션)")]
    public bool cameraRelative = false;
    public Transform cam;             // cameraRelative=true일 때 사용

    [Header("Animator(옵션)")]
    public Animator animator;         // 없어도 동작. 있으면 파라미터 반영
    public float damp = 0.08f;        // Animator 댐핑

    // 읽기 전용 (디버그/연동)
    public float Speed { get; private set; }   // XZ 속도 크기
    public float MoveX { get; private set; }   // 우/좌 성분 (-1~+1)
    public float MoveY { get; private set; }   // 전/후 성분 (-1~+1)
    public bool IsStrafe { get; private set; } // C 키로 토글
    public bool IsMoving { get; private set; } // 이동 중인지

    CharacterController cc;
    Vector3 vel;     // XZ 속도
    float baseY;     // 고정할 Y

    // Animator 해시
    int hSpeed, hMoveX, hMoveY, hIsStrafe, hIsMoving;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator)
        {
            hSpeed = Animator.StringToHash("Speed");
            hMoveX = Animator.StringToHash("MoveX");
            hMoveY = Animator.StringToHash("MoveY");
            hIsStrafe = Animator.StringToHash("IsStrafe");
            hIsMoving = Animator.StringToHash("IsMoving");
        }
    }

    void OnEnable()
    {
        baseY = transform.position.y; // 시작 높이 고정
    }

    void Update()
    {
        // 1) 입력
        float x = Input.GetAxisRaw("Horizontal"); // ←→
        float z = Input.GetAxisRaw("Vertical");   // ↑↓
        IsStrafe = Input.GetKey(KeyCode.C);

        // 2) 목표 방향(wish)
        Vector3 wish;
        if (!cameraRelative)
        {
            wish = new Vector3(x, 0f, z);
        }
        else
        {
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            wish = fwd * z + right * x;
        }
        if (wish.sqrMagnitude > 1e-6f) wish.Normalize();

        // 3) 속도 보간 (XZ만)
        Vector3 targetVel = wish * moveSpeed;
        vel = Vector3.Lerp(vel, targetVel, 1 - Mathf.Exp(-accel * Time.deltaTime));

        // 4) 이동
        cc.Move(vel * Time.deltaTime);

        // 5) Y 고정
        var p = transform.position; p.y = baseY; transform.position = p;

        // 6) 회전
        //  - 기본: 입력 방향을 바라보며 전진(JogForward)
        //  - C(스트레이프): 시선 유지(회전 금지)
        if (!IsStrafe)
        {
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion to = Quaternion.LookRotation(flat);
                transform.rotation = Quaternion.Slerp(transform.rotation, to, rotateLerp * Time.deltaTime);
            }
        }

        // 7) 상태 산출
        Speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        IsMoving = Speed > 0.05f;

        // 현재 바라보는 방향 기준으로 입력 성분 분해 → 8방향 블렌드용
        Vector3 fwdBasis = transform.forward;                // 시선 유지 기준
        Vector3 rightBasis = Vector3.Cross(Vector3.up, fwdBasis);
        MoveX = Vector3.Dot(wish, rightBasis);               // +오른쪽 / -왼쪽
        MoveY = Vector3.Dot(wish, fwdBasis);                 // +앞 / -뒤

        // 8) 애니메이터 파라미터(있을 때만)
        if (animator)
        {
            animator.SetBool(hIsStrafe, IsStrafe);
            animator.SetBool(hIsMoving, IsMoving);
            animator.SetFloat(hSpeed, Speed, damp, Time.deltaTime);
            animator.SetFloat(hMoveX, MoveX, damp, Time.deltaTime);
            animator.SetFloat(hMoveY, MoveY, damp, Time.deltaTime);
        }
    }
}
