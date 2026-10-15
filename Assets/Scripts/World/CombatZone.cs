using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// 결계로 잠기는 전투 구획 하나. 범위는 이 오브젝트 위치를 중심으로 가로(x) size.x, 세로(z) size.y.
// portalPosition의 포탈에서 E를 holdTime 동안 누르고 있으면 구획 안쪽으로 들어가고 결계(ZoneBarrier)가 선다.
// 들어가 있는 동안 구획 밖 몹은 없다(치우고 스포너를 멈춘다). 웨이브를 다 잡으면 마지막 몹 자리에 열쇠가 떨어지고,
// 그 열쇠를 다 주워야 결계가 풀린다. 클리어한 구획의 포탈은 다시 열리지 않는다
public class CombatZone : MonoBehaviour, IChargePad
{
    public enum State { Idle, Active, Cleared }

    public Vector2 size = new Vector2(80f, 50f);
    public int keyReward = 1; // 클리어하면 떨어지는 열쇠 수
    public Vector3 portalPosition; // 이 구획으로 들어가는 포탈 자리 (월드)

    [Header("포탈")]
    public GameObject portalPrefab;
    public float portalScale = 2.4f;
    public float interactRadius = 2.5f;
    public float holdTime = 0.5f;
    public float enterDepth = 4f; // 들어가면 포탈에서 구획 안쪽으로 이만큼 들어간 자리에 선다
    public Color barColor = new Color(1f, 0.6f, 0.15f, 1f);
    public bool debugKeys = true; // \ : 들어가 있는 구획을 클리어 처리 (테스트용)

    [Header("웨이브")]
    public int[] waveCounts = { 8, 10, 12 };
    public float bigZoneMultiplier = 1.5f; // 열쇠 2개 구획은 웨이브마다 이만큼 더 나온다
    public float waveRest = 3f;            // 한 웨이브를 다 잡고 다음 웨이브까지
    public float firstWaveDelay = 0.5f;    // 결계가 다 선 뒤 첫 웨이브까지
    public float keySpacing = 1.5f;        // 열쇠 2개 구획에서 열쇠끼리 간격
    public GameObject enemyPrefab;         // 비워 두면 맵 스포너의 것을 쓴다
    public float spawnInset = 3f;          // 결계에서 이만큼 안쪽부터
    public float spawnBand = 4f;           // 거기서 다시 이만큼 폭의 외곽 띠에서만 나온다
    public float spawnMinDistance = 8f;    // 플레이어 코앞에는 안 나온다

    [Header("미니보스 (마지막 웨이브와 함께 나온다)")]
    public float miniBossHealthMultiplier = 5f; // 정예 기본 체력 대비 (노말은 0.8배가 먼저 걸린다)
    public bool miniBossMatchPlayerSpeed = true; // 이동속도를 플레이어 기본 이동속도와 같게
    public float miniBossDamageMultiplier = 1f;
    public float miniBossScale = 1f;

    public static CombatZone Current { get; private set; } // 지금 갇혀 있는 구획

    // 정예 종류는 구획마다 안 겹치게 뽑는다. 마지막 구획에서는 남은 종류가 전부 나온다
    static readonly List<int> usedElites = new List<int>();
    static int clearedZones;
    int miniBossCount;

    readonly List<GameObject> waveMobs = new List<GameObject>();
    int wave;          // 나온 웨이브 수
    int spawned;       // 이 구획에서 지금까지 낸 몹 수
    float nextWaveTime = -1f;
    Vector3 lastMobPos;                     // 마지막으로 남았던 몹 자리 (열쇠가 떨어지는 곳)
    readonly List<GameObject> keys = new List<GameObject>();
    readonly List<MonoBehaviour> pausedByZone = new List<MonoBehaviour>();

    public State Status { get; private set; }

    Transform player;
    GameObject portal;
    float holdProgress;

    public float Progress { get { return holdTime > 0f ? Mathf.Clamp01(holdProgress / holdTime) : 0f; } }
    public Color BarColor { get { return barColor; } }
    public string Prompt { get { return CanEnter() ? ChargeCastBarUI.HoldPrompt("구획 진입") : null; } }

