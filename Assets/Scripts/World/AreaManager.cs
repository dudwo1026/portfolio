using UnityEngine;

// 지상(맵1)과 옥상(보스전)은 같은 씬에 있지만 동시에 돌지 않는다.
// 옥상에 올라가면 지상의 몹을 전부 제거하고 지상 스포너를 세운 뒤, 옥상 스포너만 돌린다.
public class AreaManager : MonoBehaviour
{
    public Transform player;
    public float roofHeight = 30f;     // 이 높이를 넘으면 옥상에 있는 것으로 본다

    [Header("옥상 컨텐츠")]
    public GameObject boss;            // 포탈을 타고 올라오기 전까지는 꺼 둔다
    public float roofGraceTime = 3f;   // 입장 직후 이만큼은 보스도 몹도 움직이지 않는다
    public float checkInterval = 0.25f;

    public bool PlayerOnRoof { get; private set; }

    // 보스를 잡고 나면 어느 쪽 스포너도 다시 돌지 않는다
    public static bool BossDefeated { get; private set; }

    float timer;
    bool applied;

    void Start()
    {
        BossDefeated = false; // static이라 플레이 세션 사이에 남을 수 있다

        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        // 시작 위치가 이미 옥상이면 바로 보스전 상태로 들어간다 (테스트 편의)
        Apply(player != null && player.position.y >= roofHeight);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        if (player == null) return;

        bool onRoof = player.position.y >= roofHeight;
        if (applied && onRoof == PlayerOnRoof) return;

        Apply(onRoof);
    }

    void Apply(bool onRoof)
    {
        bool changed = !applied || onRoof != PlayerOnRoof;
        PlayerOnRoof = onRoof;
        applied = true;

        // 옥상 스포너와 지상 스포너 중 한쪽만 돈다
        EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
            spawners[i].paused = BossDefeated || (spawners[i].roofSpawner ? !onRoof : onRoof);

        // 보스는 플레이어가 옥상에 올라와야 깨어난다 (포탈은 게이지가 가득 차야 열린다)
        if (boss != null) boss.SetActive(onRoof);

        // 정예 스포너는 지상 전용
        EliteSpawner[] elites = Object.FindObjectsByType<EliteSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < elites.Length; i++) elites[i].paused = BossDefeated || onRoof;

        if (changed && onRoof)
        {
            ClearEnemies();   // 올라간 순간 지상의 몹을 전부 치운다
            ApplyRoofGrace(); // 도착하자마자 얻어맞지 않도록 잠깐 숨 돌릴 시간을 준다
        }
    }

    // 옥상에 막 올라온 직후: 보스는 특수패턴/일반공격을 쉬고, 옥상 스포너도 첫 소환을 미룬다
    void ApplyRoofGrace()
    {
        if (roofGraceTime <= 0f) return;

        if (boss != null)
        {
            BossActionLock actionLock = boss.GetComponent<BossActionLock>();
            if (actionLock == null) actionLock = boss.AddComponent<BossActionLock>();
            actionLock.LockFor(roofGraceTime); // 특수패턴은 이 잠금을 이미 보고 있다

            BossMeleeAttack melee = boss.GetComponent<BossMeleeAttack>();
            if (melee != null) melee.DelayFor(roofGraceTime);
        }

        EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
            if (spawners[i].roofSpawner) spawners[i].DelayNextSpawn(roofGraceTime);
    }

    // 보스 처치 시 호출. 이후로는 Apply가 다시 돌아도 전부 멈춘 상태를 유지한다
    public static void NotifyBossDefeated()
    {
        if (BossDefeated) return;
        BossDefeated = true;

        EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++) spawners[i].paused = true;

        EliteSpawner[] elites = Object.FindObjectsByType<EliteSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < elites.Length; i++) elites[i].paused = true;
    }

    // 지상 몹만 치운다. 옥상에 미리 배치해 둔 보스나 옥상 스포너가 뽑은 몹은 건드리지 않는다
    void ClearEnemies()
    {
        EnemyAI[] all = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].transform.position.y < roofHeight) Destroy(all[i].gameObject);
    }
}
