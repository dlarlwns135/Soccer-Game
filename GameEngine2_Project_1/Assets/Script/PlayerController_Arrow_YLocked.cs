using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController_Arrow_YLocked : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;      // �̵� �ӵ�
    public float accel = 14f;         // ���� ����
    public float rotateLerp = 16f;    // ȸ�� ������

    [Header("Camera-Relative (�ɼ�)")]
    public bool cameraRelative = false;
    public Transform cam;             // cameraRelative=true�� �� ���

    [Header("Animator(�ɼ�)")]
    public Animator animator;         // ��� ����. ������ �Ķ���� �ݿ�
    public float damp = 0.08f;        // Animator ����

    // �б� ���� (�����/����)
    public float Speed { get; private set; }   // XZ �ӵ� ũ��
    public float MoveX { get; private set; }   // ��/�� ���� (-1~+1)
    public float MoveY { get; private set; }   // ��/�� ���� (-1~+1)
    public bool IsStrafe { get; private set; } // C Ű�� ���
    public bool IsMoving { get; private set; } // �̵� ������

    CharacterController cc;
    Vector3 vel;     // XZ �ӵ�
    float baseY;     // ������ Y

    // Animator �ؽ�
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
        baseY = transform.position.y; // ���� ���� ����
    }

    void Update()
    {
        // 1) �Է�
        float x = Input.GetAxisRaw("Horizontal"); // ���
        float z = Input.GetAxisRaw("Vertical");   // ���
        IsStrafe = Input.GetKey(KeyCode.C);

        // 2) ��ǥ ����(wish)
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

        // 3) �ӵ� ���� (XZ��)
        Vector3 targetVel = wish * moveSpeed;
        vel = Vector3.Lerp(vel, targetVel, 1 - Mathf.Exp(-accel * Time.deltaTime));

        // 4) �̵�
        cc.Move(vel * Time.deltaTime);

        // 5) Y ����
        var p = transform.position; p.y = baseY; transform.position = p;

        // 6) ȸ��
        //  - �⺻: �Է� ������ �ٶ󺸸� ����(JogForward)
        //  - C(��Ʈ������): �ü� ����(ȸ�� ����)
        if (!IsStrafe)
        {
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion to = Quaternion.LookRotation(flat);
                transform.rotation = Quaternion.Slerp(transform.rotation, to, rotateLerp * Time.deltaTime);
            }
        }

        // 7) ���� ����
        Speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        IsMoving = Speed > 0.05f;

        // ���� �ٶ󺸴� ���� �������� �Է� ���� ���� �� 8���� �����
        Vector3 fwdBasis = transform.forward;                // �ü� ���� ����
        Vector3 rightBasis = Vector3.Cross(Vector3.up, fwdBasis);
        MoveX = Vector3.Dot(wish, rightBasis);               // +������ / -����
        MoveY = Vector3.Dot(wish, fwdBasis);                 // +�� / -��

        // 8) �ִϸ����� �Ķ����(���� ����)
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
