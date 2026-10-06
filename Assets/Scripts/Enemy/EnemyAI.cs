using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Wander, Attack }

public class EnemyAI : MonoBehaviour
{
    public float wanderSpeed = 2.5f;
    public float attackSpeed = 5f;
    public float wanderDecisionInterval = 4f;
    public float wanderMoveChance = 0.75f;
    public float wanderConeAngle = 300f;
    public float avoidDistance = 1.5f;   // 배회 중 전방을 이만큼 미리 살핀다
    public float avoidRadius = 0.4f;     // 몸통 폭만큼 두껍게 살핀다
    public int wanderDirectionTries = 6; // 막혔을 때 다시 골라보는 횟수
    public float wanderArriveDistance = 2f; // 목적지에 이만큼 가까워지면 다음 목적지를 고른다
    public float minFirstWanderDistance = 25f; // 첫 목적지는 스폰 지점에서 이만큼 이상 떨어뜨린다

    [Header("혼잡 회피")]
    public float crowdRadius = 3f;          // 이 반경 안에
    public int crowdLimit = 6;              // 자기 말고 이만큼 몰려 있으면 자리를 뜬다
    public float crowdCheckInterval = 0.5f; // 100마리가 매 프레임 세면 낭비라 주기적으로만 검사
    public float crowdEscapeDistance = 12f;

    [Header("턱 넘기")]
    // 리지드바디를 수평으로만 밀면 인도 연석(높이 1.0) 같은 수직 턱에 그대로 처박힌다.
    // 플레이어는 PlayerGroundSnap이 같은 일을 해 준다 (maxStepUp 1.2)
    public float maxStepUp = 1.2f;       // 이 높이까지는 걸어서 올라간다
    public float stepProbeHeight = 0.2f; // 발밑 기준 이만큼 더 위에서부터 훑는다

    [Header("우회 경로 추적")]
    // 직선으로 뚫려 있으면 예전처럼 곧장 간다. 담벼락 같은 게 끼어 있을 때만 내비메시 경로를 쓴다
    public bool usePathfinding = true;
    public float repathInterval = 0.35f;     // 경로 재계산 주기 (개체마다 시점을 흩는다)
    public float repathMoveThreshold = 2.5f; // 목표가 이만큼 움직이면 주기를 기다리지 않고 다시 계산
    public float cornerArriveDistance = 1f;  // 꺾임점에 이만큼 붙으면 다음 꺾임점으로
    public float pathClearRadius = 0.45f;    // 직선 판정을 몸통 굵기로 한다 (모서리를 긁지 않게)
    public float navSampleRadius = 4f;       // 내비메시 위의 가장 가까운 점을 찾는 허용 반경

    // 스포너가 지정해 주는 배회 구역(사분면). 지정되면 그 안의 임의 지점을 찍고 이동한다
    [System.NonSerialized] public bool hasWanderArea;
    [System.NonSerialized] public Bounds wanderArea;

    public void SetWanderArea(Bounds area)
    {
        wanderArea = area;
        hasWanderArea = true;
        wanderTimer = 0f; // 배정받자마자 목적지를 정한다
    }

    public float recognitionRadius = 15f;
    public float visionAngle = 180f;

    public float attackDamage = 10f;
    public float attackCooldown = 2f;
    public float attackWindup = 0.5f; // 휘두르기 시작 후 판정까지의 선딜레이. 이 사이에 피하면 헛친다
    public float attackRange = 1.8f; // shorter than player's melee reach (meleeRange 1.96 + meleeRadius 0.8 = 2.76)
    public float loseTargetGraceTime = 5f;
    public bool persistentAggro = false; // 정예몹: 한 번 발견하면 인식범위 밖으로 나가도 계속 추적
    public bool isElite = false;         // 처치 게이지 가산량 구분용

    // special patterns live in their own components (e.g. EliteChargePattern) and drive the enemy through the hooks below
    [System.NonSerialized] public bool patternDriving;
    [System.NonSerialized] public bool roofSpawned; // 옥상 스포너가 낸 몹만 보급키트를 떨군다
    [System.NonSerialized] public bool forcedAggro; // 도발: chase the player no matter the distance or sight
    public System.Action onPlayerDetected;
    public System.Action onMeleeHit; // raised when the player's melee connects (bullets don't raise it)


