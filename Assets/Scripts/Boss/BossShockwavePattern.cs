using System.Collections;
using UnityEngine;

// 보스 특수패턴 5: 제자리 점프 3회. 착지할 때마다 서쪽 울타리에서 동쪽으로 가로지르는 충격파가 하나씩 나간다.
// 구역을 세로로 3등분해 한 칸씩 쓰고, 어느 칸이 먼저 오는지는 무작위.
[RequireComponent(typeof(EnemyAI))]
public class BossShockwavePattern : MonoBehaviour
{
    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public int jumpCount = 3;
    public float jumpInterval = 1f;          // 점프 간격
    public float intervalStepPerPhase = 0.1f;  // 페이즈마다 이만큼 짧아진다
    public float minInterval = 0.25f;
    public float jumpHeight = 3f;
    public float jumpDuration = 0.35f;

    [Header("충격파")]
    public float waveDamage = 50f;
    public float waveWeakenDuration = 5f;  // 피격 시 허약 지속시간
    public float waveSpeed = 17.5f;
    public float waveThickness = 2f;
    public float waveHeight = 2f;
    public Color waveColor = new Color(1f, 0.5f, 0.1f);

    [Header("구역")]
    public string areaObjectName = "Building_4";
    public int lanes = 3;

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    Bounds area;
    bool areaReady;
    float nextCastTime;
    bool running;

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

    float CurrentInterval()
    {
        int phase = obstacles != null ? obstacles.TriggeredCount : 0;
        return Mathf.Max(minInterval, jumpInterval - intervalStepPerPhase * phase);
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
        if (running) return;
        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextCastTime) return;
        if (actionLock.IsLocked) return; // 다른 패턴이 보스를 묶고 있다

        actionLock.LockFor(CurrentInterval() * jumpCount); // 점프 3회 동안 다른 패턴 금지
        nextCastTime = Time.time + NextCooldown();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;

        int[] order = ShuffledLanes();
        float interval = CurrentInterval();

        for (int i = 0; i < jumpCount; i++)
        {
            yield return Jump();                 // 뛰었다 착지
            SpawnWave(order[i % order.Length]);  // 착지하는 순간 충격파

            float wait = interval - jumpDuration;
            if (wait > 0f) yield return new WaitForSeconds(wait);
        }

        running = false;
    }

    int[] ShuffledLanes()
    {
        int[] a = new int[lanes];
        for (int i = 0; i < lanes; i++) a[i] = i;
        for (int i = lanes - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = a[i]; a[i] = a[j]; a[j] = tmp;
        }
        return a;
    }

    IEnumerator Jump()
    {
        Vector3 ground = transform.position;
        float t = 0f;
        while (t < jumpDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / jumpDuration);
            float y = jumpHeight * 4f * n * (1f - n); // 포물선: 0에서 떠서 1에서 착지
            transform.position = ground + Vector3.up * y;
            yield return null;
        }
        transform.position = ground;
    }

    void SpawnWave(int lane)
    {
        float laneDepth = area.size.z / lanes;
        float z = area.min.z + laneDepth * (lane + 0.5f);
        float startX = area.min.x;   // 서쪽 울타리

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BossShockwave";
        Destroy(go.GetComponent<Collider>()); // 판정은 직접 한다
        go.transform.position = new Vector3(startX, area.max.y + waveHeight * 0.5f, z);
        go.transform.localScale = new Vector3(waveThickness, waveHeight, laneDepth);
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = waveColor;
        go.GetComponent<Renderer>().sharedMaterial = m;

        BossShockwave w = go.AddComponent<BossShockwave>();
        w.weakenDuration = waveWeakenDuration;
        w.Init(ai.PlayerTransform, waveDamage, waveSpeed, area.max.x + waveThickness);
    }
}
