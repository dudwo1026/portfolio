using UnityEngine;
using UnityEngine.InputSystem;

// TopDownMove(기본 0)보다 먼저 돈다. 대시가 끝나는 프레임에 movementLocked를 먼저 풀어야
// 같은 프레임에 TopDownMove가 입력을 읽는다. 순서가 반대면 그 한 프레임 동안
// MoveDirection이 0으로 남아 정지 스프라이트가 스쳐 지나간다
[DefaultExecutionOrder(-100)]
public class PlayerDash : MonoBehaviour
{
    // dash distance so that (dash + remaining walk during cooldown) totals 10% more
    // than plain walking over one full cooldown cycle:
    // target = walkSpeed(9)*cooldown(0.8)*1.1 = 7.92; minus walking the leftover
    // (cooldown-dashDuration)=0.6s at walkSpeed = 5.4 => dashDistance = 2.52.
    //
    // walkSpeed가 바뀌면 이 값도 같이 바꿔야 한다. 안 그러면 대시가 그냥 걷기보다
    // 느려져서 쓸 이유가 없어진다 (이동속도 1.5배 때 6->9 가 되면서 3.36 -> 5.04 로 올렸다)
    public float dashDistance = 5.04f; // 2x the 10%-cycle-target value above
    public float dashDuration = 0.2f;
    public float cooldown = 0.8f;

    // 대시 모션이 이 값들을 보고 자세와 좌우를 정한다
    public bool IsDashing { get { return dashTimeLeft > 0f; } }
    public Vector3 DashDirection { get { return dashDir; } }

    float nextDashTime = 0f;
    float dashTimeLeft = 0f;
    Vector3 dashDir;
    Rigidbody rb;
    TopDownMove mover;
    CapsuleCollider capsule;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mover = GetComponent<TopDownMove>();
        capsule = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // 일시정지(확인창·게임오버) 중에는 입력을 받지 않는다
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame && Time.time >= nextDashTime)
            BeginDash();
    }

    void BeginDash()
    {
        nextDashTime = Time.time + cooldown;
        dashTimeLeft = dashDuration;
        dashDir = mover != null && mover.MoveDirection.sqrMagnitude > 0.0001f
            ? mover.MoveDirection
            : transform.forward;
        if (mover != null) mover.movementLocked = true;
    }

    void FixedUpdate()
    {
        if (dashTimeLeft <= 0f) return;

        float speed = dashDistance / dashDuration;
        float step = speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + dashDir * AllowedStep(step));
        dashTimeLeft -= Time.fixedDeltaTime;

        if (dashTimeLeft <= 0f && mover != null)
            mover.movementLocked = false;
    }


    // 대시는 한 프레임에 0.34씩 건너뛴다. 난간·울타리처럼 얇은 콜라이더는 그 사이로 그냥 통과한다.
    // 움직이기 전에 몸통을 쓸어보고, 막힌 만큼 잘라낸다.
    float AllowedStep(float step)
    {
        const float skin = 0.06f;
        float allowed = step;

        // 1) 몸통 스윕: 떨어진 곳에서 다가갈 때 몸이 벽에 박히기 전에 세운다
        RaycastHit[] hits = rb.SweepTestAll(dashDir, step + skin, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (Blocked(hits[i].collider) == false) continue;
            // 벽에 붙어 옆으로 대시하는 중이면 막지 않는다. 정면으로 파고들 때만 자른다
            if (Vector3.Dot(hits[i].normal, dashDir) > -0.1f) continue;
            float d = hits[i].distance - skin;
            if (d < allowed) allowed = d;
        }

        // 2) 중심선 레이: SweepTest는 '이미 겹쳐 있는' 콜라이더를 무시한다.
        //    울타리에 몸을 붙인 채 대시하면 스윕이 아무것도 못 잡아 그대로 통과해 버린다.
        //    그래서 캡슐 중심이 벽을 넘어가는 것만 따로 막는다.
        //    벽과 나란히 대시할 때는 레이가 벽에 닿지 않으므로 방해받지 않는다
        float radius = capsule != null ? capsule.radius * transform.lossyScale.x : 0.5f;
        RaycastHit[] line = Physics.RaycastAll(rb.position, dashDir, step + radius + skin, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < line.Length; i++)
        {
            if (Blocked(line[i].collider) == false) continue;
            float d = line[i].distance - radius - skin;
            if (d < allowed) allowed = d;
        }

        return Mathf.Max(0f, allowed);
    }

    // 몹과 자기 자신은 대시를 막지 않는다
    bool Blocked(Collider col)
    {
        if (col.transform.root == transform.root) return false;
        if (col.GetComponentInParent<EnemyAI>() != null) return false;
        return true;
    }


public void CancelDash()
    {
        // drops the remaining dash so it can't resume later (e.g. after an airborne);
        // the cooldown is deliberately left running
        dashTimeLeft = 0f;
        if (mover != null) mover.movementLocked = false;
    }
}
