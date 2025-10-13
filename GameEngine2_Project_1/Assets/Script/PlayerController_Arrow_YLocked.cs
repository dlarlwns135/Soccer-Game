using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController_Arrow_YLocked : MonoBehaviour
{
    [Header("Control")]
    public bool cameraRelative = false;     // 카메라 기준 입력 여부
    public Transform cam;                   // cameraRelative=true일 때 필요
    public float rotateLerp = 16f;          // (비-스트레이프) 회전 스무딩
    public float rootMotionScale = 1.0f;    // 루트모션 배속(전체 속도 미세 보정)

    [Header("Animator Damp")]
    public float damp = 0.08f;              // 파라미터 댐핑

    // 읽기 전용 (디버그/연동)
    public float Speed { get; private set; }   // 루트모션 기반 XZ 속도 (m/s)
    public float MoveX { get; private set; }   // 우/좌 성분 (-1~+1)
    public float MoveY { get; private set; }   // 전/후 성분 (-1~+1)
    public bool IsStrafe { get; private set; } // C 키로 스트레이프
    public bool IsMoving { get; private set; } // 입력 유무

    CharacterController cc;
    Animator animator;
    float baseY; // Y 고정 기준

    // Animator 해시
    int hSpeed, hMoveX, hMoveY, hIsStrafe, hIsMoving;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        animator.applyRootMotion = true; // 루트모션 사용

        hSpeed = Animator.StringToHash("Speed");
        hMoveX = Animator.StringToHash("MoveX");
        hMoveY = Animator.StringToHash("MoveY");
        hIsStrafe = Animator.StringToHash("IsStrafe");
        hIsMoving = Animator.StringToHash("IsMoving");
    }

    void OnEnable()
    {
        baseY = transform.position.y; // 시작 높이 고정
    }

    void Update()
    {
        // 1) 입력(방향키 + C)
        float x = Input.GetAxisRaw("Horizontal"); // ←→
        float z = Input.GetAxisRaw("Vertical");   // ↑↓
        IsStrafe = Input.GetKey(KeyCode.C);
        IsMoving = (x != 0f || z != 0f);

        // 2) 입력 벡터(wish)
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

        // 3) 현재 바라보는 방향 기준 성분(2D 블렌딩용)
        Vector3 fwdBasis = transform.forward;
        Vector3 rightBasis = Vector3.Cross(Vector3.up, fwdBasis);
        MoveX = Vector3.Dot(wish, rightBasis); // +오른쪽 / -왼쪽
        MoveY = Vector3.Dot(wish, fwdBasis);   // +앞 / -뒤

        // 4) 애니메이터 파라미터(루트모션 적용 전)
        animator.SetBool(hIsStrafe, IsStrafe);
        animator.SetBool(hIsMoving, IsMoving);
        animator.SetFloat(hMoveX, MoveX, damp, Time.deltaTime);
        animator.SetFloat(hMoveY, MoveY, damp, Time.deltaTime);
        // Speed는 루트모션 적용 후 OnAnimatorMove에서 세팅
    }

    void OnAnimatorMove()
    {
        // ─ 이동: 루트모션 deltaPosition(XZ만) 적용
        Vector3 delta = animator.deltaPosition * rootMotionScale;
        delta.y = 0f; // Y는 고정
        cc.Move(delta);

        // ─ 회전: 스트레이프가 아니면 입력 방향을 바라보게 회전
        if (!IsStrafe)
        {
            // MoveX/MoveY는 '현 시선 기준' 좌표계 → 월드 방향으로 환산
            Vector3 worldDir = (transform.right * MoveX + transform.forward * MoveY);
            if (worldDir.sqrMagnitude > 1e-4f)
            {
                worldDir.Normalize();
                Quaternion to = Quaternion.LookRotation(worldDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, to, rotateLerp * Time.deltaTime);
            }
            // (입력이 전혀 없을 땐 deltaRotation을 적용할 수도 있음)
            // else { transform.rotation *= animator.deltaRotation; }
        }
        // 스트레이프 중엔 시선 유지(회전 적용 X)

        // ─ Y 고정
        var p = transform.position; p.y = baseY; transform.position = p;

        // ─ Speed 계산(루트모션 기반) & 파라미터 반영
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        Speed = new Vector3(delta.x, 0f, delta.z).magnitude / dt; // m/s
        animator.SetFloat(hSpeed, Speed, damp, Time.deltaTime);
    }
}
