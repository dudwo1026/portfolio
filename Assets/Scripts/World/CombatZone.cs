using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// 결계로 잠기는 전투 구획 하나. 범위는 이 오브젝트 위치를 중심으로 가로(x) size.x, 세로(z) size.y.
// portalPosition의 포탈에서 E를 holdTime 동안 누르고 있으면 구획 안쪽으로 들어가고 결계(ZoneBarrier)가 선다.
// 클리어하면 결계가 풀리고 포탈은 다시 열리지 않는다
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
    public float firstWaveDelay = 1f;
    public GameObject enemyPrefab;         // 비워 두면 맵 스포너의 것을 쓴다
    public float spawnInset = 3f;          // 결계에서 이만큼 안쪽에만 나온다
    public float spawnMinDistance = 8f;    // 플레이어 코앞에는 안 나온다

    public static CombatZone Current { get; private set; } // 지금 갇혀 있는 구획

    readonly List<GameObject> waveMobs = new List<GameObject>();
    int wave;          // 나온 웨이브 수
    int spawned;       // 이 구획에서 지금까지 낸 몹 수
    float nextWaveTime = -1f;

    public State Status { get; private set; }

    Transform player;
    GameObject portal;
    float holdProgress;

    public float Progress { get { return holdTime > 0f ? Mathf.Clamp01(holdProgress / holdTime) : 0f; } }
    public Color BarColor { get { return barColor; } }
    public string Prompt { get { return CanEnter() ? ChargeCastBarUI.HoldPrompt("구획 진입") : null; } }

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
            Clear();
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
        nextWaveTime = Time.time + firstWaveDelay;
    }

    public void Clear()
    {
        Status = State.Cleared;
        if (Current == this) Current = null;
        ZoneBarrier barrier = GetComponent<ZoneBarrier>();
        if (barrier != null) barrier.SetRaised(false);
        foreach (GameObject m in waveMobs) if (m != null) Destroy(m); // 테스트 키로 넘겼을 때 남은 몹
        waveMobs.Clear();
        if (KillGauge.Instance != null) KillGauge.Instance.ShowZoneProgress(0, 0);
    }

    int WaveSize(int i)
    {
        return Mathf.RoundToInt(waveCounts[i] * (keyReward >= 2 ? bigZoneMultiplier : 1f));
    }

    // 전멸하면 waveRest 뒤 다음 웨이브, 마지막 웨이브까지 다 잡으면 클리어
    void UpdateWaves()
    {
        waveMobs.RemoveAll(m => m == null);
        int total = 0;
        for (int i = 0; i < waveCounts.Length; i++) total += WaveSize(i);
        if (KillGauge.Instance != null) KillGauge.Instance.ShowZoneProgress(spawned - waveMobs.Count, total);

        if (nextWaveTime >= 0f)
        {
            if (Time.time < nextWaveTime) return;
            nextWaveTime = -1f;
            SpawnWave(WaveSize(wave));
            wave++;
            return;
        }
        if (waveMobs.Count > 0) return;
        if (wave < waveCounts.Length) nextWaveTime = Time.time + waveRest;
        else Clear();
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
            ai.ForceAttack(); // 구획 안 웨이브는 바로 덤빈다
        }
    }

    // 구획 안 내비메시 위, 플레이어에서 떨어진 빈자리. 건물 지붕(높이 3 이상)은 안 쓴다
    Vector3 SpawnPoint(Bounds area)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 p = new Vector3(Random.Range(area.min.x, area.max.x), transform.position.y, Random.Range(area.min.z, area.max.z));
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 3f, NavMesh.AllAreas) || hit.position.y > 3f) continue;
            if (player != null && (hit.position - player.position).sqrMagnitude < spawnMinDistance * spawnMinDistance) continue;
            Vector3 at = hit.position + Vector3.up;
            if (Physics.CheckSphere(at, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
            return at;
        }
        return transform.position + Vector3.up;
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