    void Awake()
    {
        // static이라 씬을 다시 불러도 남는다. 구획마다 한 번씩 불려도 결과는 같다
        usedElites.Clear();
        clearedZones = 0;
        Current = null;
    }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        if (portalPrefab != null) portal = SpawnPortal();
        if (enemyPrefab == null)
        {
            EnemySpawner s = FindFirstObjectByType<EnemySpawner>();
            if (s != null) enemyPrefab = s.enemyPrefab;
        }
    }

    void Update()
    {
        if (debugKeys && Current == this && Keyboard.current != null && Keyboard.current.backslashKey.wasPressedThisFrame)
        {
            // 테스트: 웨이브를 건너뛰고 열쇠를 바로 주운 것으로 처리
            if (keys.Count == 0) DropKeys(player.position);
            foreach (GameObject k in keys) if (k.activeSelf) KeyItemSpawner.Instance.Collect(k);
            return;
        }
        if (Status == State.Active)
        {
            UpdateWaves();
            return;
        }

        bool held = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        if (!held || !CanEnter())
        {
            holdProgress = 0f;
            return;
        }
        holdProgress += Time.deltaTime;
        if (holdProgress >= holdTime) Enter();
    }

    bool CanEnter()
    {
        if (Status != State.Idle || Current != null || player == null) return false;
        Vector3 d = player.position - portalPosition;
        d.y = 0f;
        return d.sqrMagnitude <= interactRadius * interactRadius;
    }

    void Enter()
    {
        holdProgress = 0f;
        Status = State.Active;
        Current = this;
        player.position = portalPosition + Inward() * enterDepth;
        if (portal != null) portal.SetActive(false);
        ZoneBarrier barrier = GetComponent<ZoneBarrier>();
        if (barrier != null) barrier.SetRaised(true);
        wave = 0;
        spawned = 0;
        keys.Clear();
        PickMiniBosses();
        nextWaveTime = Time.time + (barrier != null ? barrier.RaiseDuration : 0f) + firstWaveDelay;
        HoldOutsideMobs(true);
    }

    void Clear()
    {
        Status = State.Cleared;
        clearedZones++;
        if (Current == this) Current = null;
        ZoneBarrier barrier = GetComponent<ZoneBarrier>();
        if (barrier != null) barrier.SetRaised(false);
        foreach (GameObject m in waveMobs) if (m != null) Destroy(m); // 테스트 키로 넘겼을 때 남은 몹
        waveMobs.Clear();
        if (KillGauge.Instance != null) KillGauge.Instance.ShowZoneProgress(0, 0);
        HoldOutsideMobs(false);
    }

    // 들어가 있는 동안 맵 몹을 치우고 맵 스포너·정예 스포너를 멈춘다. 나오면 내가 멈춘 것만 되돌린다
    // (옥상/지상에 따라 AreaManager가 따로 재워 둔 스포너는 건드리지 않는다)
    void HoldOutsideMobs(bool hold)
    {
        if (!hold)
        {
            foreach (MonoBehaviour s in pausedByZone)
            {
                if (s is EnemySpawner) ((EnemySpawner)s).paused = false;
                else if (s is EliteSpawner) ((EliteSpawner)s).paused = false;
            }
            pausedByZone.Clear();
            return;
        }
        foreach (EnemySpawner s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            if (!s.paused) { s.paused = true; pausedByZone.Add(s); }
        foreach (EliteSpawner s in FindObjectsByType<EliteSpawner>(FindObjectsSortMode.None))
            if (!s.paused) { s.paused = true; pausedByZone.Add(s); }
        foreach (EnemyAI e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            if (e.GetComponent<BossDefeat>() == null) Destroy(e.gameObject);
    }

    // 마지막 몹이 쓰러진 자리에 열쇠를 떨군다. 여러 개면 옆으로 나란히
    void DropKeys(Vector3 at)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(at, out hit, 3f, NavMesh.AllAreas)) at = hit.position;
        for (int i = 0; i < keyReward; i++)
        {
            Vector3 p = at + Vector3.right * (i - (keyReward - 1) * 0.5f) * keySpacing + Vector3.up * 0.8f;
            keys.Add(KeyItemSpawner.Instance.SpawnKeyAt(p));
        }
    }

    int WaveSize(int i)
    {
        return Mathf.RoundToInt(waveCounts[i] * (keyReward >= 2 ? bigZoneMultiplier : 1f));
    }

    // 전멸하면 waveRest 뒤 다음 웨이브. 마지막 웨이브까지 다 잡으면 열쇠가 떨어지고, 다 주우면 클리어
    void UpdateWaves()
    {
        if (keys.Count > 0)
        {
            if (keys.TrueForAll(k => k == null || !k.activeSelf)) Clear();
            return;
        }
        waveMobs.RemoveAll(m => m == null);
        foreach (GameObject m in waveMobs) lastMobPos = m.transform.position; // 하나 남으면 그 몹 자리
        int total = miniBossCount;
        for (int i = 0; i < waveCounts.Length; i++) total += WaveSize(i);
        if (KillGauge.Instance != null) KillGauge.Instance.ShowZoneProgress(spawned - waveMobs.Count, total);

        if (nextWaveTime >= 0f)
        {
            if (Time.time < nextWaveTime) return;
            nextWaveTime = -1f;
            SpawnWave(WaveSize(wave));
            wave++;
            if (wave == waveCounts.Length) SpawnMiniBosses();
            return;
        }
        if (waveMobs.Count > 0) return;
        if (wave < waveCounts.Length) nextWaveTime = Time.time + waveRest;
        else DropKeys(lastMobPos);
    }

    void SpawnWave(int count)
    {
        if (enemyPrefab == null) return;
        Bounds area = new Bounds(transform.position, new Vector3(size.x - spawnInset * 2f, 10f, size.y - spawnInset * 2f));
        for (int i = 0; i < count; i++)
        {
            GameObject e = Instantiate(enemyPrefab, SpawnPoint(area), Quaternion.identity);
            waveMobs.Add(e);
            spawned++;
            EnemyAI ai = e.GetComponent<EnemyAI>();
            if (ai == null) continue;
            ai.SetWanderArea(area);
            ai.alwaysHunt = true; // 구획 안 웨이브는 나오자마자 끝까지 쫓는다
            ai.ForceAttack();
        }
    }

    // 들어갈 때 이번 구획 미니보스 종류를 정한다: 아직 안 나온 정예 중 무작위 1종, 마지막 구획은 남은 종류 전부.
    // 자폭 정예(F)는 항상 두 마리가 짝으로 나온다. 게이지 전체 수에 처음부터 맞게 들어가도록 여기서 센다
    readonly List<int> miniBossTypes = new List<int>();
    void PickMiniBosses()
    {
        miniBossTypes.Clear();
        EliteSpawner es = FindFirstObjectByType<EliteSpawner>();
        if (es == null) { miniBossCount = 0; return; }
        List<int> left = new List<int>();
        for (int i = 0; i < es.elitePrefabs.Length; i++) if (!usedElites.Contains(i)) left.Add(i);
        bool lastZone = clearedZones >= FindObjectsByType<CombatZone>(FindObjectsSortMode.None).Length - 1;
        int kinds = lastZone ? left.Count : Mathf.Min(1, left.Count);
        for (int n = 0; n < kinds; n++)
        {
            int type = left[Random.Range(0, left.Count)];
            left.Remove(type);
            usedElites.Add(type);
            int copies = es.elitePrefabs[type].GetComponent<EliteSelfDestructPattern>() != null ? 2 : 1;
            for (int c = 0; c < copies; c++) miniBossTypes.Add(type);
        }
        miniBossCount = miniBossTypes.Count;
    }

    // 정해 둔 미니보스를 강화해서 낸다. 웨이브 몹과 같이 잡아야 끝난다
    void SpawnMiniBosses()
    {
        EliteSpawner es = FindFirstObjectByType<EliteSpawner>();
        if (es == null) return;
        Bounds area = new Bounds(transform.position, new Vector3(size.x - spawnInset * 2f, 10f, size.y - spawnInset * 2f));
        foreach (int type in miniBossTypes)
        {
            GameObject e = Instantiate(es.elitePrefabs[type], SpawnPoint(area), Quaternion.identity);
            e.name = es.elitePrefabs[type].name + " (MiniBoss)";
            waveMobs.Add(e);
            spawned++;

            Health h = e.GetComponent<Health>();
            if (h != null) h.SetMaxHealth(h.maxHealth * miniBossHealthMultiplier);
            e.transform.localScale *= miniBossScale;
            EnemySpriteAnimator anim = e.GetComponentInChildren<EnemySpriteAnimator>();
            if (anim != null) anim.sizeMultiplier = miniBossScale;
            EnemyAI ai = e.GetComponent<EnemyAI>();
            if (ai == null) continue;
            NormalDifficulty.ScaleEliteDamage(ai, miniBossDamageMultiplier);
            TopDownMove pm = player != null ? player.GetComponent<TopDownMove>() : null;
            if (miniBossMatchPlayerSpeed && pm != null) ai.attackSpeed = ai.wanderSpeed = pm.moveSpeed;
            ai.SetWanderArea(area);
            ai.alwaysHunt = true;
            ai.ForceAttack();
        }
    }

    // 구획 외곽 띠(area 테두리에서 안쪽으로 spawnBand) 위, 플레이어에서 떨어진 빈자리.
    // 건물 안에 따로 떨어진 내비메시 섬에 나오면 갇히므로, 플레이어까지 길이 이어진 자리만 쓴다
    Vector3 SpawnPoint(Bounds area)
    {
        NavMeshHit pHit = new NavMeshHit();
        bool hasPlayer = player != null && NavMesh.SamplePosition(player.position, out pHit, 3f, NavMesh.AllAreas);
        Vector3 playerNav = pHit.position;
        NavMeshPath path = new NavMeshPath();
        float w = area.size.x, d = area.size.z;
        for (int i = 0; i < 40; i++)
        {
            // 둘레 위 한 점을 고르고 안쪽으로 0~spawnBand 들인다
            float t = Random.Range(0f, 2f * (w + d)), inset = Random.Range(0f, spawnBand);
            Vector3 p;
            if (t < w) p = new Vector3(area.min.x + t, 0f, area.min.z + inset);
            else if (t < w + d) p = new Vector3(area.max.x - inset, 0f, area.min.z + (t - w));
            else if (t < 2f * w + d) p = new Vector3(area.max.x - (t - w - d), 0f, area.max.z - inset);
            else p = new Vector3(area.min.x + inset, 0f, area.max.z - (t - 2f * w - d));
            p.y = transform.position.y;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 1.5f, NavMesh.AllAreas) || hit.position.y > 3f) continue;
            if (hasPlayer && (hit.position - player.position).sqrMagnitude < spawnMinDistance * spawnMinDistance) continue;
            if (hasPlayer && (!NavMesh.CalculatePath(hit.position, playerNav, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)) continue;
            Vector3 at = hit.position + Vector3.up;
            if (Physics.CheckSphere(at, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
            return at;
        }
        return portalPosition + Inward() * enterDepth + Vector3.up; // 플레이어가 들어온 자리는 확실히 이어져 있다
    }

    void OnDestroy()
    {
        if (Current == this) Current = null; // static이라 씬을 다시 불러도 남는다
    }

    // 포탈에서 가장 가까운 구획 변의 안쪽 방향
    Vector3 Inward()
    {
        Vector3 d = portalPosition - transform.position;
        float ex = size.x * 0.5f - Mathf.Abs(d.x), ez = size.y * 0.5f - Mathf.Abs(d.z); // 각 변까지 남은 거리
        return ex < ez ? new Vector3(-Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(d.z));
    }

    // 고리가 구획 안쪽을 바라보게 세우고, 아래 끝이 바닥 위 0.1에 오게 띄운다
    GameObject SpawnPortal()
    {
        GameObject go = Instantiate(portalPrefab, portalPosition, Quaternion.LookRotation(Inward()), transform);
        go.transform.localScale = Vector3.one * portalScale;
        float ground = portalPosition.y;
        RaycastHit hit;
        if (Physics.Raycast(portalPosition + Vector3.up * 5f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
        go.transform.position = new Vector3(portalPosition.x, ground + 0.96f * portalScale + 0.1f, portalPosition.z); // 고리 반지름 ≈ 0.96
        return go;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.2f, size.y));
        Gizmos.DrawWireSphere(portalPosition, 1.5f);
    }
}
