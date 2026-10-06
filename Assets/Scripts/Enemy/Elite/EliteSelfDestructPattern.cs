using System.Collections.Generic;
using UnityEngine;

// Self-destruct pattern: this elite has no normal attack. Once the player steps inside the fuse
// radius it lights up, slows to a crawl over 3s while still closing in, then blows up:
// a much larger blast than the fuse radius, so walking away only works if you start early.
//
// 도화선이 붙기 전에 죽이면 대신 '조기 처치 폭발'이 난다. 플레이어에게는 피해가 없고
// 주변 적만 쓸어버리므로, 원거리에서 먼저 잡아내는 것이 손해가 아니라 보상이 된다.
[RequireComponent(typeof(EnemyAI))]
public class EliteSelfDestructPattern : MonoBehaviour, IElitePatternVisual
{
    // 도화선이 타는 동안만 폭발 모션을 재생한다. 길이는 fuseDuration으로 고정이라
    // 진행도를 넘겨 그림을 그 안에 펼친다 (뒤로 갈수록 간격이 짧아지게는 셋 쪽 easing이 맡는다)
    public bool PatternActive { get { return lit; } }
    public float PatternProgress
    {
        get { return fuseDuration <= 0f ? 1f : Mathf.Clamp01(fuseTimer / fuseDuration); }
    }

    public float fuseRadius = 5f;     // 점화: player must come this close
    public float fuseDuration = 3f;   // 이 동안 이동속도가 minSpeed까지 감소
    public float minSpeed = 2f;       // 폭발 직전 속도 (0이면 너무 쉽게 벗어남)
    public float blastRadius = 15f;
    public float damagePercent = 20f;    // 플레이어 최대 체력 대비 %
    public float tauntDuration = 30f;     // 도발: 자폭 실행 시 항상 부여
    public float confusionDuration = 8f;  // 혼란: 폭발 대미지를 입었을 때만 추가로 부여

    [Header("조기 처치 폭발")]
    // 도화선이 붙기 전(lit == false)에 죽었을 때만 터진다.
    // 플레이어에게는 피해를 주지 않는다 — 이 폭발의 목적은 무리 정리이지 자해가 아니다
    public float deathBlastRadius = 5f;
    public float deathBlastDamage = 100f;
    public Color deathBlastColor = new Color(0.35f, 0.8f, 1f, 1f); // 자폭(주황)과 구분되는 하늘색

    public Material castMaterial;     // shared "casting" tint, blinked during the fuse
    public Color blastColor = new Color(1f, 0.35f, 0.05f, 1f);
    public float blastMarkerLifetime = 0.5f;
    public float blinkStartInterval = 0.35f;
    public float blinkEndInterval = 0.05f;

    EnemyAI ai;
    Renderer rend;
    Material normalMaterial;
    Health selfHealth;

