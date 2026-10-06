using UnityEngine;

// Eruption pattern: detect -> lock the direction -> stand still for a wind-up -> the ground erupts.
// The blast sweeps a 45-degree sector of radius 14 outward over 0.3s, in the LOCKED direction
// (turning away during the wind-up does not save the player; stepping out of the sector does).
[RequireComponent(typeof(EnemyAI))]
public class EliteEruptionPattern : MonoBehaviour
{
    public float cooldown = 13f;         // counted from the wind-up start, hit or miss
    public float windupDuration = 0.5f;  // 방향 확정 후 정지 시간
    public float radius = 14f;
    public float coneAngle = 45f;        // full width of the forward sector
    public float spreadDuration = 0.3f;  // time the front takes to reach the far edge
    public float damagePercent = 15f;    // 플레이어 최대 체력 대비 %
    public float triggerRange = 14f;
    public float weakenDuration = 3f;  // 허약(대시 불가) + 슬로우(이동속도 50%) 지속시간

    public Material castMaterial;        // shared "casting" tint
    public Color eruptionColor = new Color(0.95f, 0.45f, 0.05f, 1f);
    public float markerLifetime = 0.4f;
    public int markersPerStep = 4;
    public float markerScale = 1.3f;

    enum Phase { Idle, Windup, Erupt }

    EnemyAI ai;
    Renderer rend;
    Material normalMaterial;
    Material markerMaterial;

    Phase phase = Phase.Idle;
    float phaseTimer;
    Vector3 lockedForward;   // 2단계에서 확정된 방향 — 이후 갱신되지 않음
    Vector3 lockedOrigin;
    float prevFront;
    bool playerHit;
    float groundY;
    float nextCastTime;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
    }

    void OnDisable()
    {
        if (phase != Phase.Idle) EndPattern();
    }

    void FixedUpdate()
    {
        if (phase == Phase.Windup) { UpdateWindup(); return; }
        if (phase == Phase.Erupt) { UpdateEruption(); return; }

        // 1. 플레이어 감지
        if (ai.IsStunned) return;
        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextCastTime) return;

        Transform p = ai.PlayerTransform;
        if (p == null) return;

        Vector3 toPlayer = p.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > triggerRange) return;

        Begin(p);
    }

    // 2. 패턴 실행 방향 확정 + 3. 정지 시작
    void Begin(Transform p)
    {
        phase = Phase.Windup;
        phaseTimer = windupDuration;
        nextCastTime = Time.time + cooldown;

        FaceTarget(p);
        lockedForward = transform.forward;
        lockedOrigin = transform.position;

        ai.patternDriving = true; // 정지: 시전 내내 제자리
        groundY = rend != null ? rend.bounds.min.y : transform.position.y;
        SetCastVisual(true);
    }

    void UpdateWindup()
    {
        if (ai.IsStunned) { EndPattern(); return; } // 스턴당하면 시전 취소

        phaseTimer -= Time.fixedDeltaTime;
        if (phaseTimer > 0f) return;

        // 4. 패턴 실행
        phase = Phase.Erupt;
        phaseTimer = 0f;
        prevFront = 0f;
        playerHit = false;
    }

    void UpdateEruption()
    {
        phaseTimer += Time.fixedDeltaTime;
        float front = Mathf.Min(radius, radius * (phaseTimer / spreadDuration));

        SpawnMarkers(prevFront, front);

        if (!playerHit)
        {
            Transform p = ai.PlayerTransform;
            // 엄폐물에 막혀 있으면 통과시키지 않는다 (막힌 동안은 계속 무효)
            if (p != null && InCone(p.position, front) && ai.HasLineOfSight(lockedOrigin, p))
            {
                Health h = ai.PlayerHealth;
                if (h != null) h.TakeDamage(ai.PercentOfPlayerMaxHealth(damagePercent));

                PlayerWeaken weaken = p.GetComponent<PlayerWeaken>(); // 허약(대시 봉인)
                if (weaken != null) weaken.Apply(weakenDuration);

                PlayerSlow slow = p.GetComponent<PlayerSlow>();     // 슬로우(감속) 동시 부여
                if (slow != null) slow.Apply(weakenDuration);

                playerHit = true;
            }
        }

        prevFront = front;
        if (phaseTimer >= spreadDuration) EndPattern();
    }

    bool InCone(Vector3 point, float front)
    {
        Vector3 flat = point - lockedOrigin;
        flat.y = 0f;
        if (flat.magnitude > front) return false;
        return Vector3.Angle(lockedForward, flat) <= coneAngle * 0.5f;
    }

    void SpawnMarkers(float from, float to)
    {
        if (to <= from) return;
        if (markerMaterial == null)
        {
            markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            markerMaterial.color = eruptionColor;
        }

        float half = coneAngle * 0.5f;
        for (int i = 0; i < markersPerStep; i++)
        {
            float dist = Random.Range(from, to);
            float angle = Random.Range(-half, half);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * lockedForward;
            Vector3 pos = lockedOrigin + dir * dist;
            pos.y = groundY;

            // root object: the elite's own scale is non-uniform and would squash the marker
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Eruption";
            Destroy(marker.GetComponent<Collider>()); // visual only
            marker.transform.position = pos;
            marker.transform.localScale = new Vector3(markerScale, 0.15f, markerScale);
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            Destroy(marker, markerLifetime);
        }
    }

    void EndPattern()
    {
        phase = Phase.Idle;
        ai.patternDriving = false;
        SetCastVisual(false);
    }

    void FaceTarget(Transform p)
    {
        Vector3 dir = p.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    void SetCastVisual(bool on)
    {
        if (rend == null) return;
        if (on && castMaterial != null) rend.sharedMaterial = castMaterial;
        else if (!on && normalMaterial != null) rend.sharedMaterial = normalMaterial;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Vector3 l = Quaternion.Euler(0f, -coneAngle * 0.5f, 0f) * transform.forward * radius;
        Vector3 r = Quaternion.Euler(0f, coneAngle * 0.5f, 0f) * transform.forward * radius;
        Gizmos.DrawLine(transform.position, transform.position + l);
        Gizmos.DrawLine(transform.position, transform.position + r);
        Gizmos.DrawLine(transform.position + l, transform.position + r);
    }
}
