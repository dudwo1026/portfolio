using UnityEngine;
using UnityEngine.UI;

// 처치 게이지. 일반몹 1, 정예몹 10씩 오르고, 일정 구간마다 정예 스포너가 열린다.
// 가득 차면 게이지바가 빛난다 (가득 찼을 때의 이벤트는 추후 구현).
public class KillGauge : MonoBehaviour
{
    public static KillGauge Instance;

    [Header("게이지")]
    public float maxValue = 300f;
    public float normalKillValue = 1f;
    public float eliteKillValue = 5f;

    [Header("정예 스포너 개방")]
    public float[] unlockAt = { 50f, 100f, 200f }; // 게이지가 각 값에 닿을 때마다
    public int spawnersPerStep = 2;     // 이만큼씩 열린다 (50 → 2, 100 → 4, 200 → 6)
    public int maxSpawners = 6;
    public EliteSpawner eliteSpawner;

    // ───── 테스트용. 출시 빌드에서는 debugKeys를 꺼서 통째로 죽인다 ─────
    [Header("테스트용 키")]
    public bool debugKeys = true;
    public float debugAddAmount = 100f;  // / 키로 한 번에 차오르는 양
    public float debugSpawnDistance = 10f; // 숫자패드 1~6 소환 거리 (플레이어 남쪽)
    public float debugSpawnSpread = 2f;    // 연타했을 때 겹치지 않게 좌우로 흩는 폭
    public float debugSpawnLift = 1.2f;    // 바닥에서 띄우는 높이. 지면에 박힌 채 생기지 않도록
    Transform debugPlayer;
    // ──────────────────────────────────────────────────────────────

    [Header("UI")]
    public Image fill;
    public Text label;
    public Image glow;                  // 가득 찼을 때만 보이는 발광 테두리
    public Color glowColor = new Color(1f, 0.95f, 0.4f, 1f);
    public float glowMinAlpha = 0.25f;
    public float glowMaxAlpha = 0.85f;
    public float glowPulseSpeed = 3f;

    [Header("공세 경보")]
    public float[] warnAt = { 50f, 100f, 200f, 250f }; // 게이지가 각 값을 넘는 순간 경보
    public string warnMessage = "적들의 공세가 더욱 거세집니다";
    public HudMessage warn;              // 화면 중상단, 빨간 글씨

    public float Value { get; private set; }
    public bool IsFull { get { return Value >= maxValue; } }
    public int UnlockedSpawners { get; private set; }

    public System.Action onFull; // 추후 이벤트 연결 지점

    // 이 값부터 정예는 발견 절차 없이 생성되자마자 추격한다 (넘는 순간 살아 있는 정예도 전부)
    public float eliteHuntAt = 250f;
    public bool EliteHunt { get { return Value >= eliteHuntAt; } }