    public EnemyState State { get; private set; } = EnemyState.Wander;
    // 공격 모션을 시작한 시각. EnemySpriteAnimator가 선딜에 맞춰 그림을 펼치는 데 쓴다
    public float AttackMotionTime { get; private set; } = -999f;
    public bool IsStunned => stunTimer > 0f;
    public bool IsKnockedBack => knockbackTimer > 0f;
    // 밀려나는 방향(수평). 스프라이트가 끌려가는 쪽을 보게 하는 데 쓴다
    public Vector3 KnockbackDirection => knockbackVelocity;
    public bool IsAirborne => airborneTimer > 0f;
    public Transform PlayerTransform => player;
    public Health PlayerHealth => playerHealth;
    public Rigidbody Body => rb;



    Transform player;
    Health playerHealth;
    Health selfHealth;
    Rigidbody rb;
    // 몸통 치수. 일반몹은 CapsuleCollider, 정예는 BoxCollider를 쓰므로 콜라이더 종류를 가리지 않고
    // Awake에서 한 번만 재 둔다. 스케일이 런타임에 바뀌지 않으니 캐시해도 안전하다
    float halfHeight = 1f;
    float bodyRadius = 0.5f;
    float bodyCenterY;    // 콜라이더 center가 0이 아닐 때의 보정

    float wanderTimer;
    Vector3 wanderDir;
    Vector3 wanderTarget;
    bool firstDestinationPicked;
    float crowdTimer;

    NavMeshPath navPath;
    readonly Vector3[] corners = new Vector3[16];
    int cornerCount;
    int cornerIndex;
    bool pathValid;
    float repathTimer;
    Vector3 pathTarget;

    float attackCooldownTimer;
    float windupTimer = -1f;   // 0보다 크면 공격 시전 중
    float knockbackTimer;
    float knockbackDuration;
    Vector3 knockbackVelocity;
    float stunTimer;

    // 에어본. 플레이어(PlayerAirborne)와 달리 착지 후 경직이 없다 —
    // 몹은 내려오는 즉시 다시 움직인다
    float airborneTimer;
    float airborneDuration;
    float airborneHeight;
    float airborneGroundY;
    Vector3 airborneLockedPos;

    float loseTargetTimer;
    Vector3 lastKnownPosition;
    bool everDetected;

    // 총성 알림을 쏠 때마다 씬 전체를 뒤지지 않도록 살아 있는 개체를 들고 있는다
    static readonly System.Collections.Generic.List<EnemyAI> active = new System.Collections.Generic.List<EnemyAI>();

    void OnEnable() { active.Add(this); }
    void OnDisable() { active.Remove(this); }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        MeasureBody();
        selfHealth = GetComponent<Health>();
        GameObject playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            playerHealth = playerGO.GetComponent<Health>();
        }
        wanderTimer = 0f; // 스폰 직후 첫 프레임에 바로 방향을 정한다 (예전엔 4초간 가만히 서 있었음)
        crowdTimer = Random.Range(0f, crowdCheckInterval); // 개체마다 검사 시점을 흩어 한 프레임에 몰리지 않게
        navPath = new NavMeshPath();
        repathTimer = Random.Range(0f, repathInterval);    // 경로 계산도 같은 이유로 흩는다
        if (selfHealth != null) selfHealth.Damaged += OnDamaged;
    }

    void Start()
    {
        // 정예몹은 감지 절차 없이 생성되자마자 플레이어를 쫓는다.
        // (Awake가 아니라 Start인 이유: 패턴 컴포넌트들이 onPlayerDetected를 붙일 시간을 준다)
        if (isElite && player != null) EnterAttack();
    }

void OnDamaged()
    {
        if (selfHealth != null && selfHealth.CurrentHealth <= 0f)
        {
            KillGauge.ReportKill(isElite); // 처치 게이지 가산 (자폭 소멸은 체력 0을 거치지 않으므로 제외)
            if (roofSpawned) SupplyKit.TryDrop(transform.position);
            Destroy(gameObject);
            return;
        }
        if (State == EnemyState.Wander) EnterAttack();
    }

