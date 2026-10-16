using System.Collections;
using UnityEngine;

// 보스 '분신 패턴'. 구역 귀퉁이에 분신을 세우고 조준 → 확정 → 발사.
// 두 종류를 한 쿨타임으로 공유하며, 시전할 때마다 무작위로 고른다.
//   정조준형: 플레이어를 그대로 겨눈다
//   페이크형: 정조준하다 막판에 홱 틀어 빗맞춘다 (구역이 두 번 줄어든 뒤 해금)
[RequireComponent(typeof(EnemyAI))]
public class BossClonePattern : MonoBehaviour
{
    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public float cloneDamage = 20f;
    public Vector3 cloneScale = new Vector3(4f, 4f, 4f);
    public float cloneSpeed = 60f;
    public float cloneLifeTime = 6f;
    public float aimDuration = 2f;

    [Header("정조준형")]
    public float lockDelay = 1f;

    [Header("페이크형")]
    public int fakeUnlockStages = 2;    // 장애물이 이만큼 깔린 뒤부터 등장
    public float fakeChance = 0.4f;     // 해금 후 페이크가 뽑힐 확률
    public float swingDuration = 0.3f;  // 조준을 홱 트는 시간
    public float fakeLockDelay = 0.6f;  // 확정 후 발사까지
    public float missDistance = 4f;     // 플레이어 오른쪽으로 비껴가는 거리

    [Header("페이크형 후속타 (두더지)")]
    public bool moleEnabled = true;
    public float moleDelay = 1.2f;      // 분신이 지나간 뒤 솟아오르기까지
    public float moleDamage = 40f;
    public float moleRadius = 5f;
    public Vector3 moleScale = new Vector3(6f, 6f, 6f);

