using UnityEngine;

// 이 게임의 플레이어는 물리 중력을 쓰지 않는다. TopDownMove가 매 FixedUpdate마다 속도를 지우기 때문에
// useGravity를 켜도 떨어지지 않는다. 대신 발밑 지면을 찾아 y를 직접 맞춘다.
// 연석·계단은 걸어서 오르고, 발밑이 비면 가속하며 떨어진다.
[DefaultExecutionOrder(-200)] // TopDownMove/PlayerDash이 rb.position을 읽기 전에 y를 보정해 둔다
[RequireComponent(typeof(Rigidbody))]
public class PlayerGroundSnap : MonoBehaviour
{
    public float maxStepUp = 1.2f;    // 이만큼까지는 걸어서 올라간다 (인도 연석이 1.02)
    public float gravity = 25f;       // 낙하 가속도
    public float maxFallSpeed = 30f;
    public float probeExtra = 0.5f;   // 발밑을 이만큼 더 내려다본다
    public float radiusScale = 0.9f;  // 벽에 몸이 끼지 않도록 캡슐보다 살짝 얇게 훑는다
    public float stepAhead = 0.35f;   // 진행 방향으로 이만큼 더 앞을 짚어 오를 단을 찾는다
    public float stepUpSpeed = 8f;    // 올라설 때 초당 이만큼씩 올린다 (0이면 한 프레임에 순간이동)

    public bool IsGrounded { get; private set; }
    public float FallSpeed { get { return fallSpeed; } }