void EnterAttack()
    {
        State = EnemyState.Attack;
        everDetected = true;
        if (player != null) lastKnownPosition = player.position;
        loseTargetTimer = loseTargetGraceTime;

        if (onPlayerDetected != null) onPlayerDetected();
    }

void FixedUpdate()
    {
        if (stunTimer > 0f) stunTimer -= Time.fixedDeltaTime;

        // 에어본이 최우선. 넉백·기절보다 위에 둬야 y를 서로 다투지 않는다
        if (airborneTimer > 0f) { UpdateAirborne(); return; }

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0f)
                rb.linearVelocity = Vector3.zero;
            else
                rb.linearVelocity = knockbackVelocity * (knockbackTimer / knockbackDuration); // linear decay: ice-slide feel
            return;
        }

        if (stunTimer > 0f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (patternDriving) return; // a pattern component is moving this enemy

        if (player == null) return;

        if (State == EnemyState.Wander) UpdateWander();
        else UpdateAttack();
    }

    void UpdateWander()
    {
        if (forcedAggro) { EnterAttack(); return; } // 도발 중에는 배회하지 않는다

        wanderTimer -= Time.fixedDeltaTime;
        if (wanderTimer <= 0f) DecideWander();

        crowdTimer -= Time.fixedDeltaTime;
        if (crowdTimer <= 0f)
        {
            crowdTimer = crowdCheckInterval;
            CheckCrowd();
        }

        // 목적지가 있으면 도착 여부를 보고, 아니면 계속 그쪽으로 조향한다
        if (hasWanderArea && wanderDir.sqrMagnitude > 0.0001f)
        {
            Vector3 toTarget = wanderTarget - rb.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= wanderArriveDistance) DecideWander();
            else wanderDir = toTarget.normalized;
        }

        if (wanderDir.sqrMagnitude > 0.0001f)
        {
            // 벽에 처박은 채로 다음 결정(최대 4초)까지 비비지 않도록, 막히면 즉시 다시 고른다
            if (IsBlocked(wanderDir)) DecideWander();

            if (wanderDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(wanderDir);
                rb.MovePosition(StepUpAdjusted(rb.position + wanderDir * wanderSpeed * Time.fixedDeltaTime));
            }
        }

        // 배회 중에는 눈에 보여야만 발견한다 (벽/차량 뒤는 못 봄)
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= recognitionRadius && HasLineOfSight(transform.position, player)) EnterAttack();
    }

    // 좁은 곳에 몰려 있으면 무리의 반대편으로 빠진다
    void CheckCrowd()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        float sqrRadius = crowdRadius * crowdRadius;
        Vector3 myPos = rb.position;

        for (int i = 0; i < active.Count; i++)
        {
            EnemyAI other = active[i];
            if (other == null || other == this) continue;

            Vector3 d = other.transform.position - myPos;
            d.y = 0f;
            if (d.sqrMagnitude > sqrRadius) continue;

            sum += other.transform.position;
            count++;
        }

        if (count < crowdLimit) return;

        Vector3 away = myPos - (sum / count);
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) // 정확히 겹쳐 있으면 아무 방향이나
            away = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        away.Normalize();

        if (IsBlocked(away)) { DecideWander(); return; } // 그쪽이 막혔으면 평소 방식으로 다시 고른다

        Vector3 target = myPos + away * crowdEscapeDistance;
        if (hasWanderArea)
        {
            target.x = Mathf.Clamp(target.x, wanderArea.min.x, wanderArea.max.x);
            target.z = Mathf.Clamp(target.z, wanderArea.min.z, wanderArea.max.z);
        }
        target.y = myPos.y;

        wanderTarget = target;
        wanderDir = away;
        wanderTimer = wanderDecisionInterval;
    }

    void DecideWander()
    {
        wanderTimer = wanderDecisionInterval;

        if (Random.value >= wanderMoveChance)
        {
            wanderDir = Vector3.zero;
            return;
        }

        // 배회 구역이 있으면 그 안의 임의 지점을 목적지로 삼는다.
        // 방향만 무작위로 정하면 스폰 지점 근처를 맴돌게 되어 몹이 뭉친다
        if (hasWanderArea)
        {
            // 첫 목적지는 스폰 지점에서 멀리 잡는다. 균등 랜덤이면 바로 옆이 뽑히는 경우가 잦아
            // 스포너 주변에 그대로 눌러앉는다
            bool first = !firstDestinationPicked;
            float minDistSqr = first ? minFirstWanderDistance * minFirstWanderDistance : 0f;

            Vector3 bestPoint = Vector3.zero;
            Vector3 bestDir = Vector3.zero;
            float bestDistSqr = -1f;

            for (int i = 0; i < wanderDirectionTries; i++)
            {
                Vector3 p = new Vector3(
                    Random.Range(wanderArea.min.x, wanderArea.max.x),
                    rb.position.y,
                    Random.Range(wanderArea.min.z, wanderArea.max.z));

                Vector3 to = p - rb.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f) continue;

                Vector3 d = to.normalized;
                if (IsBlocked(d)) continue;

                if (to.sqrMagnitude >= minDistSqr)
                {
                    firstDestinationPicked = true;
                    wanderTarget = p;
                    wanderDir = d;
                    return;
                }

                // 조건에 못 미쳐도 가장 먼 후보는 들고 있는다 (구역이 좁으면 하나도 못 고른다)
                if (to.sqrMagnitude > bestDistSqr)
                {
                    bestDistSqr = to.sqrMagnitude;
                    bestPoint = p;
                    bestDir = d;
                }
            }

            if (bestDistSqr > 0f)
            {
                firstDestinationPicked = true;
                wanderTarget = bestPoint;
                wanderDir = bestDir;
                return;
            }
            // 사방이 막혔으면 아래의 방향 기반 폴백으로 넘어간다
        }

        float half = wanderConeAngle * 0.5f;
        for (int i = 0; i < wanderDirectionTries; i++)
        {
            Vector3 candidate = Quaternion.Euler(0f, Random.Range(-half, half), 0f) * transform.forward;
            if (!IsBlocked(candidate)) { wanderDir = candidate; return; }
        }

        // 사방이 막혔으면 뒤로 돌아선다. 그래도 막혔으면 이번 주기는 쉰다
        Vector3 back = -transform.forward;
        wanderDir = IsBlocked(back) ? Vector3.zero : back;
    }

    // 몸통 반높이/반지름을 콜라이더 종류를 가리지 않고 직접 잰다.
    //
    // 예전에는 CapsuleCollider만 보고, 없으면 half=1.0 / radius=0.5 라는 고정값으로 넘어갔다.
    // 그런데 정예몹은 전부 BoxCollider(1x1x1, 스케일 1.2/1.6/1.2)라 항상 그 고정값을 탔고,
    // 실제 반높이 0.8 대신 1.0을 쓰는 바람에 '발밑'을 실제보다 0.2 아래로 계산했다.
    // 그 결과 StepUpAdjusted가 평지에서도 rise=0.2를 오를 턱으로 착각해 매 FixedUpdate마다
    // 몸을 0.2 들어올렸고, 중력이 도로 끌어내리면서 이동 중에 땅에 박혀 떠는 것처럼 보였다.
    // (일반몹은 CapsuleCollider라 값이 맞아떨어져 멀쩡했다)
    void MeasureBody()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            halfHeight = cap.height * 0.5f * transform.lossyScale.y;
            bodyRadius = cap.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            bodyCenterY = cap.center.y * transform.lossyScale.y;
            return;
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
            bodyRadius = Mathf.Max(box.size.x * transform.lossyScale.x, box.size.z * transform.lossyScale.z) * 0.5f;
            bodyCenterY = box.center.y * transform.lossyScale.y;
            return;
        }

        // 그 밖의 콜라이더는 월드 AABB로 근사한다
        Collider any = GetComponent<Collider>();
        if (any != null)
        {
            Vector3 e = any.bounds.extents;
            halfHeight = e.y;
            bodyRadius = Mathf.Max(e.x, e.z);
            bodyCenterY = any.bounds.center.y - transform.position.y;
        }
    }

    // 수평으로 갈 목적지에서 발밑 지형이 조금 높으면, 그 턱 위로 올려놓는다.
    // 이게 없으면 리지드바디가 수직면에 그대로 막혀서, 인도 연석(1.0)조차 못 넘는다.
    // 내려가는 쪽은 손대지 않는다 — 중력이 알아서 처리한다.
    Vector3 StepUpAdjusted(Vector3 target)
    {
        if (maxStepUp <= 0f) return target;

        float half = halfHeight;
        float radius = bodyRadius;
        float feet = target.y + bodyCenterY - half;

        // 발 밑이 아니라 '몸통 앞'을 짚어야 한다. 캡슐 중심은 반지름 때문에 벽 안쪽으로 못 들어가므로,
        // 목적지에서만 아래로 훑으면 연석 윗면을 영원히 못 본다 (계속 차도 바닥만 짚는다)
        Vector3 move = target - rb.position;
        move.y = 0f;
        Vector3 ahead = target;
        if (move.sqrMagnitude > 0.000001f)
            ahead += move.normalized * (radius + 0.15f);

        Vector3 origin = new Vector3(ahead.x, feet + maxStepUp + stepProbeHeight, ahead.z);
        float probe = maxStepUp + stepProbeHeight + 0.5f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probe, ~0, QueryTriggerInteraction.Ignore);
        float ground = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.gameObject == gameObject) continue;
            if (player != null && c.gameObject == player.gameObject) continue;
            // 다른 몹 머리를 밟고 올라서지 않도록 제외한다
            if (c.GetComponentInParent<EnemyAI>() != null) continue;
            if (hits[i].point.y > ground) ground = hits[i].point.y;
        }
        if (float.IsNegativeInfinity(ground)) return target;

        float rise = ground - feet;
        if (rise > 0.05f && rise <= maxStepUp) target.y = ground + half - bodyCenterY;
        return target;
    }

    // 정적 지형만 막는 것으로 본다. 다른 적까지 장애물로 치면 무리지어 있을 때 매 프레임 방향을 다시 고르게 된다
    bool IsBlocked(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        Vector3 origin = rb.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, avoidRadius, dir, avoidDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject go = hits[i].collider.gameObject;
            if (go == gameObject) continue;
            if (player != null && go == player.gameObject) continue;
            if (hits[i].collider.GetComponent<EnemyAI>() != null) continue;
            return true;
        }
        return false;
    }

    void UpdateAttack()
    {
        bool sensed = CanSensePlayer();
        if (sensed)
        {
            lastKnownPosition = player.position;
            loseTargetTimer = loseTargetGraceTime;
        }
        else
        {
            loseTargetTimer -= Time.fixedDeltaTime;
            if (loseTargetTimer <= 0f)
            {
                State = EnemyState.Wander;
                wanderTimer = wanderDecisionInterval;
                wanderDir = Vector3.zero;
                return;
            }
        }

        Vector3 targetPos = sensed ? player.position : lastKnownPosition;
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist > 0.05f)
        {
            // 직선이 막혀 있으면 내비메시가 뽑은 우회 경로의 다음 꺾임점 쪽으로 돈다
            Vector3 dir = SteerTowards(targetPos, toTarget.normalized);
            transform.rotation = Quaternion.LookRotation(dir);
            if (dist > attackRange)
                rb.MovePosition(StepUpAdjusted(rb.position + dir * attackSpeed * Time.fixedDeltaTime));
        }

        attackCooldownTimer -= Time.fixedDeltaTime;

        if (windupTimer > 0f)
        {
            // 시전 중. 선딜레이가 끝나는 순간에 다시 판정한다 —
            // 그 사이에 플레이어가 사거리 밖으로 빠졌으면 헛친다
            windupTimer -= Time.fixedDeltaTime;
            if (windupTimer <= 0f)
            {
                windupTimer = -1f;
                if (sensed && dist <= attackRange && playerHealth != null)
                    playerHealth.TakeDamage(attackDamage);
            }
        }
        else if (sensed && dist <= attackRange && attackCooldownTimer <= 0f)
        {
            attackCooldownTimer = attackCooldown;
            windupTimer = attackWindup;
            AttackMotionTime = Time.time;
        }
    }

    // 목표로 갈 방향. 직선이 뚫려 있으면 그대로 직진하고(예전 움직임 그대로, 계산도 싸다),
    // 벽·담벼락에 가로막혔을 때만 내비메시 경로의 꺾임점을 따라 우회한다
    Vector3 SteerTowards(Vector3 targetPos, Vector3 straightDir)
    {
        if (!usePathfinding) return straightDir;

        repathTimer -= Time.fixedDeltaTime;
        bool targetMoved = (targetPos - pathTarget).sqrMagnitude > repathMoveThreshold * repathMoveThreshold;
        if (repathTimer <= 0f || targetMoved)
        {
            repathTimer = repathInterval;
            RecalculatePath(targetPos);
        }

        if (!pathValid) return straightDir;

        // 이미 지나친 꺾임점은 버리고, 아직 남은 첫 꺾임점으로 향한다
        while (cornerIndex < cornerCount)
        {
            Vector3 d = corners[cornerIndex] - rb.position;
            d.y = 0f;
            if (d.sqrMagnitude <= cornerArriveDistance * cornerArriveDistance) { cornerIndex++; continue; }
            return d.normalized;
        }

        pathValid = false; // 경로를 다 썼다. 다음 계산까지는 직진
        return straightDir;
    }

    void RecalculatePath(Vector3 targetPos)
    {
        pathTarget = targetPos;
        pathValid = false;
        cornerIndex = 0;
        cornerCount = 0;

        if (ClearLineTo(targetPos)) return; // 뻥 뚫렸으면 경로가 필요 없다

        // 내비메시가 구워져 있지 않거나 길이 끊겨 있으면 아무것도 하지 않는다 → 예전처럼 직진
        NavMeshHit from, to;
        if (!NavMesh.SamplePosition(rb.position, out from, navSampleRadius, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(targetPos, out to, navSampleRadius, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, navPath)) return;
        if (navPath.status == NavMeshPathStatus.PathInvalid) return;

        cornerCount = navPath.GetCornersNonAlloc(corners);
        if (cornerCount < 2) return;

        cornerIndex = 1; // 0번은 자기 발밑이라 건너뛴다
        pathValid = true;
    }

    // 여기서 어떤 지점까지 몸통이 지나갈 수 있는지. 얇은 광선으로 보면 모서리를 긁으므로 몸통 굵기로 훑는다.
    // 다른 적과 플레이어는 장애물로 치지 않는다 (몹 한 마리 때문에 우회하면 길이 계속 바뀐다)
    bool ClearLineTo(Vector3 point)
    {
        Vector3 from = rb.position + Vector3.up * 0.5f;
        Vector3 to = point + Vector3.up * 0.5f;
        Vector3 delta = to - from;
        float d = delta.magnitude;
        if (d < 0.01f) return true;

        RaycastHit[] hits = Physics.SphereCastAll(from, pathClearRadius, delta / d, d, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject go = hits[i].collider.gameObject;
            if (go == gameObject) continue;
            if (player != null && go == player.gameObject) continue;
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            return false;
        }
        return true;
    }

    bool CanSensePlayer()
    {
        if (forcedAggro) return true;              // 도발
        if (persistentAggro && everDetected) return true; // 한 번 발견한 뒤로는 놓치지 않는다

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= recognitionRadius) return true;

        Vector3 origin = transform.position + transform.forward * 0.6f + Vector3.up * 0.5f;
        Vector3 targetPoint = player.position + Vector3.up * 0.5f;
        Vector3 toTarget = targetPoint - origin;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float angle = Vector3.Angle(transform.forward, flatToTarget);
        if (angle > visionAngle * 0.5f) return false;

        RaycastHit hit;
        if (Physics.Raycast(origin, toTarget.normalized, out hit, toTarget.magnitude))
            return hit.collider != null && hit.collider.gameObject == player.gameObject;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, recognitionRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Vector3 leftBound = Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * transform.forward * 10f;
        Vector3 rightBound = Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * transform.forward * 10f;
        Gizmos.DrawLine(transform.position, transform.position + leftBound);
        Gizmos.DrawLine(transform.position, transform.position + rightBound);
    }


