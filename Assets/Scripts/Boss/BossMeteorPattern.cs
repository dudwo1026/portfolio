using System.Collections;
using UnityEngine;

// 보스 특수패턴 4: 2초 캐스팅 후 플레이어 머리 위로 운석을 연달아 떨군다.
// 캐스팅이 끝나면 보스는 바로 다른 행동을 할 수 있고, 운석 낙하는 알아서 진행된다.
[RequireComponent(typeof(EnemyAI))]
public class BossMeteorPattern : MonoBehaviour
{
    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public float castTime = 2f;        // 이 시간 동안 보스가 캐스팅
    public float warningDelay = 1.5f;  // 워닝 생성 후 낙하까지
    public float spawnInterval = 0.5f; // 운석과 운석 사이 간격
    public int baseCount = 4;          // 기본 개수. 페이즈마다 +1

    [Header("운석")]
    public float damage = 30f;
    public float radius = 2.5f;        // 지름 5
    public float fallHeight = 25f;
    public float meteorScale = 2f;

    [Header("색")]
    public Color castColor = new Color(1f, 0.45f, 0.05f);
    public Color warningColor = new Color(1f, 0.25f, 0.05f);
    public Color meteorColor = new Color(0.25f, 0.06f, 0.02f);
    public Color impactColor = new Color(1f, 0.6f, 0.15f);
    public float impactLifeTime = 0.35f;

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    Renderer rend;
    Material normalMaterial;
    float nextCastTime;
    bool running;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        obstacles = GetComponent<BossObstaclePattern>();
        actionLock = GetComponent<BossActionLock>();
        if (actionLock == null) actionLock = gameObject.AddComponent<BossActionLock>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
    }

    void Start()
    {
        nextCastTime = Time.time + NextCooldown();
    }

    // 18~23초 사이 무작위, 페이즈가 오를수록 짧아진다
    float NextCooldown()
    {
        int phase = obstacles != null ? obstacles.TriggeredCount : 0;
        float roll = Random.Range(cooldownMin, cooldownMax);
        return Mathf.Max(cooldownFloor, roll - cooldownStepPerPhase * phase);
    }

    void Update()
    {
        if (running) return;
        if (ai.State != EnemyState.Attack) return;
        if (ai.PlayerTransform == null) return;
        if (Time.time < nextCastTime) return;
        if (actionLock.IsLocked) return; // 다른 패턴이 보스를 묶고 있다

        actionLock.LockFor(castTime); // 캐스팅 동안은 다른 패턴이 끼어들지 못한다
        nextCastTime = Time.time + NextCooldown();
        StartCoroutine(Run());
    }

    int MeteorCount()
    {
        int extra = obstacles != null ? obstacles.TriggeredCount : 0; // 페이즈가 오를수록 하나씩
        return baseCount + extra;
    }

    IEnumerator Run()
    {
        running = true;

        SetCastVisual(true);
        yield return new WaitForSeconds(castTime);
        SetCastVisual(false);

        running = false; // 캐스팅만 끝나면 보스는 자유. 낙하는 이어서 진행된다

        int count = MeteorCount();
        for (int i = 0; i < count; i++)
        {
            Transform p = ai.PlayerTransform;
            if (p == null) yield break;

            StartCoroutine(DropMeteor(p.position)); // 그 순간의 플레이어 위치를 노린다
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    IEnumerator DropMeteor(Vector3 target)
    {
        float groundY = FindGroundY(target);
        Vector3 impact = new Vector3(target.x, groundY, target.z);

        GameObject warning = MakeDisc("MeteorWarning", impact, radius * 2f, 0.08f, warningColor);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Meteor";
        Destroy(rock.GetComponent<Collider>()); // 낙하 중에는 부딪히지 않는다
        rock.transform.localScale = Vector3.one * meteorScale;
        rock.GetComponent<Renderer>().sharedMaterial = MakeMaterial(meteorColor);

        float t = 0f;
        Vector3 from = impact + Vector3.up * fallHeight;
        Vector3 to = impact + Vector3.up * meteorScale * 0.5f;
        while (t < warningDelay)
        {
            t += Time.deltaTime;
            if (rock != null) rock.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / warningDelay));
            yield return null;
        }

        if (warning != null) Destroy(warning);
        if (rock != null) Destroy(rock);

        Explode(impact);
    }

    void Explode(Vector3 impact)
    {
        Collider[] hits = Physics.OverlapSphere(impact + Vector3.up * 1f, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponent<PlayerCombat>() == null) continue; // 플레이어만
            Health h = hits[i].GetComponent<Health>();
            if (h != null) h.TakeDamage(damage);
        }

        GameObject flash = MakeDisc("MeteorImpact", impact, radius * 2f, 0.12f, impactColor);
        Destroy(flash, impactLifeTime);
    }

    float FindGroundY(Vector3 point)
    {
        return BossGround.Y(point, point.y); // 플레이어·토템·장애물 위가 아니라 그 밑 바닥
    }

    GameObject MakeDisc(string name, Vector3 pos, float diameter, float thickness, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = new Vector3(pos.x, pos.y + thickness * 0.5f, pos.z);
        go.transform.localScale = new Vector3(diameter, thickness, diameter);
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
        return go;
    }

    Material MakeMaterial(Color c)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        return m;
    }

    void SetCastVisual(bool on)
    {
        if (rend == null) return;
        if (on) rend.material.color = castColor;
        else if (normalMaterial != null) rend.sharedMaterial = normalMaterial;
    }
}
