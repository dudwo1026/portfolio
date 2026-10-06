using System.Collections.Generic;
using UnityEngine;

// 10초마다 한 마리씩 계속 소환한다. 상한은 모든 스포너가 공유하는 전체 생존 수.
public class EnemySpawner : MonoBehaviour
{
    [System.NonSerialized] public bool paused; // 옥상에 올라가 있는 동안 정지
    public bool roofSpawner; // 옥상(보스전) 전용 스포너. 지상/옥상에 따라 어느 쪽이 도는지 갈린다
    public GameObject enemyPrefab;
    public float spawnInterval = 10f;
    public int maxAlive = 100;  // 스포너별이 아니라 전체 합산 상한
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    public float spawnRadius = 5f;      // 같은 자리에 쌓이지 않도록 이 반경 안에 흩어 놓는다
    public float clearRadius = 0.6f;    // 이 반경 안에 뭔가 있으면 그 자리는 쓰지 않는다
    public int placementTries = 12;

    [Header("과밀 시 소환 중단")]
    public float crowdCheckRadius = 10f;
    public int crowdSpawnLimit = 20;  // 반경 안에 이만큼 있으면 이 스포너는 쉰다

    [Header("배회 구역 (맵을 사분면으로 나눔)")]
    public string mapName = "Map1";
    public float areaInset = 3f;  // 담장 안쪽으로 이만큼 여유

    Bounds quadrant;
    bool quadrantResolved;
    bool quadrantValid;

    // 스포너가 여러 개여도 상한이 하나로 유지되도록 static
    static readonly List<GameObject> alive = new List<GameObject>();

    public static int AliveCount
    {
        get { Prune(); return alive.Count; }
    }

    float timer;

    void OnEnable()
    {
        timer = 0f; // 시작하자마자 첫 마리
    }

    // 옥상 입장 직후처럼, 한동안 조용히 있어야 할 때
    public void DelayNextSpawn(float seconds)
    {
        if (seconds > timer) timer = seconds;
    }

    void Update()
    {
        if (paused) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = spawnInterval;
        TrySpawn();
    }

    void TrySpawn()
    {
        if (enemyPrefab == null) return;

        Prune();
        if (alive.Count >= maxAlive) return; // 꽉 찼으면 이번 주기는 건너뛴다

        // 주변이 이미 포화면 더 뱉지 않는다 (자리가 빌 때까지 이 스포너만 쉰다)
        if (EnemyAI.CountNear(transform.position, crowdCheckRadius) >= crowdSpawnLimit) return;

        GameObject e = Instantiate(enemyPrefab, FindSpawnPoint(), Quaternion.identity);
        alive.Add(e);

        // 이 스포너가 속한 사분면을 배회 구역으로 물려준다
        EnemyAI ai = e.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.roofSpawned = roofSpawner;
            if (HasQuadrant()) ai.SetWanderArea(quadrant);
        }
    }

    bool HasQuadrant()
    {
        if (quadrantResolved) return quadrantValid;
        quadrantResolved = true;

        GameObject map = GameObject.Find(mapName);
        Renderer floor = map != null ? map.GetComponent<Renderer>() : null;
        if (floor == null) { quadrantValid = false; return false; }

        Bounds b = floor.bounds;
        Vector3 c = b.center;
        Vector3 p = transform.position;

        // 스포너 위치로 사분면을 고른다. 담장에 붙지 않도록 바깥쪽만 여유를 준다
        float minX = p.x >= c.x ? c.x : b.min.x + areaInset;
        float maxX = p.x >= c.x ? b.max.x - areaInset : c.x;
        float minZ = p.z >= c.z ? c.z : b.min.z + areaInset;
        float maxZ = p.z >= c.z ? b.max.z - areaInset : c.z;

        quadrant = new Bounds();
        quadrant.SetMinMax(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, maxZ));
        quadrantValid = true;
        return true;
    }

    // 스포너 주변에서 비어 있는 자리를 찾는다. 다 차 있으면 원래 자리에 그냥 내보낸다
    Vector3 FindSpawnPoint()
    {
        Vector3 basePos = transform.position + spawnOffset;

        for (int i = 0; i < placementTries; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 p = basePos + new Vector3(offset.x, 0f, offset.y);
            if (!Physics.CheckSphere(p, clearRadius, ~0, QueryTriggerInteraction.Ignore)) return p;
        }
        return basePos;
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) quadrantResolved = false; // 에디터에서 위치를 옮기면 다시 계산
        if (!HasQuadrant()) return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(quadrant.center.x, 0.1f, quadrant.center.z),
                            new Vector3(quadrant.size.x, 0.1f, quadrant.size.z));
    }

    static void Prune()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
            if (alive[i] == null) alive.RemoveAt(i); // 죽은 개체는 자리를 비운다
    }
}
