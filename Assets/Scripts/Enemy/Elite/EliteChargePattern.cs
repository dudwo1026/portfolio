using System.Collections.Generic;
using UnityEngine;

// Rush pattern: on first player detection, charge in a straight line until an obstacle stops it.
// Attach only to elites that should have it - enemies without this component have no charge parameters at all.
[RequireComponent(typeof(EnemyAI))]
public class EliteChargePattern : MonoBehaviour, IElitePatternVisual
{
    public float cooldown = 20f;           // 돌진 재사용 대기. 시전 시작 시점부터 잰다
    public float speedMultiplier = 3.5f;   // x EnemyAI.attackSpeed
    public float damagePercent = 15f;      // 플레이어 최대 체력 대비 %
    public float stunOnMiss = 5f;          // crashed without hitting the player
    public float stunOnHit = 3f;           // crashed after the charge connected
    public float knockbackForce = 3f;
    public float knockbackDuration = 0.4f;
    // 돌진 중 플레이어 쪽으로 트는 최대 각속도(도/초). 0이면 예전처럼 완전 직진.
    // 낮게 잡아야 '진행방향과 수직으로 빠지면 피해진다'가 성립한다 —
    // 플레이어가 거리 d에서 수직으로 달리면 각속도 v/d 가 필요하므로,
    // 이 값보다 가까이서 옆으로 빠지면 돌진이 각도를 못 따라온다.
    // 플레이어 속도 6 / 이 값 30 기준으로 11.5유닛 안쪽에서 수직 회피가 성립한다
    public float turnDegreesPerSecond = 30f;

    [Header("몹 관통")]
    // 다른 몹은 장애물로 치지 않는다. 예전에는 일반몹 한 마리만 앞을 지나가도 돌진이
    // 1.2유닛 만에 끝나고 5초 자기 기절이 걸렸다 (측정으로 확인)
    public float mobDamagePercent = 15f;      // 맞은 몹의 '최대' 체력 대비 %
    public float mobAirborneDuration = 0.7f;  // 뜨고 내려오는 시간. 착지 경직은 없다
    public float mobAirborneHeight = 1.5f;

    // 선딜은 일부러 두지 않는다. 6시 방향 기습이 문제였지만 그건 EliteThreatIndicator(화면 밖
    // 정예 표시기)가 위치를 미리 알려주는 것으로 해결했다. 표시기를 보고 미리 옆으로 흘리면
    // 위의 선회 한계 때문에 돌진은 각도를 못 따라온다 — 대처 수단은 이미 열려 있다

    EnemyAI ai;
    Rigidbody rb;

    float nextChargeTime;
    bool charging;

    // 돌진 자세를 그리는 데 쓴다 (EnemySpriteAnimator)
    public bool IsCharging { get { return charging; } }
    public bool PatternActive { get { return charging; } }
    // 벽에 박을 때까지 계속되므로 길이를 미리 알 수 없다. -1 = 고정 속도로 1회 재생 후 유지
    public float PatternProgress { get { return -1f; } }
    bool hitPlayer;
    Vector3 chargeDir;
    readonly HashSet<EnemyAI> struckThisCharge = new HashSet<EnemyAI>();

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable() { ai.onPlayerDetected += BeginCharge; }

    void OnDisable()
    {
        ai.onPlayerDetected -= BeginCharge;
        if (charging) EndCharge();
    }

    // 발견 직후 첫 돌진 외에도, 추격 중이면 쿨타임마다 다시 돌진한다
    void Update()
    {
        if (charging || ai.IsStunned) return;
        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextChargeTime) return;
        BeginCharge();
    }

    void BeginCharge()
    {
        if (charging) return;
        if (Time.time < nextChargeTime) return;

        Transform p = ai.PlayerTransform;
        if (p == null) return;

        Vector3 dir = p.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        nextChargeTime = Time.time + cooldown; // 시전 시작 기준. 맞든 빗나가든 동일
        charging = true;
        hitPlayer = false;
        struckThisCharge.Clear();
        chargeDir = dir.normalized;
        transform.rotation = Quaternion.LookRotation(chargeDir);
        ai.patternDriving = true;
    }

    void FixedUpdate()
    {
        if (!charging || ai.IsStunned) return;

        // 완만한 유도. 매 프레임 제한된 각도만큼만 플레이어 쪽으로 튼다
        Transform p = ai.PlayerTransform;
        if (p != null && turnDegreesPerSecond > 0f)
        {
            Vector3 want = p.position - rb.position;
            want.y = 0f;
            if (want.sqrMagnitude > 0.0001f)
            {
                chargeDir = Vector3.RotateTowards(chargeDir, want.normalized,
                    turnDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                transform.rotation = Quaternion.LookRotation(chargeDir);
            }
        }

        // keeps going until OnCollisionEnter hits an obstacle (maps are fully walled, so it always ends)
        float step = ai.attackSpeed * speedMultiplier * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + chargeDir * step);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!charging) return;

        Transform p = ai.PlayerTransform;
        if (p != null && collision.gameObject == p.gameObject)
        {
            // charge connected: damage + launch the player, but keep barreling on until an obstacle stops it
            PlayerAirborne airborne = collision.gameObject.GetComponent<PlayerAirborne>();
            if (airborne != null && airborne.IsActive) return; // already launched by this charge

            if (ai.PlayerHealth != null) ai.PlayerHealth.TakeDamage(ai.PercentOfPlayerMaxHealth(damagePercent));
            if (airborne != null) airborne.Launch();
            hitPlayer = true;
            return;
        }

        // 다른 몹은 뚫고 지나간다. 피해를 주고 띄우되 돌진은 멈추지 않는다
        EnemyAI other = collision.gameObject.GetComponentInParent<EnemyAI>();
        if (other != null && other != ai)
        {
            // 한 번의 돌진에서 같은 몹을 여러 번 치지 않는다.
            // 관통 중에는 접촉이 끊겼다 다시 붙으며 OnCollisionEnter가 여러 번 들어온다
            if (struckThisCharge.Add(other))
            {
                Health oh = other.GetComponent<Health>();
                if (oh != null) oh.TakeDamage(oh.maxHealth * mobDamagePercent * 0.01f);
                other.ApplyAirborne(mobAirborneHeight, mobAirborneDuration);
            }
            return;
        }

        // only wall-like (roughly horizontal) contacts count as obstacles, not the ground
        bool hitObstacle = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.y) < 0.5f) { hitObstacle = true; break; }
        }
        if (!hitObstacle) return;

        EndCharge();
        ai.ApplyStun(hitPlayer ? stunOnHit : stunOnMiss);
        ai.ApplyKnockback(-chargeDir, knockbackForce, knockbackDuration);
    }

    void EndCharge()
    {
        charging = false;
        ai.patternDriving = false;
    }
}
