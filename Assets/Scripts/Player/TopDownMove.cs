using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownMove : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float speedMultiplier = 1f; // 허약 등 상태이상용 배율
    public float CurrentSpeed { get { return moveSpeed * speedMultiplier; } }
    public Vector3 MoveDirection { get; private set; }
    public bool movementLocked = false;
    public bool inputInverted = false; // 혼란


    Rigidbody rb;
    Camera cam;


void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        Application.runInBackground = true;
    }

void FixedUpdate()
    {
        // 플레이어 이동은 전부 MovePosition으로 처리한다. MovePosition은 속도를 지우지 않으므로,
        // 적과 부딪히거나 돌진에 받혔을 때 생긴 속도가 감쇠 없이 남아 영원히 밀려나게 된다.
        // 물리 속도는 어디서도 쓰지 않으니 매 프레임 지운다.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (movementLocked)
        {
            MoveDirection = Vector3.zero;
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (cam != null)
        {
            camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }

        if (inputInverted) { h = -h; v = -v; }

        Vector3 dir = (camRight * h + camForward * v).normalized;
        MoveDirection = dir;
        rb.MovePosition(rb.position + dir * CurrentSpeed * Time.fixedDeltaTime);
    }
}
