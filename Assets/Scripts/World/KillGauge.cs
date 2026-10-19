using UnityEngine;
using UnityEngine.UI;

// 우상단 게이지 바. 구획(CombatZone)에 들어가 있는 동안만 보이고, 그 구획에서 잡은 수 / 전체 수를 그린다.
// 테스트용 키도 여기 모여 있다
public class KillGauge : MonoBehaviour
{
    public static KillGauge Instance;

    // ───── 테스트용. 출시 빌드에서는 debugKeys를 꺼서 통째로 죽인다 ─────
    [Header("테스트용 키")]
    public bool debugKeys = true;
    public EliteSpawner eliteSpawner;      // 숫자패드 1~6 정예 목록
    public float debugSpawnDistance = 10f; // 숫자패드 1~6 소환 거리 (플레이어 남쪽)
    public float debugSpawnSpread = 2f;    // 연타했을 때 겹치지 않게 좌우로 흩는 폭
    public float debugSpawnLift = 1.2f;    // 바닥에서 띄우는 높이. 지면에 박힌 채 생기지 않도록
    Transform debugPlayer;
    // ──────────────────────────────────────────────────────────────

    [Header("UI")]
    public Image fill;
    public Text label;
    public Image glow;                  // 구획을 다 잡았을 때만 보이는 발광 테두리
    public Color glowColor = new Color(1f, 0.95f, 0.4f, 1f);
    public float glowMinAlpha = 0.25f;
    public float glowMaxAlpha = 0.85f;
    public float glowPulseSpeed = 3f;

    bool spawnsPaused;   // [ 키 상태
    float clearTimer;
    readonly System.Collections.Generic.List<EnemySpawner> pausedByMe = new System.Collections.Generic.List<EnemySpawner>();

    int zoneKilled, zoneTotal;
    bool Full { get { return zoneTotal > 0 && zoneKilled >= zoneTotal; } }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Refresh();
    }

    // 구획이 매 프레임 알려 준다. total 0이면 구획 밖 → 바를 숨긴다
    public void ShowZoneProgress(int killed, int total)
    {
        if (killed == zoneKilled && total == zoneTotal) return;
        zoneKilled = killed;
        zoneTotal = total;
        Refresh();
    }

    void Refresh()
    {
        bool show = zoneTotal > 0;
        GameObject bar = fill != null && fill.transform.parent != null ? fill.transform.parent.gameObject : null;
        if (bar != null && bar.activeSelf != show) bar.SetActive(show);
        if (!show) return;
        if (fill != null) fill.fillAmount = (float)zoneKilled / zoneTotal;
        if (label != null) label.text = zoneKilled + "/" + zoneTotal;
    }

    // 테스트용 키. 게임 로직이 아니므로 Update 본문과 섞지 않고 여기에 모아 둔다
    //   0 : 플레이어 무적 + 무한탄창 켜기/끄기
    //   [ : 스폰을 멈추고 살아있는 몹을 지운다 / 다시 누르면 재개 (맵을 둘러볼 때)
    //   ] : 주무기 돌격소총 → 레이저 → 산탄총 순환 (임시)
    //   숫자패드 1~6 : 정예 a~f를 플레이어 남쪽 10유닛에 소환 (패턴을 하나씩 확인할 때)
    //   ' : 열쇠를 전부 모은 것으로 처리해 옥상 포탈을 바로 연다
    //   ; : 보조무기(권총) 피해 1000 켜기/끄기
    //   (\ : 들어가 있는 구획을 클리어 처리 — CombatZone)
    void HandleDebugKeys()
    {
        if (!debugKeys) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (kb.digit0Key.wasPressedThisFrame)
        {
            PlayerCombat pc = FindFirstObjectByType<PlayerCombat>();
            if (pc != null)
            {
                bool on = !pc.infiniteAmmo;
                pc.infiniteAmmo = on;
                Health h = pc.GetComponent<Health>();
                if (h != null) h.invulnerable = on;
                Debug.Log("[Debug] 무적 + 무한탄창: " + (on ? "ON" : "OFF"));
            }
        }
        if (kb.leftBracketKey.wasPressedThisFrame) ToggleSpawns();
        if (kb.rightBracketKey.wasPressedThisFrame)
        {
            PlayerCombat pc = FindFirstObjectByType<PlayerCombat>();
            if (pc != null)
            {
                // 소총 → 레이저 → 산탄총 → 소총
                pc.SetPrimaryWeapon((PrimaryWeapon)(((int)pc.primaryWeapon + 1) % 3));
                Debug.Log("[Debug] 주무기: " + pc.primaryWeapon);
            }
        }
        if (kb.quoteKey.wasPressedThisFrame && KeyItemSpawner.Instance != null) KeyItemSpawner.Instance.DebugCollectAll();
        if (kb.semicolonKey.wasPressedThisFrame)
        {
            PlayerCombat pc = FindFirstObjectByType<PlayerCombat>();
            if (pc != null)
            {
                pc.debugPistolDamage = !pc.debugPistolDamage;
                Debug.Log("[Debug] 권총 피해 1000: " + (pc.debugPistolDamage ? "ON" : "OFF"));
            }
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
            // 내가 멈춘 것만 되돌린다. AreaManager·구획이 따로 재우는 스포너가 있어서 전부 깨우면 그 설정이 망가진다
            for (int i = 0; i < pausedByMe.Count; i++)
                if (pausedByMe[i] != null) pausedByMe[i].paused = false;
            pausedByMe.Clear();
            return;
        }

        pausedByMe.Clear();
        foreach (EnemySpawner s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            if (s.paused) continue;
            s.paused = true;
            pausedByMe.Add(s);
        }
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

        if (!Full)
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
