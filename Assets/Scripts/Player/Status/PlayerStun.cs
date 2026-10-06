using UnityEngine;

// 스턴 + 넉백. The shove always uses the same speed profile (defined over the full knockback
// distance) and simply gets cut short by an obstacle. The shorter the slide, the longer the stun
// and the bigger the recoil - both scale inversely with the distance covered, up to +maxBonusRatio.
public class PlayerStun : MonoBehaviour
{
    public float slideDuration = 0.4f;   // time to cover a FULL, unobstructed knockback
    public float bounceDistance = 0.5f;     // recoil off a wall when the shove was barely blocked
    public float bounceMaxDistance = 3f;    // ...growing to this when it was blocked immediately
    public float bounceDuration = 0.7f;     // fixed: the recoil always takes this long, whatever its distance

    public bool IsActive => timer > 0f;
    public float LastTravelDistance { get; private set; }
    public float LastStunDuration { get; private set; }
    public float LastBounceDistance { get; private set; }
    public float LastImpactDamage { get; private set; }

    float timer;
    float pendingImpactDamage;

    bool sliding;
    float slideTime;
    float fullDistance;   // the profile is always built for this, blocked or not
    float maxTravel;      // where an obstacle cuts it short
    Vector3 slideStart;
    Vector3 slideDir;

    float bounceTimer;
    Vector3 impactPos;
    Vector3 bounceEnd;

    Rigidbody rb;
    CapsuleCollider capsule;
    TopDownMove mover;
    PlayerCombat combat;
    PlayerDash dash;
    PlayerConsumables consumables;
    PlayerBound bound;
    Health health;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        mover = GetComponent<TopDownMove>();
        combat = GetComponent<PlayerCombat>();
        dash = GetComponent<PlayerDash>();
        consumables = GetComponent<PlayerConsumables>();
        bound = GetComponent<PlayerBound>();
        health = GetComponent<Health>();
    }

    public void Apply(Vector3 sourcePosition, float stunDuration, float knockbackDistance, float maxBonusRatio)
    {
        Apply(sourcePosition, stunDuration, knockbackDistance, maxBonusRatio, 0f);
    }

    // impactDamageAtFullBlock: 벽에 부딪혔을 때 줄 대미지의 최대치.
    // 실제로는 '밀려나지 못한 비율'만큼만, 충돌하는 순간에 들어간다
    public void Apply(Vector3 sourcePosition, float stunDuration, float knockbackDistance, float maxBonusRatio, float impactDamageAtFullBlock)
    {
        Vector3 dir = transform.position - sourcePosition;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

        float travel = SweepDistance(dir, knockbackDistance);
        LastTravelDistance = travel;

        // inverse to the distance actually covered: blocked immediately => full bonus, full slide => none
        float blockedRatio = knockbackDistance > 0f ? 1f - Mathf.Clamp01(travel / knockbackDistance) : 0f;
        float bonus = 1f + blockedRatio * maxBonusRatio;

        LastStunDuration = stunDuration * bonus;
        timer = LastStunDuration;

        sliding = true;
        slideTime = 0f;
        fullDistance = knockbackDistance;
        maxTravel = travel;
        slideStart = rb.position;
        slideDir = dir;

        bool blocked = travel < knockbackDistance - 0.01f;
        if (blocked)
        {
            float want = Mathf.Lerp(bounceDistance, bounceMaxDistance, blockedRatio);
            LastBounceDistance = Mathf.Min(want, SweepDistance(-dir, want)); // never recoil into geometry
            pendingImpactDamage = Mathf.Round(impactDamageAtFullBlock * blockedRatio);
        }
        else
        {
            LastBounceDistance = 0f;
            pendingImpactDamage = 0f; // 끝까지 밀려났으면 부딪힌 게 없다
        }
        LastImpactDamage = pendingImpactDamage;
        bounceTimer = 0f;

        if (dash != null) dash.CancelDash(); // a disabled dash would otherwise resume afterwards
        if (mover != null) mover.movementLocked = true;
        if (combat != null) combat.enabled = false;
        if (dash != null) dash.enabled = false;
        if (consumables != null) consumables.enabled = false;
        rb.linearVelocity = Vector3.zero;
    }

    float SweepDistance(Vector3 dir, float maxDistance)
    {
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float radius = (capsule != null ? capsule.radius : 0.5f) * scale;

        RaycastHit[] hits = Physics.SphereCastAll(rb.position, radius, dir, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        float best = maxDistance;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.gameObject == gameObject) continue;
            if (hits[i].collider.GetComponent<EnemyAI>() != null) continue; // enemies aren't obstacles
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return Mathf.Max(0f, best);
    }

    void FixedUpdate()
    {
        if (timer <= 0f) return;

        if (sliding)
        {
            slideTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(slideTime / slideDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out, always over the full distance
            float dist = fullDistance * eased;

            if (dist >= maxTravel)
            {
                dist = maxTravel;
                sliding = false;
                impactPos = slideStart + slideDir * maxTravel;
                bounceEnd = impactPos - slideDir * LastBounceDistance;
                bounceTimer = LastBounceDistance > 0f ? bounceDuration : 0f;

                // 벽에 닿는 그 순간에 충돌 대미지가 들어간다 (넉백 대미지 다음 순서)
                if (pendingImpactDamage > 0f)
                {
                    if (health != null) health.TakeDamage(pendingImpactDamage);
                    pendingImpactDamage = 0f;
                }
            }
            else if (t >= 1f)
            {
                sliding = false;
            }

            rb.MovePosition(slideStart + slideDir * dist);
            rb.linearVelocity = Vector3.zero;
        }
        else if (bounceTimer > 0f)
        {
            bounceTimer -= Time.fixedDeltaTime;
            float t = 1f - Mathf.Clamp01(bounceTimer / bounceDuration);
            float eased = 1f - (1f - t) * (1f - t);
            rb.MovePosition(Vector3.Lerp(impactPos, bounceEnd, eased));
            rb.linearVelocity = Vector3.zero;
        }

        timer -= Time.fixedDeltaTime;
        if (timer <= 0f) End();
    }

    void End()
    {
        timer = 0f;
        sliding = false;
        bounceTimer = 0f;
        rb.linearVelocity = Vector3.zero;

        bool stillBound = bound != null && bound.IsActive; // don't hand control back while 구속 is up
        if (mover != null) mover.movementLocked = stillBound;
        if (combat != null) combat.enabled = true;
        if (dash != null) dash.enabled = !stillBound;
        if (consumables != null) consumables.enabled = true;
    }
}
