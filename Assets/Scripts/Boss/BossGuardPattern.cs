using System.Collections.Generic;
using UnityEngine;

// 방어 모드: 처음은 남은 체력이 firstTriggerPercent에 닿을 때, 그 뒤로는 토템이 부서진
// 시점부터 recastCooldown이 지날 때마다 다시 발동한다.
// 보스 기준 동쪽에 토템이 서고, 그동안 보스는 피해를 아예 입지 않는다 — 깎인 만큼 되돌린 뒤
// '맞은 양 x healMultiplier' 를 덤으로 회복한다. 즉 때릴수록 체력이 는다.
// 토템을 전부 부수면 해제된다.
//
// 강화판(토템 2개)은 특정 체력이 아니라 '마지막 페이즈'에서 나온다.
// 페이즈는 BossObstaclePattern이 체력 20%마다 한 단계씩 올리고, 마지막 단계에 닿으면
// AllZonesSpawned가 켜진다 — 그때부터 이 패턴은 강화판으로 나간다.
//
// 토템에는 Health를 달지 않는다. 총알(Bullet)과 유탄은 Health를 찾아서 피해를 주므로,
// 없으면 그대로 통과한다 — 근접만 통하게 하는 데 추가 코드가 필요 없다.
[RequireComponent(typeof(Health))]
public class BossGuardPattern : MonoBehaviour
{
    public float firstTriggerPercent = 50f; // 첫 발동: 남은 체력 %
    public float recastCooldown = 30f;      // 재발동: 토템이 부서진 시점부터 잰다
    public int normalTotemCount = 1;        // 평소
    public int enhancedTotemCount = 2;      // 마지막 페이즈
    public int totemMeleeHits = 3;          // 토템당 근접 피격 횟수
    public float healMultiplier = 2f;       // 피해를 무효로 돌린 뒤, 맞은 양의 몇 배를 더 회복하는가

    [Header("토템 배치")]
    public float totemDistance = 9f;   // 보스 기준 동쪽으로 이만큼
    public float sideAngle = 30f;      // 2개일 때 동쪽을 중심으로 좌우로 벌리는 각
    public Vector3 totemSize = new Vector3(1.8f, 4.5f, 1.8f);
    public Color totemColor = new Color(0.95f, 0.80f, 0.20f, 1f);
    [Tooltip("넣어 두면 원기둥 대신 이 모델을 높이 totemSize.y에 맞춰(비율 유지) 세운다. 충돌도 모델 폭에 맞춘다")]
    public GameObject totemPrefab;
    public float totemYaw = 90f;               // 모델을 위에서 볼 때 시계 방향으로 이만큼 돌린다
    public float totemDropHeight = 14f;        // 이 높이에서 떨어져 꽂힌다 (보이는 것만, 판정은 처음부터)
    public float totemDropTime = 0.2f;
    public GameObject totemImpactEffect;       // 꽂히는 순간 발밑에서 터뜨린다
    public float totemImpactEffectScale = 3f;
    public GameObject totemStrikeEffect;       // 주위 공격 판정 순간 발밑에서 터뜨린다 (맞든 안 맞든)
    public float totemStrikeEffectScale = 1f;
    public GameObject totemStrikeEffect2;      // 같이 겹쳐 터뜨리는 두 번째 이펙트
    public float totemStrikeEffectScale2 = 1f;
    public Color guardColor = new Color(0.35f, 0.65f, 1f, 1f); // 방어 중 보스 색

    [Header("토템 공격")]
    public float totemAttackRadius = 3.5f;     // 피해 범위
    public float totemAttackInterval = 5f;     // 시전 주기 (시전 시작 기준)
    public float totemWarningTime = 1.5f;      // 워닝 표시 유지 시간. 이 뒤에 판정이 들어간다
    public float totemDamagePercent = 10f;     // 플레이어 최대 체력 대비 %
    public Color totemWarningColor = new Color(1f, 0.35f, 0.1f, 1f);

    public bool Guarding { get { return totems.Count > 0; } }

    Health health;
    EnemyAI ai;
    BossObstaclePattern obstacles;
    Renderer rend;
    MaterialPropertyBlock block;
    Color normalColor;
    bool firstDone;
    float nextCastTime = float.PositiveInfinity; // 토템이 부서지면 그때 채워진다
    float lastHp;
    readonly List<BossTotem> totems = new List<BossTotem>();

