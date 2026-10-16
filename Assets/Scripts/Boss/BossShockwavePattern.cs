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

    [Header("충격파 모델 (비워 두면 상자)")]
    public GameObject wavePrefab;
    public float wavePieceWidth = 4f;       // 칸 폭을 이 정도 너비로 나눠 파도를 나란히 세운다
    public float wavePieceOverlap = 1.3f;   // 이웃 파도와 겹치는 정도 (1이면 딱 붙는다)
    public float waveVisualThickness = 3f;  // 보이는 진행 방향 두께. 판정(waveThickness)과 따로
    public float waveSizeJitter = 0.15f;    // 파도마다 크기를 이 비율만큼 들쭉날쭉하게
    public float waveYaw = -90f;            // 파도 끝이 동쪽(진행 방향)으로 말리게
    public float waveBrightness = 1.15f;    // 색 밝기 배율
    public float waveGlow = 0.2f;           // 그림자 속에서도 어둡지 않게 텍스처 그대로 빛낸다. 0이면 없음
    Material waveMat;

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

        Vector3 size = new Vector3(waveThickness, waveHeight, laneDepth);
        Vector3 pos = new Vector3(startX, area.max.y + waveHeight * 0.5f, z);
        GameObject go;
        if (wavePrefab != null)
        {
            go = new GameObject("BossShockwave");
            go.transform.position = pos;
            BuildWaveModels(go.transform, size, area.max.y);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BossShockwave";
            Destroy(go.GetComponent<Collider>()); // 판정은 직접 한다
            go.transform.position = pos;
            go.transform.localScale = size;
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = waveColor;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        BossShockwave w = go.AddComponent<BossShockwave>();
        w.size = size;
        w.weakenDuration = waveWeakenDuration;
        w.Init(ai.PlayerTransform, waveDamage, waveSpeed, area.max.x + waveThickness);
    }

    // 파도 모델 하나는 둥근 더미라 칸 폭만큼 늘리면 찌그러진다. 칸을 여러 칸으로 나눠 하나씩 세운다.
    // 회전 없는 받침을 월드 축으로 늘리고, 그 안에서 모델만 돌린다 (돌린 걸 늘리면 비틀린다)
    void BuildWaveModels(Transform root, Vector3 size, float groundY)
    {
        Quaternion yaw = Quaternion.Euler(0f, waveYaw, 0f);
        GameObject probe = Instantiate(wavePrefab, Vector3.zero, yaw * wavePrefab.transform.rotation);
        Vector3 unit = probe.GetComponentInChildren<Renderer>().bounds.size; // 배율 1일 때 월드 크기
        Destroy(probe);

        int n = Mathf.Max(1, Mathf.RoundToInt(size.z / wavePieceWidth));
        float cell = size.z / n;
        for (int i = 0; i < n; i++)
        {
            float k = 1f + Random.Range(-waveSizeJitter, waveSizeJitter);
            Transform holder = new GameObject("Wave").transform;
            holder.SetParent(root, false);
            GameObject model = Instantiate(wavePrefab, holder);
            model.transform.localRotation = yaw * wavePrefab.transform.rotation;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            holder.localScale = new Vector3(waveVisualThickness / unit.x, size.y * k / unit.y, cell * wavePieceOverlap * k / unit.z);

            // 칸 가운데, 바닥에 발을 붙인다
            Renderer r = model.GetComponentInChildren<Renderer>();
            Vector3 want = root.position + new Vector3(0f, 0f, (i + 0.5f) * cell - size.z * 0.5f);
            Bounds b = r.bounds;
            holder.position += new Vector3(want.x - b.center.x, groundY - b.min.y, want.z - b.center.z);
            r.sharedMaterial = WaveMaterial(r.sharedMaterial);
        }
    }

    Material WaveMaterial(Material src)
    {
        if (waveMat != null) return waveMat;
        waveMat = new Material(src);
        Color c = src.color * waveBrightness;
        c.a = 1f;
        waveMat.color = c;
        if (waveGlow > 0f && src.mainTexture != null)
        {
            waveMat.EnableKeyword("_EMISSION");
            waveMat.SetTexture("_EmissionMap", src.mainTexture);
            waveMat.SetColor("_EmissionColor", Color.white * waveGlow);
        }
        return waveMat;
    }
}