public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        if (rb == null) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || duration <= 0f) return;

        knockbackTimer = duration;
        knockbackDuration = duration;
        knockbackVelocity = direction.normalized * force;
        rb.linearVelocity = knockbackVelocity;
    }











// 특수패턴 대미지는 고정 수치가 아니라 플레이어 최대 체력 대비 퍼센트로 정한다
public float PercentOfPlayerMaxHealth(float percent)
    {
        if (playerHealth == null) return 0f;
        return playerHealth.maxHealth * percent * 0.01f;
    }


// 어떤 지점 주변에 몹이 몇 마리 있는지. 스포너가 과밀 지역에 더 뱉지 않도록 쓰인다
public static int CountNear(Vector3 position, float radius)
    {
        float sqrRadius = radius * radius;
        int count = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyAI e = active[i];
            if (e == null) { active.RemoveAt(i); continue; }

            Vector3 d = e.transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude <= sqrRadius) count++;
        }
        return count;
    }


// 총성: 반경 안의 적을 전부 추격 상태로 돌린다. 소리라서 장애물은 따지지 않는다
public static void AlertGunshot(Vector3 origin, float radius)
    {
        float sqrRadius = radius * radius;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyAI e = active[i];
            if (e == null) { active.RemoveAt(i); continue; }
            if ((e.transform.position - origin).sqrMagnitude > sqrRadius) continue;
            e.ForceAttack();
        }
    }


