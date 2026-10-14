using System.Collections.Generic;
using UnityEngine;

// 보스 특수패턴 1: 체력이 일정 비율씩 깎일 때마다 정해진 구역에 장애물을 떨군다.
// 시전 → 예고 표시 → castTime 뒤 장애물 생성. 그 순간 구역 안에 있으면 즉사.
// 장애물은 보스가 죽을 때까지 남는다.
[RequireComponent(typeof(Health))]
public class BossObstaclePattern : MonoBehaviour
{
    [Header("구역 (순서대로 시전)")]
    public Vector3[] zonePositions;
    public Vector3[] zoneSizes;

    [Header("발동")]
    public float hpStepPercent = 20f;  // 이만큼 깎일 때마다 한 번
    public float castTime = 3f;

    [Header("연출")]
    public Color warningColor = new Color(1f, 0.25f, 0.1f, 1f);
    public Color obstacleColor = new Color(0.12f, 0.14f, 0.18f);
    public float warningThickness = 0.15f;
    [Tooltip("넣어 두면 큐브 대신 이 모델을 구역 크기에 꽉 차게 늘려 세운다. 판정은 구역 상자 그대로")]
    public GameObject obstaclePrefab;
    [Tooltip("켜면 모델을 여러 개 바둑판으로 깔고, 끄면 한 덩어리로 늘린다 (무늬도 같이 늘어난다)")]
    public bool tileObstacle = false;
    [Tooltip("모델 색 밝기 배율. 1이면 원본")]
    public float obstacleBrightness = 1f;
    Material obstacleMat;

    Health health;
    int triggered;          // 지금까지 시전한 횟수

    public int TriggeredCount { get { return triggered; } }
    public bool AllZonesSpawned { get { return zonePositions != null && triggered >= zonePositions.Length; } }
    bool casting;
    float castTimer;
    int castingZone;
    GameObject warning;
    readonly List<GameObject> obstacles = new List<GameObject>();

    void Awake()
    {
        health = GetComponent<Health>();
        health.Damaged += OnDamaged;
    }

    void OnDamaged()
    {
        TryStartCast();
    }

    void TryStartCast()
    {
        if (casting) return;
        if (zonePositions == null || triggered >= zonePositions.Length) return;
        if (health.maxHealth <= 0f) return;

        float lostRatio = 1f - Mathf.Clamp01(health.CurrentHealth / health.maxHealth);
        // 1 - 1600f/2000f 가 0.19999... 로 떨어져 한 단계 늦게 발동하므로 아주 작은 여유를 준다
        int steps = Mathf.FloorToInt(lostRatio / (hpStepPercent * 0.01f) + 0.0001f);
        if (steps <= triggered) return;

        BeginCast(triggered);
    }

    void BeginCast(int zone)
    {
        casting = true;
        castingZone = zone;
        castTimer = castTime;
        warning = MakeWarning(zone);
    }

    void Update()
    {
        if (!casting) return;

        castTimer -= Time.deltaTime;
        if (castTimer > 0f) return;

        casting = false;
        if (warning != null) Destroy(warning);

        SpawnObstacle(castingZone);
        triggered = castingZone + 1;

        TryStartCast(); // 한 번에 여러 단계가 깎였으면 이어서 시전
    }

    GameObject MakeWarning(int zone)
    {
        Vector3 size = zoneSizes[zone];
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BossObstacleWarning";
        Destroy(go.GetComponent<Collider>()); // 예고는 막지 않는다
        go.transform.position = new Vector3(zonePositions[zone].x,
                                            zonePositions[zone].y - size.y * 0.5f + warningThickness * 0.5f,
                                            zonePositions[zone].z);
        go.transform.localScale = new Vector3(size.x, warningThickness, size.z);
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(warningColor);
        return go;
    }

    void SpawnObstacle(int zone)
    {
        Vector3 size = zoneSizes[zone];
        Vector3 pos = zonePositions[zone];

        // 생성 순간 그 자리에 있으면 즉사
        Collider[] hits = Physics.OverlapBox(pos, size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponent<PlayerCombat>() == null) continue;
            Health ph = hits[i].GetComponent<Health>();
            if (ph != null) ph.TakeDamage(ph.maxHealth * 1000f); // 확실히 즉사
        }

        GameObject go;
        if (obstaclePrefab != null)
        {
            go = new GameObject("BossObstacle_" + (zone + 1));
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
            // 한 개를 구역 크기로 늘리면 무늬까지 수십 배로 늘어난다.
            // 높이를 구역에 맞춘 배율로 모델을 바둑판처럼 깔고, 칸 수에 맞게 가로·세로만 살짝 맞춘다
            GameObject probe = Instantiate(obstaclePrefab);
            Vector3 unit = probe.GetComponentInChildren<Renderer>().bounds.size; // 배율 1일 때 월드 크기
            Destroy(probe);
            float s = size.y / unit.y;
            int nx = tileObstacle ? Mathf.Max(1, Mathf.RoundToInt(size.x / (unit.x * s))) : 1;
            int nz = tileObstacle ? Mathf.Max(1, Mathf.RoundToInt(size.z / (unit.z * s))) : 1;
            Vector3 cell = new Vector3(size.x / nx, size.y, size.z / nz);
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 center = pos + new Vector3((ix + 0.5f) * cell.x - size.x * 0.5f, 0f, (iz + 0.5f) * cell.z - size.z * 0.5f);
                    // 모델 루트가 눕혀져(-90도) 있어서 회전은 그대로 두고, 회전 없는 받침을 월드 축으로 늘린다
                    Transform holder = new GameObject("Piece").transform;
                    holder.SetParent(go.transform, false);
                    holder.position = center;
                    GameObject model = Instantiate(obstaclePrefab, holder);
                    foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c); // 막는 건 위 상자만
                    holder.localScale = new Vector3(cell.x / unit.x, cell.y / unit.y, cell.z / unit.z);
                    Renderer r = model.GetComponentInChildren<Renderer>();
                    holder.position += center - r.bounds.center;
                    if (obstacleMat == null)
                    {
                        obstacleMat = new Material(r.sharedMaterial);
                        Color c = r.sharedMaterial.color * obstacleBrightness;
                        c.a = 1f;
                        obstacleMat.color = c;
                    }
                    r.sharedMaterial = obstacleMat;
                }
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BossObstacle_" + (zone + 1);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(obstacleColor);
        }
        obstacles.Add(go);
    }

    Material MakeMaterial(Color c)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        return m;
    }

    void OnDestroy()
    {
        // 보스가 죽으면 장애물도 같이 치운다
        if (warning != null) Destroy(warning);
        for (int i = 0; i < obstacles.Count; i++)
            if (obstacles[i] != null) Destroy(obstacles[i]);
        obstacles.Clear();

        if (health != null) health.Damaged -= OnDamaged;
    }

    void OnDrawGizmosSelected()
    {
        if (zonePositions == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.5f);
        for (int i = 0; i < zonePositions.Length && i < zoneSizes.Length; i++)
            Gizmos.DrawWireCube(zonePositions[i], zoneSizes[i]);
    }
}