    [Header("생성 구역")]
    public string areaObjectName = "Building_4";
    public float inset = 5f;
    public float spawnHeight = 2.2f;

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    float nextCastTime;
    Bounds area;
    bool areaReady;
    bool fakeSeen;        // 해금을 인지했는지
    bool forceFakeNext;   // 해금 직후 첫 시전은 반드시 페이크

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        obstacles = GetComponent<BossObstaclePattern>();
        actionLock = GetComponent<BossActionLock>();
        if (actionLock == null) actionLock = gameObject.AddComponent<BossActionLock>();
    }

    void Start()
    {
        ResolveArea();
        nextCastTime = Time.time + NextCooldown();
    }

    void ResolveArea()
    {
        GameObject go = GameObject.Find(areaObjectName);
        Renderer r = go != null ? go.GetComponent<Renderer>() : null;
        if (r == null) return;
        area = r.bounds;
        areaReady = true;
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
        if (!areaReady) { ResolveArea(); return; }

        // 해금되는 순간을 붙잡아 둔다. 다음 시전은 무조건 페이크
        bool fakeAvailable = obstacles != null && obstacles.TriggeredCount >= fakeUnlockStages;
        if (fakeAvailable && !fakeSeen)
        {
            fakeSeen = true;
            forceFakeNext = true;
        }

        if (ai.State != EnemyState.Attack) return;
        if (ai.PlayerTransform == null) return;
        if (Time.time < nextCastTime) return;
        if (actionLock.IsLocked) return;

        bool useFake = fakeAvailable && (forceFakeNext || Random.value < fakeChance);
        if (useFake) forceFakeNext = false;

        // 경로가 확정될 때까지는 다른 패턴을 쓰지 못한다
        actionLock.LockFor(aimDuration + (useFake ? swingDuration : 0f));

        nextCastTime = Time.time + NextCooldown();
        Cast(useFake);
    }

    void Cast(bool useFake)
    {
        float roofY = area.max.y;
        float y = roofY + spawnHeight;
        float minX = area.min.x + inset;
        float minZ = area.min.z + inset, maxZ = area.max.z - inset;

        // 장애물이 동쪽을 막고 나면 그 안쪽만 쓴다. 안 그러면 막힌 자리로 밀린 분신끼리 겹친다
        float maxX = FreeEastEdge(y, (minZ + maxZ) * 0.5f) - inset;
        if (maxX <= minX) maxX = minX;
        float cx = (minX + maxX) * 0.5f;

        // 네 귀퉁이 + 남북 변 중앙 = 6곳
        Vector3[] spots = {
            new Vector3(minX, y, minZ),
            new Vector3(minX, y, maxZ),
            new Vector3(maxX, y, minZ),
            new Vector3(maxX, y, maxZ),
            new Vector3(cx,   y, minZ),
            new Vector3(cx,   y, maxZ)
        };

        for (int i = 0; i < spots.Length; i++) SpawnClone(spots[i], useFake);

        // 마지막 장애물까지 깔린 뒤에는 동쪽 변 중앙에 한 기 더 (퇴로 쪽을 덮는다)
        if (obstacles != null && obstacles.AllZonesSpawned)
            SpawnClone(new Vector3(maxX, y, (minZ + maxZ) * 0.5f), useFake);

        // 페이크는 빗맞추고 끝나지 않는다. 분신이 지나간 자리에서 본체가 솟아오른다
        if (useFake && moleEnabled) StartCoroutine(MoleRoutine(spots));
    }

    // 분신이 플레이어를 스쳐 지나간 뒤, 그 자리에서 보스가 튀어오른다
    IEnumerator MoleRoutine(Vector3[] spots)
    {
        yield return new WaitForSeconds(aimDuration + swingDuration + fakeLockDelay); // 발사까지

        Transform p = ai.PlayerTransform;
        if (p == null) yield break;

        // 가장 먼 분신까지 플레이어 자리를 지나칠 때까지 기다린다
        float travel = 0f;
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 d = p.position - spots[i];
            d.y = 0f;
            if (d.magnitude > travel) travel = d.magnitude;
        }
        yield return new WaitForSeconds(cloneSpeed > 0f ? travel / cloneSpeed : 0f);

        p = ai.PlayerTransform;
        if (p == null) yield break;
        SpawnMole(p.position); // 분신이 지나간 자리 = 플레이어가 피해 선 자리
    }

    void SpawnMole(Vector3 spot)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BossMole";
        Destroy(go.GetComponent<Collider>()); // 플레이어를 밀어내지 않는다. 판정은 거리로 직접 본다
        go.transform.position = new Vector3(spot.x, area.max.y, spot.z);
        go.transform.localScale = moleScale;

        BossMoleStrike mole = go.AddComponent<BossMoleStrike>();
        mole.warningTime = moleDelay;
        mole.damage = moleDamage;
        mole.hitRadius = moleRadius;

        Renderer src = GetComponent<Renderer>();
        mole.Init(ai.PlayerTransform, area.max.y, src != null ? src.sharedMaterial : null);
    }

    // 동쪽에서 서쪽으로 훑어 막히지 않은 첫 지점을 찾는다 (장애물이 쌓일수록 서쪽으로 당겨진다)
    float FreeEastEdge(float y, float z)
    {
        Vector3 half = cloneScale * 0.45f;
        for (float x = area.max.x; x > area.min.x; x -= 1f)
        {
            if (!Physics.CheckBox(new Vector3(x, y, z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                return x;
        }
        return area.min.x;
    }

    void SpawnClone(Vector3 pos, bool useFake)
    {
        // 중심이 장애물 안이면 그 분신은 아예 나오지 않는다 (막힌 자리에 끼는 것 방지)
        if (Physics.CheckSphere(pos, 0.1f, ~0, QueryTriggerInteraction.Ignore)) return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BossClone";
        go.transform.position = pos;
        go.transform.localScale = cloneScale;
        go.GetComponent<Collider>().isTrigger = true; // 물리로 밀리지 않게. 지형 충돌은 BoxCast로 직접 본다

        Renderer src = GetComponent<Renderer>();
        if (src != null) go.GetComponent<Renderer>().sharedMaterial = src.sharedMaterial;

        BossClone clone = go.AddComponent<BossClone>();
        clone.aimDuration = aimDuration;
        clone.speed = cloneSpeed;
        clone.lifeTime = cloneLifeTime;
        clone.fakeAim = useFake;
        clone.lockDelay = useFake ? fakeLockDelay : lockDelay;
        clone.swingDuration = swingDuration;
        clone.missDistance = missDistance;
        clone.Init(ai.PlayerTransform, cloneDamage, area.max.y, gameObject);
    }
}