    bool wasFull;
    bool spawnsPaused;   // [ 키 상태
    float clearTimer;
    readonly System.Collections.Generic.List<EnemySpawner> pausedByMe = new System.Collections.Generic.List<EnemySpawner>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UnlockedSpawners = 0;
        if (eliteSpawner != null) eliteSpawner.SetActiveSlots(0); // 게이지가 차기 전에는 정예가 나오지 않는다
        Refresh();
    }

    // 적이 죽을 때 EnemyAI가 호출한다
    public static void ReportKill(bool isElite)
    {
        if (Instance == null) return;
        Instance.Add(isElite ? Instance.eliteKillValue : Instance.normalKillValue);
    }

    public void Add(float amount)
    {
        if (amount <= 0f) return;

        float before = Value;
        Value = Mathf.Clamp(Value + amount, 0f, maxValue);
        foreach (float at in warnAt)
            if (before < at && Value >= at && warn != null) warn.Show(warnMessage); // 여러 개를 한 번에 넘어도 같은 문구라 한 번 뜬 것과 같다
        if (before < eliteHuntAt && EliteHunt)
            foreach (EnemyAI e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e.isElite) e.ForceAttack();
        UpdateUnlocks();
        Refresh();

        if (IsFull && !wasFull)
        {
            wasFull = true;
            if (onFull != null) onFull();
        }
        else if (!IsFull)
        {
            wasFull = false;
        }
    }

    void UpdateUnlocks()
    {
        int steps = 0;
        foreach (float at in unlockAt) if (Value >= at) steps++;
        int want = Mathf.Clamp(steps * spawnersPerStep, 0, maxSpawners);
        if (want == UnlockedSpawners) return;

        UnlockedSpawners = want;
        if (eliteSpawner != null) eliteSpawner.SetActiveSlots(want);
    }

    void Refresh()
    {
        if (fill != null) fill.fillAmount = maxValue > 0f ? Value / maxValue : 0f;
        if (label != null) label.text = Mathf.FloorToInt(Value) + "/" + Mathf.FloorToInt(maxValue);
    }

    // 테스트용 키. 게임 로직이 아니므로 Update 본문과 섞지 않고 여기에 모아 둔다
    //   0 : 게이지를 가득 채운다
    //   / : 게이지를 debugAddAmount(기본 100)만큼 올린다 (숫자패드 / 도 동일)
    //   [ : 스폰을 멈추고 살아있는 몹을 지운다 / 다시 누르면 재개 (맵을 둘러볼 때)
    //   ] : 주무기 돌격소총 ↔ 레이저 전환 (임시)
    //   숫자패드 1~6 : 정예 a~f를 플레이어 남쪽 10유닛에 소환 (패턴을 하나씩 확인할 때)
    //   ' : 게이지를 채우고 열쇠를 전부 모은 것으로 처리해 포탈을 바로 연다
    void HandleDebugKeys()
    {
        if (!debugKeys) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (kb.digit0Key.wasPressedThisFrame) Add(maxValue);
        if (kb.slashKey.wasPressedThisFrame || kb.numpadDivideKey.wasPressedThisFrame) Add(debugAddAmount);
        if (kb.leftBracketKey.wasPressedThisFrame) ToggleSpawns();
        if (kb.rightBracketKey.wasPressedThisFrame)
        {
            PlayerCombat pc = FindFirstObjectByType<PlayerCombat>();
            if (pc != null)
            {
                pc.primaryWeapon = pc.primaryWeapon == PrimaryWeapon.Rifle ? PrimaryWeapon.Laser : PrimaryWeapon.Rifle;
                Debug.Log("[Debug] 주무기: " + pc.primaryWeapon);
            }
        }
        if (kb.quoteKey.wasPressedThisFrame)
        {
            Add(maxValue); // 포탈은 게이지 가득 + 열쇠 전부가 조건이다
            if (KeyItemSpawner.Instance != null) KeyItemSpawner.Instance.DebugCollectAll();
        }

        if (kb.numpad1Key.wasPressedThisFrame) DebugSpawnElite(0);
        if (kb.numpad2Key.wasPressedThisFrame) DebugSpawnElite(1);
        if (kb.numpad3Key.wasPressedThisFrame) DebugSpawnElite(2);
        if (kb.numpad4Key.wasPressedThisFrame) DebugSpawnElite(3);
        if (kb.numpad5Key.wasPressedThisFrame) DebugSpawnElite(4);
        if (kb.numpad6Key.wasPressedThisFrame) DebugSpawnElite(5);

        // 옥상으로 올라가면 AreaManager가 스포너 설정을 다시 깔기 때문에 몹이 또 나온다. 주기적으로 치운다
        if (!spawnsPaused) return;
        clearTimer -= Time.unscaledDeltaTime;
        if (clearTimer > 0f) return;
        clearTimer = 1f;
        ClearMobs();
    }

    // 정예 한 마리를 플레이어 남쪽(-Z)에 바로 세운다.
    // 프리팹은 EliteSpawner가 들고 있는 걸 그대로 쓴다 — 참조를 두 벌 두면 한쪽만 바뀌어 어긋난다
    void DebugSpawnElite(int index)
    {
        if (eliteSpawner == null || eliteSpawner.elitePrefabs == null) return;
        if (index < 0 || index >= eliteSpawner.elitePrefabs.Length) return;
        GameObject prefab = eliteSpawner.elitePrefabs[index];
        if (prefab == null) return;

        if (debugPlayer == null)
        {
            TopDownMove mover = FindFirstObjectByType<TopDownMove>();
            if (mover != null) debugPlayer = mover.transform;
        }
        if (debugPlayer == null) return;

        // 같은 키를 연타해도 겹쳐 쌓이지 않도록 좌우로만 조금 흩는다. 남쪽이라는 건 유지된다
        Vector3 pos = debugPlayer.position + Vector3.back * debugSpawnDistance
                    + Vector3.right * Random.Range(-debugSpawnSpread, debugSpawnSpread);

        // 바닥을 찾아 발을 붙인다. 몹은 바닥으로 치지 않는다 —
        // 연타하면 먼저 나온 정예 머리 위에 다음 정예가 얹혀 공중에서 생긴다
        RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 20f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float groundY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hits[i].point.y > groundY) groundY = hits[i].point.y;
        }
        pos.y = groundY > float.NegativeInfinity ? groundY + debugSpawnLift : debugPlayer.position.y;

        GameObject e = Instantiate(prefab, pos, Quaternion.identity);
        e.name = prefab.name; // "(Clone)" 없이 원래 이름을 유지해 로그에서 알아보기 쉽게
    }

    void ToggleSpawns()
    {
        spawnsPaused = !spawnsPaused;

        if (!spawnsPaused)
        {
            // 내가 멈춘 것만 되돌린다. AreaManager가 옥상/지상에 따라 따로 재우는 스포너가 있어서
            // 전부 깨우면 그 설정이 망가진다
            for (int i = 0; i < pausedByMe.Count; i++)
                if (pausedByMe[i] != null) pausedByMe[i].paused = false;
            pausedByMe.Clear();
            if (eliteSpawner != null) eliteSpawner.paused = false;
            return;
        }

        pausedByMe.Clear();
        foreach (EnemySpawner s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            if (s.paused) continue;
            s.paused = true;
            pausedByMe.Add(s);
        }
        if (eliteSpawner != null) eliteSpawner.paused = true;
        ClearMobs();
    }

    void ClearMobs()
    {
        // 보스는 남긴다. 지우면 보스전이 통째로 날아간다
        EnemyAI[] alive = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < alive.Length; i++)
            if (alive[i].GetComponent<BossDefeat>() == null) Destroy(alive[i].gameObject);
    }

    void Update()
    {
        HandleDebugKeys();

        if (glow == null) return;

        if (!IsFull)
        {
            if (glow.enabled) glow.enabled = false;
            return;
        }

        if (!glow.enabled) glow.enabled = true;
        float t = (Mathf.Sin(Time.unscaledTime * glowPulseSpeed) + 1f) * 0.5f;
        Color c = glowColor;
        c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);
        glow.color = c;
    }
}