    // 마지막 페이즈에 닿았는가. 장애물 패턴이 존마다 한 단계씩 올린다
    bool LastPhase { get { return obstacles != null && obstacles.AllZonesSpawned; } }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        health = GetComponent<Health>();
        ai = GetComponent<EnemyAI>();
        obstacles = GetComponent<BossObstaclePattern>();
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
            normalColor = rend.sharedMaterial.GetColor(BaseColorId);
    }

    // 재발동은 체력과 무관하므로 피격 이벤트가 아니라 시간으로 본다
    void Update()
    {
        if (Guarding || !firstDone) return;
        if (Time.time < nextCastTime) return;
        Begin(LastPhase ? enhancedTotemCount : normalTotemCount);
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        lastHp = health.CurrentHealth;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        ClearTotems();
    }

    void OnDamaged()
    {
        if (Guarding)
        {
            // 깎인 만큼 먼저 되돌려 피해를 무효로 만들고(= 피해 0), 그 위에 맞은 양 x2 를 더 회복한다.
            // Heal은 Damaged를 다시 부르지 않으므로 재귀 걱정이 없다
            float taken = lastHp - health.CurrentHealth;
            if (taken > 0f) health.Heal(taken + taken * healMultiplier);
            lastHp = health.CurrentHealth;
            return;
        }

        lastHp = health.CurrentHealth;
        if (firstDone || health.maxHealth <= 0f) return;

        // 첫 발동만 체력으로 연다. 그 뒤로는 Update가 쿨타임으로 돌린다
        float pct = health.CurrentHealth / health.maxHealth * 100f;
        if (pct > firstTriggerPercent) return;
        firstDone = true;
        Begin(LastPhase ? enhancedTotemCount : normalTotemCount);
    }

    // 토템을 세우는 기준 방향. 보스가 보는 쪽이 아니라 항상 월드 동쪽(+X)이다.
    // 보스는 플레이어를 계속 쳐다보므로 transform.forward를 쓰면 발동 순간의 시선에 따라
    // 토템이 매번 다른 자리에 서서, 어디를 때려야 할지 외울 수가 없었다
    static Vector3 BaseDirection { get { return Vector3.right; } }

    void Begin(int count)
    {
        ClearTotems();

        // 보스가 밟고 선 높이. 위에서 레이를 쏘면 보스 자신을 먼저 맞는다
        float groundY = rend != null ? rend.bounds.min.y : transform.position.y;

        for (int i = 0; i < count; i++)
        {
            float a = count <= 1 ? 0f : -sideAngle + (2f * sideAngle * i / (count - 1));
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * BaseDirection;

            Vector3 pos = transform.position + dir.normalized * totemDistance;
            float y = BossGround.Y(pos, groundY); // 토템 자리의 바닥 (다른 소환물·몹 위에 올라서지 않게)
            pos.y = y + totemSize.y * 0.5f;
            totems.Add(BossTotem.Spawn(this, pos, totemSize, totemColor, totemMeleeHits, y, totemPrefab));
        }

        nextCastTime = float.PositiveInfinity; // 서 있는 동안에는 재발동 타이머를 돌리지 않는다
        Tint(true);
    }

    public void NotifyTotemDestroyed(BossTotem t)
    {
        totems.Remove(t);
        if (totems.Count > 0) return;
        Tint(false);
        nextCastTime = Time.time + recastCooldown; // 쿨타임은 '부서진 시점'부터
    }

    // 토템이 플레이어에게 줄 피해량. 플레이어 최대 체력 비례라 EnemyAI의 계산을 그대로 쓴다
    public float TotemDamage
    {
        get { return ai != null ? ai.PercentOfPlayerMaxHealth(totemDamagePercent) : 0f; }
    }

    public Transform PlayerTransform { get { return ai != null ? ai.PlayerTransform : null; } }
    public Health PlayerHealth { get { return ai != null ? ai.PlayerHealth : null; } }

    void ClearTotems()
    {
        for (int i = 0; i < totems.Count; i++)
            if (totems[i] != null) Destroy(totems[i].gameObject);
        totems.Clear();
        Tint(false);
    }

    void Tint(bool on)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorId, on ? guardColor : normalColor);
        rend.SetPropertyBlock(block);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.95f, 0.8f, 0.2f, 0.6f);
        for (int i = 0; i < 2; i++)
        {
            float a = i == 0 ? -sideAngle : sideAngle;
            Gizmos.DrawWireSphere(transform.position + Quaternion.Euler(0f, a, 0f) * BaseDirection * totemDistance, 1f);
        }
        Gizmos.DrawWireSphere(transform.position + BaseDirection * totemDistance, 1f);
    }
}

