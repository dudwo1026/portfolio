using UnityEngine;

// Lobs an AoE shell at the player on a fixed interval. The arc is solved for a fixed flight time
// under normal gravity, so launch speed/angle follow from the distance instead of being pinned.
[RequireComponent(typeof(EnemyAI))]
public class EliteRangedAttack : MonoBehaviour, IElitePatternVisual
{
    public float attackInterval = 4f;
    public float range = 20f;
    public float flightTimeAtMaxRange = 2f; // launch -> impact at full range; scales down with distance
    public float minFlightTime = 0.15f;
    public float gravity = 9.81f;
    public float projectileScale = 1f;   // roughly the player's width
    public float damagePercent = 10f;    // 플레이어 최대 체력 대비 %
    public float aoeRadius = 2.5f;       // diameter 5 blast at the impact point

    [Header("Special shot")]
    public int specialEveryNShots = 4;   // every 4th attack
    public float specialAoeRadius = 3.5f;
    public float blindDuration = 10f;
    public Material specialMaterial;     // dark grey, so the blind shell reads differently
    public float launchHeight = 1f;

    [Header("Aim prediction")]
    public float predictionStrength = 1f; // 0 = aim where the player is, 1 = full lead
    public int predictionIterations = 3;  // flight time depends on the lead, so solve it a few times

    EnemyAI ai;
    Renderer rend;
    TopDownMove playerMover;
    float nextShotTime;
    int shotCount;

    // 사격 모션 전체 길이. 모션이 시작되고 fireAtProgress 지점에서 실제로 발사한다 (EnemySpriteAnimator)
    public float shotMotionTime = 0.6f;
    // 4장 중 3번째 장(attack3)이 시작되는 지점 = 2/4. 장수를 바꾸면 이것도 맞춘다
    public float fireAtProgress = 0.5f;
    float lastShotTime = -999f;
    float fireAt = -1f; // 발사 예약 시각. -1이면 예약 없음
    public bool PatternActive { get { return Time.time - lastShotTime < shotMotionTime; } }
    public float PatternProgress { get { return Mathf.Clamp01((Time.time - lastShotTime) / shotMotionTime); } }

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
    }

void FixedUpdate()
    {
        if (ai.IsStunned || ai.patternDriving) { fireAt = -1f; return; } // 선딜 중 기절하면 취소
        Transform p = ai.PlayerTransform;
        if (p == null) return;

        // 예약된 발사. 조준은 이 순간에 다시 잡는다 — 선딜 동안 플레이어가 움직였으므로
        if (fireAt >= 0f)
        {
            if (Time.time < fireAt) return;
            fireAt = -1f;
            Fire(PredictAimPoint(p));
            return;
        }

        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextShotTime) return;

        Vector3 flat = p.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist > range || dist < 0.01f) return;

        // 모션을 먼저 시작하고, attack3이 나오는 순간에 쏜다
        nextShotTime = Time.time + attackInterval;
        lastShotTime = Time.time;
        fireAt = Time.time + shotMotionTime * fireAtProgress;
    }

void Fire(Vector3 aimPoint)
    {
        Vector3 flat = aimPoint - transform.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance < 0.01f) return;
        Vector3 dir = flat / distance;

        shotCount++;
        bool special = specialEveryNShots > 0 && shotCount % specialEveryNShots == 0;
        float radius = special ? specialAoeRadius : aoeRadius;

        Vector3 start = transform.position + Vector3.up * launchHeight + dir * 0.8f;

        // flight time is proportional to distance (2s at max range), which keeps the horizontal speed constant
        float time = FlightTimeFor(distance);
        float horizontal = distance / time;
        float vertical = (aimPoint.y - start.y) / time + 0.5f * gravity * time;
        Vector3 velocity = dir * horizontal + Vector3.up * vertical;

        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = name + (special ? "_SpecialProjectile" : "_Projectile");
        proj.transform.position = start;
        proj.transform.localScale = Vector3.one * projectileScale * (special ? 1.4f : 1f);
        if (special && specialMaterial != null) proj.GetComponent<Renderer>().sharedMaterial = specialMaterial;
        else if (rend != null) proj.GetComponent<Renderer>().sharedMaterial = rend.sharedMaterial; // matches the shooter

        Collider col = proj.GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody prb = proj.AddComponent<Rigidbody>();
        prb.useGravity = false;
        prb.isKinematic = true; // the shell integrates its own arc

        EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
        ep.Launch(velocity, gravity, ai.PercentOfPlayerMaxHealth(damagePercent), radius, time, gameObject);
        if (special) ep.SetBlind(blindDuration);

        transform.rotation = Quaternion.LookRotation(dir);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
    }


float FlightTimeFor(float distance)
    {
        float time = range > 0.01f ? flightTimeAtMaxRange * (distance / range) : flightTimeAtMaxRange;
        return Mathf.Max(time, minFlightTime);
    }


Vector3 PredictAimPoint(Transform p)
    {
        if (playerMover == null) playerMover = p.GetComponent<TopDownMove>();

        Vector3 velocity = Vector3.zero;
        if (playerMover != null) velocity = playerMover.MoveDirection * playerMover.CurrentSpeed * predictionStrength;

        Vector3 aim = p.position;
        for (int i = 0; i < predictionIterations; i++)
        {
            Vector3 flat = aim - transform.position;
            flat.y = 0f;
            aim = p.position + velocity * FlightTimeFor(flat.magnitude);
            aim = ClampToRange(aim); // never lead past our own range (the lead would otherwise feed itself)
        }
        return aim;
    }


Vector3 ClampToRange(Vector3 aim)
    {
        Vector3 flat = aim - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= range) return aim;

        Vector3 clamped = transform.position + flat.normalized * range;
        clamped.y = aim.y;
        return clamped;
    }
}
