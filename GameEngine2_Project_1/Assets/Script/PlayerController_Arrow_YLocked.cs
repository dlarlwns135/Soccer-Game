using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController_Arrow_YLocked : MonoBehaviour
{
    [Header("Control")]
    public bool cameraRelative = false;     // ī�޶� ���� �Է� ����
    public Transform cam;                   // cameraRelative=true�� �� �ʿ�
    public float rotateLerp = 16f;          // (��-��Ʈ������) ȸ�� ������
    public float rootMotionScale = 1.0f;    // ��Ʈ��� ���(��ü �ӵ� �̼� ����)

    [Header("Animator Damp")]
    public float damp = 0.08f;              // �Ķ���� ����

    // �б� ���� (�����/����)
    public float Speed { get; private set; }   // ��Ʈ��� ��� XZ �ӵ� (m/s)
    public float MoveX { get; private set; }   // ��/�� ���� (-1~+1)
    public float MoveY { get; private set; }   // ��/�� ���� (-1~+1)
    public bool IsStrafe { get; private set; } // C Ű�� ��Ʈ������
    public bool IsMoving { get; private set; } // �Է� ����

    CharacterController cc;
    Animator animator;
    float baseY; // Y ���� ����

    // Animator �ؽ�
    int hSpeed, hMoveX, hMoveY, hIsStrafe, hIsMoving;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        animator.applyRootMotion = true; // ��Ʈ��� ���

        hSpeed = Animator.StringToHash("Speed");
        hMoveX = Animator.StringToHash("MoveX");
        hMoveY = Animator.StringToHash("MoveY");
        hIsStrafe = Animator.StringToHash("IsStrafe");
        hIsMoving = Animator.StringToHash("IsMoving");
    }

    void OnEnable()
    {
        baseY = transform.position.y; // ���� ���� ����
    }

    void Update()
    {
        // 1) �Է�(����Ű + C)
        float x = Input.GetAxisRaw("Horizontal"); // ���
        float z = Input.GetAxisRaw("Vertical");   // ���
        IsStrafe = Input.GetKey(KeyCode.C);
        IsMoving = (x != 0f || z != 0f);

        // 2) �Է� ����(wish)
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

        // 3) ���� �ٶ󺸴� ���� ���� ����(2D ������)
        Vector3 fwdBasis = transform.forward;
        Vector3 rightBasis = Vector3.Cross(Vector3.up, fwdBasis);
        MoveX = Vector3.Dot(wish, rightBasis); // +������ / -����
        MoveY = Vector3.Dot(wish, fwdBasis);   // +�� / -��

        // 4) �ִϸ����� �Ķ����(��Ʈ��� ���� ��)
        animator.SetBool(hIsStrafe, IsStrafe);
        animator.SetBool(hIsMoving, IsMoving);
        animator.SetFloat(hMoveX, MoveX, damp, Time.deltaTime);
        animator.SetFloat(hMoveY, MoveY, damp, Time.deltaTime);
        // Speed�� ��Ʈ��� ���� �� OnAnimatorMove���� ����
    }

    void OnAnimatorMove()
    {
        // �� �̵�: ��Ʈ��� deltaPosition(XZ��) ����
        Vector3 delta = animator.deltaPosition * rootMotionScale;
        delta.y = 0f; // Y�� ����
        cc.Move(delta);

        // �� ȸ��: ��Ʈ�������� �ƴϸ� �Է� ������ �ٶ󺸰� ȸ��
        if (!IsStrafe)
        {
            // MoveX/MoveY�� '�� �ü� ����' ��ǥ�� �� ���� �������� ȯ��
            Vector3 worldDir = (transform.right * MoveX + transform.forward * MoveY);
            if (worldDir.sqrMagnitude > 1e-4f)
            {
                worldDir.Normalize();
                Quaternion to = Quaternion.LookRotation(worldDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, to, rotateLerp * Time.deltaTime);
            }
            // (�Է��� ���� ���� �� deltaRotation�� ������ ���� ����)
            // else { transform.rotation *= animator.deltaRotation; }
        }
        // ��Ʈ������ �߿� �ü� ����(ȸ�� ���� X)

        // �� Y ����
        var p = transform.position; p.y = baseY; transform.position = p;

        // �� Speed ���(��Ʈ��� ���) & �Ķ���� �ݿ�
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        Speed = new Vector3(delta.x, 0f, delta.z).magnitude / dt; // m/s
        animator.SetFloat(hSpeed, Speed, damp, Time.deltaTime);
    }
}
