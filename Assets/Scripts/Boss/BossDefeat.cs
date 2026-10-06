using UnityEngine;

// 보스가 쓰러지면 더 이상 몹이 나오지 않고(지상·옥상 스포너 전부 정지),
// 보스가 깔아 둔 장애물·분신·경고 표시 같은 잔해도 전부 치운다.
[RequireComponent(typeof(Health))]
public class BossDefeat : MonoBehaviour
{
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
    }

    void OnDamaged()
    {
        if (reported) return;
        if (health == null || health.CurrentHealth > 0f) return;

        reported = true;
        AreaManager.NotifyBossDefeated();
        ClearLeftovers();
    }

    // 보스 본체는 EnemyAI가 지운다. 여기서는 본체와 별개로 살아 있는 것들을 치운다
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