    bool lit;
    bool exploded; // Destroy() only takes effect at the end of the frame — without this a second
                   // FixedUpdate in the same frame would detonate the same bomb again
    float fuseTimer;
    float baseSpeed;
    float blinkTimer;
    bool blinkOn;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
        selfHealth = GetComponent<Health>();
    }

    // 죽는 순간을 잡으려고 Health.Damaged를 직접 듣는다.
    // EnemyAI도 같은 이벤트로 사망 처리를 하지만, 이벤트는 구독자 전원에게 가므로
    // 어느 쪽이 먼저 붙었는지와 무관하게 둘 다 호출된다 (Destroy는 프레임 끝에 적용된다)
    void OnEnable()
    {
        if (selfHealth != null) selfHealth.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (selfHealth != null) selfHealth.Damaged -= OnDamaged;
    }

    void OnDamaged()
    {
        if (lit || exploded) return;                       // 시동이 걸린 뒤엔 이 폭발이 없다
        if (selfHealth == null || selfHealth.CurrentHealth > 0f) return;
        DeathBlast();
    }

    // 조기 처치 폭발. 플레이어만 빼고 반경 안의 모든 체력에 고정 피해를 준다
    void DeathBlast()
    {
        exploded = true;   // 이후 Explode()가 겹쳐 돌지 않도록 같은 빗장을 쓴다
        enabled = false;

        Transform p = ai.PlayerTransform;
        Collider[] hits = Physics.OverlapSphere(transform.position, deathBlastRadius, ~0, QueryTriggerInteraction.Ignore);

        // 한 오브젝트에 콜라이더가 여럿이면 중복으로 맞으므로 걸러낸다
        HashSet<Health> struck = new HashSet<Health>();
        for (int i = 0; i < hits.Length; i++)
        {
            Health h = hits[i].GetComponentInParent<Health>();
            if (h == null || h == selfHealth) continue;
            if (p != null && h.gameObject == p.gameObject) continue; // 플레이어는 피해 없음
            if (!struck.Add(h)) continue;
            h.TakeDamage(deathBlastDamage);
        }

        SpawnBlastMarker(deathBlastRadius, deathBlastColor);
    }

    void FixedUpdate()
    {
        if (lit) { UpdateFuse(); return; }

        Transform p = ai.PlayerTransform;
        if (p == null) return;

        Vector3 toPlayer = p.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > fuseRadius) return;

        Light();
    }

    void Light()
    {
        lit = true;                 // 점화되면 취소 불가
        fuseTimer = 0f;
        baseSpeed = ai.attackSpeed;
        blinkTimer = 0f;
        if (ai.State != EnemyState.Attack) ai.ForceAttack();
    }

    void UpdateFuse()
    {
        fuseTimer += Time.fixedDeltaTime;

        float t = Mathf.Clamp01(fuseTimer / fuseDuration);
        ai.attackSpeed = Mathf.Lerp(baseSpeed, minSpeed, t); // 점점 느려지되 멈추지는 않음
        UpdateBlink(t);

        if (fuseTimer >= fuseDuration) Explode();
    }

    void UpdateBlink(float t)
    {
        if (rend == null || castMaterial == null) return;

        blinkTimer -= Time.fixedDeltaTime;
        if (blinkTimer > 0f) return;

        blinkOn = !blinkOn;
        rend.sharedMaterial = blinkOn ? castMaterial : normalMaterial;
        blinkTimer = Mathf.Lerp(blinkStartInterval, blinkEndInterval, t); // 점점 빨라지는 경고
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;
        enabled = false;

        Transform p = ai.PlayerTransform;
        if (p != null)
        {
            PlayerTaunt taunt = p.GetComponent<PlayerTaunt>(); // 도발: 자폭이 터지면 무조건
            if (taunt != null) taunt.Apply(tauntDuration);

            Vector3 flat = p.position - transform.position;
            flat.y = 0f;
            // 엄폐물 뒤에 있으면 폭발이 닿지 않는다
            if (flat.magnitude <= blastRadius && ai.HasLineOfSight(transform.position, p))
            {
                Health h = ai.PlayerHealth;
                if (h != null) h.TakeDamage(ai.PercentOfPlayerMaxHealth(damagePercent));

                PlayerConfusion confusion = p.GetComponent<PlayerConfusion>(); // 혼란: 폭발에 맞았을 때만
                if (confusion != null) confusion.Apply(confusionDuration);
            }
        }

        SpawnBlastMarker(blastRadius, blastColor);

        if (rend != null && normalMaterial != null) rend.sharedMaterial = normalMaterial;
        Destroy(gameObject); // 자폭
    }

    void SpawnBlastMarker(float radius, Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;

        // root object: the elite's non-uniform scale would squash it, and it must outlive the explosion
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Blast";
        Destroy(marker.GetComponent<Collider>()); // visual only
        Vector3 pos = transform.position;
        pos.y = rend != null ? rend.bounds.min.y : pos.y;
        marker.transform.position = pos;
        marker.transform.localScale = new Vector3(radius * 2f, 0.15f, radius * 2f);
        marker.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(marker, blastMarkerLifetime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, fuseRadius);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
