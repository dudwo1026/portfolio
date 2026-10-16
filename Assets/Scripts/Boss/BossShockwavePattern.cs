using System.Collections;
using UnityEngine;

// 보스 특수패턴 5: 팔을 들어 바닥을 내리찍는다 (charge3 모션 한 번). 내리찍는 순간(6장) 첫 충격파가 나가고,
// 그 자세로 버티는 동안 jumpInterval마다 하나씩 더 나간다. 충격파는 서쪽 울타리에서 동쪽으로 가로지른다.
// 구역을 세로로 3등분해 한 칸씩 쓰고, 어느 칸이 먼저 오는지는 무작위.
[RequireComponent(typeof(EnemyAI))]
public class BossShockwavePattern : MonoBehaviour, IBossMotion
{
    // 스프라이트 모션(holdFrame 방식): 0→1 내리찍기 전(1~5장), 1→2 내리찍은 자세 유지(6장), 2→3 마무리(7~8장)
    public string MotionName { get { return "Shockwave"; } }
    public bool MotionActive { get { return running; } }
    public float MotionProgress
    {
        get
        {
            float t = Time.time - runStartTime;
            if (t < slamWindup) return t / Mathf.Max(0.01f, slamWindup);
            t -= slamWindup;
            if (t < runHold) return 1f + t / Mathf.Max(0.01f, runHold);
            t -= runHold;
            return 2f + Mathf.Clamp01(t / Mathf.Max(0.01f, slamRecovery));
        }
    }

    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public int jumpCount = 3;                // 충격파 개수 (예전 점프 패턴 이름 그대로 — 씬 값이 남게)
    public float jumpInterval = 1f;          // 충격파 간격
    public float intervalStepPerPhase = 0.1f;  // 페이즈마다 이만큼 짧아진다
    public float minInterval = 0.25f;
    public float slamWindup = 0.9f;          // 1~5장. 끝나는 순간 첫 충격파
    public float slamRecovery = 0.35f;       // 7~8장
    public float minHold = 0.2f;             // 마지막 충격파 뒤에도 6장을 이만큼 더 보여 준다 (바로 7장으로 넘어가면 마지막 충격파가 일어나는 자세에서 나간다)
    float runStartTime;
    float runHold;

    [Header("충격파")]
    public float waveDamage = 50f;
    public float waveWeakenDuration = 5f;  // 피격 시 허약 지속시간
    public float waveSpeed = 17.5f;
    public float waveThickness = 5f;
    public float waveHeight = 2f;
    public Color waveColor = new Color(1f, 0.5f, 0.1f);

    [Header("충격파 모델 (비워 두면 상자)")]
    public GameObject wavePrefab;
    public float wavePieceWidth = 2.5f;      // 칸 폭을 이 정도 너비로 나눠 파도를 나란히 세운다
    public float wavePieceOverlap = 1.6f;   // 이웃 파도와 겹치는 정도 (1이면 딱 붙는다)
    public float waveVisualThickness = 4.5f; // 보이는 진행 방향 두께. 판정(waveThickness)과 따로
    public float waveVisualHeight = 4f;     // 보이는 높이. 판정(waveHeight)과 따로
    public float waveSizeJitter = 0.15f;    // 파도마다 크기를 이 비율만큼 들쭉날쭉하게
    public float waveYaw = -60f;            // -90이면 파도 끝이 정동쪽(진행 방향)으로 말린다. 여기서 시계 방향 30도
    public float waveBrightness = 1.15f;    // 색 밝기 배율
    public float waveGlow = 0.2f;           // 그림자 속에서도 어둡지 않게 텍스처 그대로 빛낸다. 0이면 없음
    Material waveMat;

    [Header("충격파 이펙트 (비워 두면 없음)")]
    public GameObject trailEffect;              // 파도마다 하나씩 발밑에 달아 뒤로 남긴다 (먼지)
    public float trailEffectScale = 1f;
    public Color trailEffectColor = new Color(0.75f, 0.62f, 1f, 0.6f);
    public float trailLifetime = 0.8f;          // 먼지 입자 수명. 원본(2초)이면 파도 3개가 지나간 뒤 화면을 덮는다
    public float trailEmission = 0.5f;          // 먼지 뿜는 양 배율
    public GameObject splashEffect;             // 파도 앞에서 계속 터뜨린다 (물보라)
    public float splashEffectScale = 4f;
    public float splashInterval = 0.12f;
    public float splashForward = 2.5f;          // 파도 중심에서 이만큼 앞

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

