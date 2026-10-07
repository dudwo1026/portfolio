using System.Collections.Generic;
using UnityEngine;

// 정예몹 관리: 처치 게이지가 열어준 수만큼만 동시에 유지한다.
// 어떤 종류가 나올지는 소환하는 순간 '지금 맵에 없는 종류' 중에서 무작위로 고른다.
public class EliteSpawner : MonoBehaviour
{
    [System.NonSerialized] public bool paused; // 옥상에 올라가 있는 동안 정지
    public GameObject[] elitePrefabs;
    public float respawnDelay = 30f;

    [Header("생성 구역")]
    public Vector3 areaCenter = new Vector3(-5f, 0f, 100f);
    public Vector2 areaSize = new Vector2(160f, 150f); // 담장 안쪽으로 조금 여유를 둔 크기
    public float spawnY = 1f;
    public float clearRadius = 1.5f;      // 이 반경이 비어 있어야 그 자리를 쓴다
    public float minPlayerDistance = 20f; // 플레이어 코앞에 튀어나오지 않도록
    public int placementTries = 30;

    // 슬롯 = 동시에 살아 있을 수 있는 자리. 슬롯에 어떤 종류가 들어갈지는 고정이 아니다
    GameObject[] current;
    int[] currentType;   // 슬롯에 들어있는 종류 (-1 = 비어 있음)
    float[] timers;
    Transform player;

    readonly List<int> candidates = new List<int>();

    // 처치 게이지가 열어준 슬롯 수. 0이면 아무 정예도 나오지 않는다
    public int ActiveSlots { get; private set; }

    void Start()
    {
        EnsureArrays();
        FindPlayer();
        for (int i = 0; i < ActiveSlots; i++) Spawn(i);
    }

    void EnsureArrays()
    {
        if (current != null && current.Length == elitePrefabs.Length) return;

        current = new GameObject[elitePrefabs.Length];
        currentType = new int[elitePrefabs.Length];
        timers = new float[elitePrefabs.Length];
        for (int i = 0; i < currentType.Length; i++) currentType[i] = -1;
    }

    void FindPlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
    }

    // 게이지 쪽에서 호출. 새로 열린 슬롯은 기다리지 않고 바로 채운다
    public void SetActiveSlots(int count)
    {
        EnsureArrays();
        count = Mathf.Clamp(count, 0, elitePrefabs.Length);
        int before = ActiveSlots;
        ActiveSlots = count;

        for (int i = before; i < count; i++)
            if (current[i] == null) Spawn(i);
    }

    void Update()
    {
        if (paused) return;

        EnsureArrays();

        for (int i = 0; i < elitePrefabs.Length; i++)
        {
            if (i >= ActiveSlots) continue;   // 아직 안 열린 슬롯
            if (current[i] != null) continue; // 살아 있으면 건드리지 않는다

            if (currentType[i] >= 0) currentType[i] = -1; // 죽었으니 그 종류는 다시 후보가 된다

            if (timers[i] <= 0f)
            {
                timers[i] = respawnDelay; // 죽은 것을 확인한 시점부터 카운트 시작
                continue;
            }

            timers[i] -= Time.deltaTime;
            if (timers[i] <= 0f) Spawn(i);
        }
    }

    void Spawn(int slot)
    {
        EnsureArrays();
        if (slot < 0 || slot >= current.Length) return;

        int type = PickUnusedType();
        if (type < 0) return; // 남은 종류가 없다

        FindPlayer();

        current[slot] = Instantiate(elitePrefabs[type], FindSpawnPoint(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        current[slot].name = elitePrefabs[type].name; // "(Clone)" 없이 원래 이름 유지
        current[slot].SetActive(true);
        // 발견 전에는 배회한다. 생성 구역 밖(담장 너머)으로 흘러가지 않게 같은 구역에 묶는다
        EnemyAI ai = current[slot].GetComponent<EnemyAI>();
        if (ai != null) ai.SetWanderArea(new Bounds(areaCenter, new Vector3(areaSize.x, 100f, areaSize.y)));
        currentType[slot] = type;
        timers[slot] = 0f;
    }

    // 지금 맵에 없는 종류 중에서 무작위로 하나
    int PickUnusedType()
    {
        candidates.Clear();
        for (int type = 0; type < elitePrefabs.Length; type++)
        {
            if (elitePrefabs[type] == null) continue;

            bool inUse = false;
            for (int slot = 0; slot < current.Length; slot++)
            {
                if (current[slot] != null && currentType[slot] == type) { inUse = true; break; }
            }
            if (!inUse) candidates.Add(type);
        }

        if (candidates.Count == 0) return -1;
        return candidates[Random.Range(0, candidates.Count)];
    }

    Vector3 FindSpawnPoint()
    {
        Vector3 fallback = areaCenter + new Vector3(0f, spawnY, 0f);

        for (int i = 0; i < placementTries; i++)
        {
            Vector3 p = new Vector3(
                areaCenter.x + Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                spawnY,
                areaCenter.z + Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));

            // 구체 바닥이 지면에 닿으면 바닥 콜라이더가 잡혀서 전부 거부된다. 반경만큼 띄워서 검사한다
            if (Physics.CheckSphere(p + Vector3.up * clearRadius, clearRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (player != null)
            {
                Vector3 d = p - player.position;
                d.y = 0f;
                if (d.magnitude < minPlayerDistance) continue;
            }
            return p;
        }
        return fallback;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawWireCube(areaCenter + Vector3.up * spawnY, new Vector3(areaSize.x, 0.1f, areaSize.y));
    }
}