// 보스 방어 토템. 근접으로만 부서진다 (Health가 없어 총알·유탄은 통하지 않는다).
// 토템을 부수려면 근접 사거리까지 붙어야 하는데, 그 자리가 안전하면 그냥 서서 때리면 그만이다.
// 그래서 플레이어가 다가오면 주기적으로 발밑을 때리는 공격을 시전한다 — 때리고 빠지게 만든다.
public class BossTotem : MonoBehaviour
{
    BossGuardPattern owner;
    Renderer rend;
    MaterialPropertyBlock block;
    Color baseColor;
    int hitsLeft;
    int hitsTotal;
    bool ownsMaterial;

    float groundY;        // 워닝 장판을 깔 높이
    float nextCastTime;   // 다음 시전 가능 시각
    float strikeTime = -1f; // 판정이 들어갈 시각 (-1이면 시전 중이 아님)
    GameObject warning;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static BossTotem Spawn(BossGuardPattern owner, Vector3 pos, Vector3 size, Color color, int hits, float groundY, GameObject prefab = null)
    {
        GameObject go;
        Renderer rend;
        Transform modelRoot = null;
        bool ownsMaterial = prefab == null;
        if (prefab != null)
        {
            go = new GameObject("BossTotem");
            go.transform.position = pos;
            GameObject model = Instantiate(prefab, go.transform); // 모델 루트 회전(FBX -90도)은 그대로
            modelRoot = model.transform;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            rend = model.GetComponentInChildren<Renderer>();
            model.transform.localScale *= size.y / rend.bounds.size.y; // 높이를 맞추고 비율 유지
            if (owner != null) model.transform.Rotate(0f, owner.totemYaw, 0f, Space.World);
            Bounds b = rend.bounds;
            model.transform.position += new Vector3(pos.x - b.center.x, groundY - b.min.y, pos.z - b.center.z); // 바닥에 세운다
            b = rend.bounds;
            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.radius = Mathf.Max(b.extents.x, b.extents.z);
            cap.height = b.size.y;
            cap.center = go.transform.InverseTransformPoint(b.center);
            color = Color.white; // 맞을수록 어두워지는 색을 텍스처 위에 곱한다
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "BossTotem";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z); // 실린더 기본 높이가 2다
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = color;
            rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = m;
        }