    Rigidbody rb;
    CapsuleCollider capsule;
    PlayerAirborne airborne;
    TopDownMove mover;
    float fallSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        airborne = GetComponent<PlayerAirborne>();
        mover = GetComponent<TopDownMove>();
    }

    void FixedUpdate()
    {
        // 띄워지는 상태이상은 y를 자기가 관리한다. 끼어들면 서로 싸운다
        if (airborne != null && airborne.IsActive) { fallSpeed = 0f; IsGrounded = true; return; }
        if (capsule == null) return;

        float half = capsule.height * 0.5f * transform.lossyScale.y;
        float radius = capsule.radius * transform.lossyScale.x * radiusScale;
        Vector3 pos = rb.position;

        float fallStep = fallSpeed * Time.fixedDeltaTime;
        Vector3 from = pos + Vector3.up * (maxStepUp + radius);
        float dist = maxStepUp + radius + half + fallStep + probeExtra;

        float groundY = float.NegativeInfinity;
        RaycastHit[] hits = Physics.SphereCastAll(from, radius, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsGround(hits[i].collider)) continue;
            // 시작부터 겹쳐 있던 콜라이더는 distance=0 에 point=(0,0,0) 쓰레기값이 온다.
            // 이걸 그대로 쓰면 '지면 높이 0' 으로 읽혀 옥상에서 지상까지 떨어진다
            if (hits[i].distance <= 0f) continue;
            // 발밑보다 너무 높이 솟은 것은 '올라설 곳'이 아니라 벽이다
            float top = hits[i].point.y;
            if (top > pos.y - half + maxStepUp + 0.001f) continue;
            if (top > groundY) groundY = top;
        }

        // 구(球) 탐침은 반지름 때문에 옆에 붙은 난간·장애물의 윗면을 먼저 집을 때가 있다.
        // 그러면 '발밑에 지면 없음'으로 읽혀 옥상에서 그대로 떨어진다.
        // 모서리에 끼었을 때 실제로 이런 일이 난다. 얇은 중심 레이로 한 번 더 짚는다
        if (float.IsNegativeInfinity(groundY))
        {
            RaycastHit[] thin = Physics.RaycastAll(pos, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < thin.Length; i++)
            {
                if (!IsGround(thin[i].collider)) continue;
                float top = thin[i].point.y;
                if (top > pos.y - half + maxStepUp + 0.001f) continue;
                if (top > groundY) groundY = top;
            }
        }

        // 연석·계단 오르기.
        // 캡슐은 반지름만큼 벽에 막혀 더 붙지 못하는데 탐침은 그보다 얇아서, 제자리에서 아래로만
        // 훑으면 올라설 윗면에 영원히 닿지 못한다 (인도 연석 앞에서 그대로 멈춰 섰다).
        // 그래서 진행 방향으로 몸통 반지름만큼 더 앞을 한 번 더 짚는다.
        // 올릴 때만 쓰고 내릴 때는 쓰지 않는다 — 낭떠러지 앞에서 미리 떨어지면 안 되니까
        groundY = Mathf.Max(groundY, StepUpTargetAhead(pos, half));

        if (groundY > float.NegativeInfinity)
        {
            float targetY = groundY + half;
            if (targetY >= pos.y - 0.001f)
            {
                // 지면에 서 있거나 한 단 올라선다.
                // 연석 한 단(1.0)을 한 프레임에 순간이동시키면 화면이 튄다. 속도를 제한해 걸어 올라가게 한다
                float y = targetY;
                if (stepUpSpeed > 0f)
                {
                    float maxRise = stepUpSpeed * Time.fixedDeltaTime;
                    if (y > pos.y + maxRise) y = pos.y + maxRise;
                }
                SetY(y);
                fallSpeed = 0f;
                IsGrounded = true;
                return;
            }

            // 지면이 아래에 있다 -> 낙하. 목표를 지나치지 않게 자른다
            fallSpeed = Mathf.Min(fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
            float next = pos.y - fallSpeed * Time.fixedDeltaTime;
            if (next <= targetY) { SetY(targetY); fallSpeed = 0f; IsGrounded = true; }
            else { SetY(next); IsGrounded = false; }
            return;
        }

        // 탐지 범위 안에 지면이 없다 -> 계속 떨어진다
        fallSpeed = Mathf.Min(fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
        SetY(pos.y - fallSpeed * Time.fixedDeltaTime);
        IsGrounded = false;
    }

    // 진행 방향 바로 앞에 올라설 만한 단이 있으면 그 윗면 높이를 돌려준다.
    // 오를 수 없거나(너무 높음) 움직이지 않는 중이면 NegativeInfinity
    float StepUpTargetAhead(Vector3 pos, float half)
    {
        if (stepAhead <= 0f || mover == null) return float.NegativeInfinity;

        Vector3 dir = mover.MoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return float.NegativeInfinity;
        dir.Normalize();

        float bodyRadius = capsule.radius * transform.lossyScale.x;
        float probeRadius = bodyRadius * 0.5f;   // 벽 옆면을 긁지 않도록 얇게
        float feet = pos.y - half;

        Vector3 ahead = pos + dir * (bodyRadius + stepAhead);
        Vector3 from = new Vector3(ahead.x, feet + maxStepUp + probeRadius, ahead.z);
        float dist = maxStepUp + probeRadius + probeExtra;

        float best = float.NegativeInfinity;
        RaycastHit[] hits = Physics.SphereCastAll(from, probeRadius, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsGround(hits[i].collider)) continue;
            if (hits[i].distance <= 0f) continue;          // 겹친 콜라이더의 쓰레기값 제외
            float top = hits[i].point.y;
            if (top > feet + maxStepUp + 0.001f) continue; // 벽이지 계단이 아니다
            // 발 높이와 같은 지면도 '지지대'로 인정한다.
            // 올라선 직후에는 본체 탐침이 아직 윗면에 닿지 못하는데, 여기서 같은 높이를 안 돌려주면
            // 떨어뜨렸다가 다시 올리기를 반복해 위아래로 떤다.
            // 더 낮은 곳은 무시한다 — 내리막을 미리 잡아 공중에 떠 있으면 안 되니까
            if (top < feet - 0.05f) continue;
            if (top > best) best = top;
        }
        return best;
    }

    // 자기 자신과 몹은 바닥으로 치지 않는다 (몹 위에 올라서면 곤란하다)
    bool IsGround(Collider col)
    {
        if (col.transform.root == transform.root) return false;
        if (col.GetComponentInParent<EnemyAI>() != null) return false;
        return true;
    }

    // y만 보정한다.
    // transform.position까지 같이 쓰면 트랜스폼 동기화가 일어나면서, 같은 프레임에 뒤따르는
    // TopDownMove/PlayerDash의 MovePosition이 통째로 무시된다.
    // (그래서 연석을 내려갈 때 수평 이동이 0.28초간 멈추고 수직으로만 떨어졌다)
    void SetY(float y)
    {
        Vector3 p = rb.position;
        if (Mathf.Abs(p.y - y) < 0.0001f) return;
        p.y = y;
        rb.position = p;
    }
}
