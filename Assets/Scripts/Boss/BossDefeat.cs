using UnityEngine;

// 보스가 쓰러지면 더 이상 몹이 나오지 않고(지상·옥상 스포너 전부 정지), 남아 있던 몹도 전부 사라진다.
// 보스가 깔아 둔 장애물·분신·경고 표시 같은 잔해도 전부 치운다.
// 보스 본체는 모든 행동을 멈추고 사망 모션(Death)을 재생한 뒤 사라진다 (EnemyAI는 보스를 바로 지우지 않는다).
[RequireComponent(typeof(Health))]
public class BossDefeat : MonoBehaviour, IBossMotion
{
    public string MotionName { get { return "Death"; } }
    public bool MotionActive { get { return dying; } }
    public float MotionProgress { get { return Mathf.Clamp01((Time.time - deathStart) / Mathf.Max(0.01f, deathMotionTime)); } }
    public float deathMotionTime = 1.6f; // 8장
    public float deathHoldTime = 1f;     // 마지막 장(웅덩이)을 이만큼 더 보여 준 뒤 사라진다
    bool dying;
    float deathStart;

    [Header("처치 연출: 잠깐 멈췄다가 느린 화면에서 서서히 원래 속도로 (실제 시간 기준)")]
    public float hitStopTime = 0.12f;
    public float slowStartScale = 0.2f;
    public float slowRecoverTime = 1f;
    bool slowing;
    float baseFixedDelta;

    // 보스가 만들어 내는 오브젝트 이름들. 컴포넌트가 없는 순수 연출물이라 이름으로 찾는다
    static readonly string[] LeftoverNames = {
        "BossObstacle_1", "BossObstacle_2", "BossObstacle_3", "BossObstacle_4",
        "BossObstacleWarning",
        "BossCloneWarning",
        "BossMole", "BossMoleWarning",
        "Meteor", "MeteorWarning", "MeteorImpact",
        "BossLaser", "BossLaserWarning",
        "BossSlam", "BossSlamWarning"
    };

    Health health;
    bool reported;

    void Awake()
    {
        health = GetComponent<Health>();
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (slowing) EndSlow(true); // 연출 도중에 지워져도 느린 화면으로 남지 않게
    }

    System.Collections.IEnumerator HitStop()
    {
        slowing = true;
        baseFixedDelta = Time.fixedDeltaTime;
        float set = 0f;
        Time.timeScale = set;
        yield return new WaitForSecondsRealtime(hitStopTime);
        for (float t = 0f; t < slowRecoverTime; t += Time.unscaledDeltaTime)
        {
            // 그 사이 일시정지·게임오버·확인창이 시간을 잡았으면 손을 뗀다 (그쪽이 다시 1로 돌린다)
            if (Time.timeScale != set) { EndSlow(false); yield break; }
            float n = t / slowRecoverTime;
            set = Mathf.Lerp(slowStartScale, 1f, n * n); // 처음엔 천천히, 끝에서 빠르게 풀린다
            Time.timeScale = set;
            Time.fixedDeltaTime = baseFixedDelta * set; // 느린 화면에서 물리가 뚝뚝 끊기지 않게
            yield return null;
        }
        EndSlow(Time.timeScale == set);
    }

    void EndSlow(bool restoreScale)
    {
        slowing = false;
        Time.fixedDeltaTime = baseFixedDelta;
        if (restoreScale) Time.timeScale = 1f;
    }

    void OnDamaged()
    {
        if (reported) return;
        if (health == null || health.CurrentHealth > 0f) return;

        reported = true;
        AreaManager.NotifyBossDefeated();
        ClearLeftovers();
        StartDying();
    }

    // AI·패턴을 전부 끄고(돌던 코루틴까지), 맞지도 부딪히지도 않게 한 뒤 모션이 끝나면 지운다
    void StartDying()
    {
        dying = true;
        deathStart = Time.time;
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb == this || mb is Health) continue;
            mb.StopAllCoroutines();
            mb.enabled = false;
        }
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        Destroy(gameObject, deathMotionTime + deathHoldTime);
        if (Time.timeScale > 0f) StartCoroutine(HitStop()); // 이미 멈춰 있으면(동시에 게임오버 등) 건드리지 않는다
    }

    // 본체와 별개로 살아 있는 것들을 치운다
    void ClearLeftovers()
    {
        // 컴포넌트로 찾을 수 있는 것부터
        BossClone[] clones = Object.FindObjectsByType<BossClone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < clones.Length; i++)
            if (clones[i] != null) Destroy(clones[i].gameObject);

        BossMoleStrike[] moles = Object.FindObjectsByType<BossMoleStrike>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < moles.Length; i++)
            if (moles[i] != null) Destroy(moles[i].gameObject);

        BossShockwave[] waves = Object.FindObjectsByType<BossShockwave>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < waves.Length; i++)
            if (waves[i] != null) Destroy(waves[i].gameObject);

        // 남은 몹도 전부 (보스 자신은 사망 모션 뒤에 StartDying이 지운다)
        EnemyAI[] mobs = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < mobs.Length; i++)
            if (mobs[i] != null && mobs[i].gameObject != gameObject) Destroy(mobs[i].gameObject);

        // 나머지는 이름으로. 씬을 한 번만 훑고 끝낸다
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string n = all[i].name;
            for (int j = 0; j < LeftoverNames.Length; j++)
            {
                if (n != LeftoverNames[j]) continue;
                Destroy(all[i].gameObject);
                break;
            }
        }
    }
}