// 범위공격용 시야 검사: origin에서 target까지 장애물에 막혔는지. 다른 적과 자기 자신은 시야를 막지 않는다
public bool HasLineOfSight(Vector3 origin, Transform target)
    {
        if (target == null) return false;

        Vector3 from = origin + Vector3.up * 0.5f;
        Vector3 to = target.position + Vector3.up * 0.5f;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(from, delta / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject go = hits[i].collider.gameObject;
            if (go == gameObject) continue;
            if (go == target.gameObject) continue;
            if (hits[i].collider.GetComponent<EnemyAI>() != null) continue;
            return false;
        }
        return true;
    }


public void ForceAttack()
    {
        if (State != EnemyState.Attack) EnterAttack();
    }


public void ApplyStun(float duration)
    {
        if (duration > stunTimer) stunTimer = duration;
    }


// 공중에 띄운다. 뜨고 내려오는 동안만 행동불가이고, 착지 경직은 없다.
// (플레이어의 PlayerAirborne에는 groundedDuration이 따로 있지만 몹에는 그 개념을 두지 않는다)
public void ApplyAirborne(float height, float duration)
    {
        if (rb == null || duration <= 0f) return;
        if (airborneTimer > 0f) return; // 이미 떠 있으면 다시 띄우지 않는다

        airborneTimer = duration;
        airborneDuration = duration;
        airborneHeight = height;
        airborneLockedPos = rb.position;
        airborneGroundY = rb.position.y;

        knockbackTimer = 0f; // 넉백이 남아 있으면 같은 프레임에 서로 위치를 다툰다
        rb.linearVelocity = Vector3.zero;
    }


void UpdateAirborne()
    {
        // 4t(1-t) : t=0에서 0, t=0.5에서 최고점, t=1에서 정확히 원래 높이.
        // PlayerAirborne과 같은 식이라 뜨는 모양이 통일된다
        float t = Mathf.Clamp01((airborneDuration - airborneTimer) / airborneDuration);
        float y = airborneGroundY + airborneHeight * 4f * t * (1f - t);

        rb.MovePosition(new Vector3(airborneLockedPos.x, y, airborneLockedPos.z));
        rb.linearVelocity = Vector3.zero;

        airborneTimer -= Time.fixedDeltaTime;
        if (airborneTimer > 0f) return;

        // 착지. 다음 프레임부터 곧바로 다시 움직인다
        airborneTimer = 0f;
        rb.position = new Vector3(airborneLockedPos.x, airborneGroundY, airborneLockedPos.z);
        rb.linearVelocity = Vector3.zero;
    }


public void NotifyMeleeHit()
    {
        if (onMeleeHit != null) onMeleeHit();
    }
}