        BossTotem t = go.AddComponent<BossTotem>();
        t.owner = owner;
        t.rend = rend;
        t.ownsMaterial = ownsMaterial;
        t.block = new MaterialPropertyBlock();
        t.baseColor = color;
        t.hitsLeft = Mathf.Max(1, hits);
        t.hitsTotal = t.hitsLeft;
        t.groundY = groundY;
        // 세워지자마자 때리면 피할 틈이 없다. 첫 시전은 주기만큼 기다린다
        t.nextCastTime = Time.time + (owner != null ? owner.totemAttackInterval : 5f);
        if (modelRoot != null && owner != null && owner.totemDropTime > 0f)
            t.StartCoroutine(t.Drop(modelRoot, new Vector3(pos.x, groundY, pos.z)));
        return t;
    }

    // 하늘에서 가속하며 떨어져 꽂힌다. 꽂히는 순간 펑 + 짧은 흔들림
    System.Collections.IEnumerator Drop(Transform model, Vector3 foot)
    {
        Vector3 end = model.position;
        Vector3 start = end + Vector3.up * owner.totemDropHeight;
        for (float t = 0f; t < owner.totemDropTime; t += Time.deltaTime)
        {
            float k = t / owner.totemDropTime;
            model.position = Vector3.Lerp(start, end, k * k);
            yield return null;
        }
        model.position = end;

        if (owner.totemImpactEffect != null)
        {
            GameObject fx = Instantiate(owner.totemImpactEffect, foot, owner.totemImpactEffect.transform.rotation);
            fx.transform.localScale = Vector3.one * owner.totemImpactEffectScale;
            Destroy(fx, 3f);
        }
        CameraFollow.Shake(0.15f, 0.15f);
    }

    void Update()
    {
        if (owner == null) return;

        // 시전 중이면 판정 시각만 기다린다
        if (strikeTime >= 0f)
        {
            if (Time.time < strikeTime) return;
            Strike();
            return;
        }

        if (Time.time < nextCastTime) return;

        Transform p = owner.PlayerTransform;
        if (p == null) return;
        if (FlatDistance(p.position) > owner.totemAttackRadius) return; // 다가와야 반응한다

        BeginCast();
    }

    float FlatDistance(Vector3 world)
    {
        Vector3 d = world - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void BeginCast()
    {
        nextCastTime = Time.time + owner.totemAttackInterval; // 주기는 시전 시작 기준
        strikeTime = Time.time + owner.totemWarningTime;

        // 범위를 그대로 보여 주는 장판. 토템의 비균등 스케일을 타지 않도록 루트로 띄운다
        warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        warning.name = "TotemStrikeWarning";
        Destroy(warning.GetComponent<Collider>()); // 표시일 뿐
        Vector3 pos = transform.position;
        pos.y = groundY + 0.02f;
        warning.transform.position = pos;
        warning.transform.localScale = new Vector3(owner.totemAttackRadius * 2f, 0.15f, owner.totemAttackRadius * 2f);
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = owner.totemWarningColor;
        warning.GetComponent<Renderer>().sharedMaterial = m;
    }

    void Strike()
    {
        strikeTime = -1f;
        if (warning != null) { Destroy(warning); warning = null; }

        Vector3 foot = new Vector3(transform.position.x, groundY, transform.position.z);
        SpawnFx(owner.totemStrikeEffect, owner.totemStrikeEffectScale, foot);
        SpawnFx(owner.totemStrikeEffect2, owner.totemStrikeEffectScale2, foot);

        // 워닝이 끝나는 순간에 다시 잰다 — 그 사이에 빠져나갔으면 헛친다
        Transform p = owner.PlayerTransform;
        Health h = owner.PlayerHealth;
        if (p == null || h == null) return;
        if (FlatDistance(p.position) > owner.totemAttackRadius) return;
        h.TakeDamage(Mathf.Round(owner.TotemDamage));
    }

    static void SpawnFx(GameObject prefab, float scale, Vector3 at)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, at, prefab.transform.rotation);
        fx.transform.localScale = Vector3.one * scale;
        // 파티클이 Local 크기 모드면 루트 스케일을 무시해 원본 크기로 나온다. 계층 스케일을 따르게 바꾼다
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule m = ps.main;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        Destroy(fx, 3f);
    }

    void OnDestroy()
    {
        if (warning != null) Destroy(warning);
        if (ownsMaterial && rend != null && rend.sharedMaterial != null) Destroy(rend.sharedMaterial); // 모델 재질은 에셋이라 지우면 안 된다
    }

    // 보스 자신의 공격(분신 돌진·레이저)이 자기 토템에 막히지 않도록 쓰는 판별.
    // 토템은 플레이어의 근접을 받아내는 물건이지 보스의 사격을 가리는 엄폐물이 아니다
    public static bool Is(Collider col)
    {
        return col != null && col.GetComponentInParent<BossTotem>() != null;
    }

    // PlayerCombat.MeleeAttack 에서만 호출한다
    public void TakeMeleeHit()
    {
        hitsLeft--;
        if (hitsLeft > 0)
        {
            // 남은 횟수가 줄수록 어두워져 진행도가 보인다
            float t = (float)hitsLeft / hitsTotal;
            rend.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(Color.black, baseColor, 0.25f + 0.75f * t));
            rend.SetPropertyBlock(block);
            return;
        }

        if (owner != null) owner.NotifyTotemDestroyed(this);
        SupplyKit.Spawn(transform.position); // 부순 보상: 보급상자 확정 드랍
        Destroy(gameObject);
    }
}