        float interval = CurrentInterval();
        runHold = interval * (jumpCount - 1) + minHold;
        actionLock.LockFor(slamWindup + runHold + slamRecovery); // 모션 내내 다른 패턴 금지
        nextCastTime = Time.time + NextCooldown();
        StartCoroutine(Run(interval));
    }

    IEnumerator Run(float interval)
    {
        running = true;
        runStartTime = Time.time;

        int[] order = ShuffledLanes();
        yield return new WaitForSeconds(slamWindup); // 6장이 나오는 순간
        for (int i = 0; i < jumpCount; i++)
        {
            if (i > 0) yield return new WaitForSeconds(interval);
            SpawnWave(order[i % order.Length]);
        }

        float left = runStartTime + slamWindup + runHold + slamRecovery - Time.time;
        if (left > 0f) yield return new WaitForSeconds(left);
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

        AttachTrails(go.transform, size);

        BossShockwave w = go.AddComponent<BossShockwave>();
        w.size = size;
        w.weakenDuration = waveWeakenDuration;
        w.splashEffect = splashEffect;
        w.splashEffectScale = splashEffectScale;
        w.splashInterval = splashInterval;
        w.splashForward = splashForward;
        w.Init(ai.PlayerTransform, waveDamage, waveSpeed, area.max.x + waveThickness);
    }

    // 파도 모델 하나는 둥근 더미라 칸 폭만큼 늘리면 찌그러진다. 칸을 여러 칸으로 나눠 하나씩 세운다.
    // 받침을 돌리고 받침의 자기 축으로 늘린다 (늘린 받침 안에서 모델만 비스듬히 돌리면 비틀린다).
    // 모델 자기 축 기준: x = 칸을 가로지르는 폭, z = 파도가 나아가는 쪽 두께
    void BuildWaveModels(Transform root, Vector3 size, float groundY)
    {
        Quaternion yaw = Quaternion.Euler(0f, waveYaw, 0f);
        GameObject probe = Instantiate(wavePrefab, Vector3.zero, wavePrefab.transform.rotation);
        Vector3 unit = probe.GetComponentInChildren<Renderer>().bounds.size; // 배율 1, 회전 전 크기
        Destroy(probe);

        int n = Mathf.Max(1, Mathf.RoundToInt(size.z / wavePieceWidth));
        float cell = size.z / n;
        for (int i = 0; i < n; i++)
        {
            float k = 1f + Random.Range(-waveSizeJitter, waveSizeJitter);
            Transform holder = new GameObject("Wave").transform;
            holder.SetParent(root, false);
            holder.localRotation = yaw;
            GameObject model = Instantiate(wavePrefab, holder);
            model.transform.localRotation = wavePrefab.transform.rotation;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            holder.localScale = new Vector3(cell * wavePieceOverlap * k / unit.x, waveVisualHeight * k / unit.y, waveVisualThickness / unit.z);

            // 칸 가운데, 바닥에 발을 붙인다
            Renderer r = model.GetComponentInChildren<Renderer>();
            Vector3 want = root.position + new Vector3(0f, 0f, (i + 0.5f) * cell - size.z * 0.5f);
            Bounds b = r.bounds;
            holder.position += new Vector3(want.x - b.center.x, groundY - b.min.y, want.z - b.center.z);
            r.sharedMaterial = WaveMaterial(r.sharedMaterial);
        }
    }

    // 파도 칸마다 발밑에 먼지를 단다. 입자는 월드에 남아야 뒤로 꼬리가 생긴다
    void AttachTrails(Transform root, Vector3 size)
    {
        if (trailEffect == null) return;
        int n = Mathf.Max(1, Mathf.RoundToInt(size.z / wavePieceWidth));
        float cell = size.z / n;
        for (int i = 0; i < n; i++)
        {
            GameObject e = Instantiate(trailEffect, root);
            e.transform.localPosition = new Vector3(0f, -size.y * 0.5f + 0.2f, (i + 0.5f) * cell - size.z * 0.5f);
            e.transform.localRotation = Quaternion.identity;
            e.transform.localScale = Vector3.one * trailEffectScale;
            foreach (ParticleSystem ps in e.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule m = ps.main;
                m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startColor = trailEffectColor;
                if (trailLifetime > 0f) m.startLifetime = trailLifetime;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTimeMultiplier *= trailEmission;
                ps.Play();
            }
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
