using UnityEngine;

// 노말 난이도 수치를 한곳에 모은다.
// 하드는 씬·프리팹에 저장된 값 그대로이고, 노말일 때만 각 오브젝트가 시작하면서 아래 값으로 덮어쓴다
// (몹·정예·보스: EnemyAI.Awake / 정예 재등장: EliteSpawner.Start)
public static class NormalDifficulty
{
    public static bool Active { get { return GameSettings.difficulty == Difficulty.Normal; } }

    public const float EnemyHealthScale = 0.8f;   // 일반·정예 체력 (일반 100 → 80)
    public const float BossHealth = 5000f;        // 보스는 비율 대신 고정 (하드 7000)
    // 정예·보스가 플레이어에게 주는 피해 (일반 몹은 그대로). 몹끼리 주는 피해는 건드리지 않는다
    public const float EliteBossDamageScale = 0.7f;

    public const float EliteRespawnDelay = 45f;   // 정예 재등장 (하드 30초)
    public const float ElitePatternCooldownScale = 1.25f; // 정예 특수패턴 쿨타임 (A 20→25, B·C 15→19, D 4→5, E 13→16)

    // 보스 패턴
    public const float BossPatternCooldownAdd = 4f;   // 분신·운석·충격파·레이저 쿨타임 18~23 → 22~27초
    public const float BossPatternCooldownFloor = 8f; // 페이즈가 올라도 이 밑으로는 안 줄어든다 (하드 5초)
    public const float BossMeleeCooldownAdd = 2f;     // 근접 6 → 8초
    public const float BossLaserTrackScale = 0.8f;    // 레이저가 플레이어 이동속도의 80%로만 따라온다
    public const int BossMeteorBaseCount = 3;         // 운석 4개 → 3개부터 (페이즈마다 +1은 그대로)
    public const float BossShockwaveMinInterval = 0.4f; // 충격파 점프 최소 간격 0.25 → 0.4초
    public const float BossGuardRecast = 40f;         // 방어 토템 재시전 30 → 40초
    public const float BossGuardHealMultiplier = 0f;  // 방어 중 맞으면 하드는 맞은 양의 2배를 회복. 노말은 무적만

    // 정예 상태이상 시간 (하드 → 노말)
    public const float StunDuration = 1.5f;       // C 기절 2초
    // D 실명은 노말에서 아예 없다 (실명탄을 쏘지 않는다)
    public const float WeakenDuration = 2f;       // E 약화 3초
    public const float ConfusionDuration = 5f;    // F 혼란 8초
    public const float TauntDuration = 20f;       // F 도발 30초

    // 정예가 플레이어에게 주는 피해를 k배로 (노말 0.7배, 구획 미니보스 강화에도 쓴다)
    public static void ScaleEliteDamage(EnemyAI ai, float k)
    {
        if (ai.isElite) ai.attackDamage *= k; // 정예 근접 (보스는 0이고 근접은 BossMeleeAttack이 따로 친다)
        EliteChargePattern charge = ai.GetComponent<EliteChargePattern>();
        if (charge != null) charge.damagePercent *= k; // mobDamagePercent(몹이 맞는 피해)는 그대로
        EliteKnockbackPattern knock = ai.GetComponent<EliteKnockbackPattern>();
        if (knock != null) { knock.damagePercent *= k; knock.impactDamagePercent *= k; }
        EliteRangedAttack ranged = ai.GetComponent<EliteRangedAttack>();
        if (ranged != null) ranged.damagePercent *= k;
        EliteEruptionPattern erupt = ai.GetComponent<EliteEruptionPattern>();
        if (erupt != null) erupt.damagePercent *= k;
        EliteSelfDestructPattern bomb = ai.GetComponent<EliteSelfDestructPattern>();
        if (bomb != null) bomb.damagePercent *= k; // deathBlastDamage는 몹만 맞는다
    }

    public static void ApplyToEnemy(EnemyAI ai)
    {
        if (!Active || ai == null) return;

        bool boss = ai.GetComponent<BossDefeat>() != null;
        Health h = ai.GetComponent<Health>();
        if (h != null) h.SetMaxHealth(boss ? BossHealth : h.maxHealth * EnemyHealthScale);

        const float dmg = EliteBossDamageScale;
        ScaleEliteDamage(ai, dmg);

        EliteChargePattern charge = ai.GetComponent<EliteChargePattern>();
        if (charge != null) charge.cooldown *= ElitePatternCooldownScale;

        EliteGrabPattern grab = ai.GetComponent<EliteGrabPattern>();
        if (grab != null) grab.cooldown *= ElitePatternCooldownScale;

        EliteKnockbackPattern knock = ai.GetComponent<EliteKnockbackPattern>();
        if (knock != null) { knock.cooldown *= ElitePatternCooldownScale; knock.stunDuration = StunDuration; }

        EliteRangedAttack ranged = ai.GetComponent<EliteRangedAttack>();
        if (ranged != null)
        {
            ranged.attackInterval *= ElitePatternCooldownScale;
            ranged.specialEveryNShots = 0; // 실명탄(하드: 4발마다)을 쏘지 않는다
        }

        EliteEruptionPattern erupt = ai.GetComponent<EliteEruptionPattern>();
        if (erupt != null) { erupt.cooldown *= ElitePatternCooldownScale; erupt.weakenDuration = WeakenDuration; }

        EliteSelfDestructPattern bomb = ai.GetComponent<EliteSelfDestructPattern>();
        if (bomb != null)
        {
            bomb.confusionDuration = ConfusionDuration; bomb.tauntDuration = TauntDuration;
            bomb.explodeWhenKilledLit = false;  // 점화돼도 터지기 전에 잡으면 자폭하지 않는다
        }

        if (!boss) return;
        const float cdAdd = BossPatternCooldownAdd, cdFloor = BossPatternCooldownFloor;

        BossMeleeAttack melee = ai.GetComponent<BossMeleeAttack>();
        if (melee != null) { melee.damage *= dmg; melee.cooldown += BossMeleeCooldownAdd; }

        BossClonePattern clone = ai.GetComponent<BossClonePattern>();
        if (clone != null)
        {
            clone.cloneDamage *= dmg; clone.moleDamage *= dmg;
            clone.cooldownMin += cdAdd; clone.cooldownMax += cdAdd; clone.cooldownFloor = cdFloor;
        }

        BossMeteorPattern meteor = ai.GetComponent<BossMeteorPattern>();
        if (meteor != null)
        {
            meteor.damage *= dmg; meteor.baseCount = BossMeteorBaseCount;
            meteor.cooldownMin += cdAdd; meteor.cooldownMax += cdAdd; meteor.cooldownFloor = cdFloor;
        }

        BossShockwavePattern wave = ai.GetComponent<BossShockwavePattern>();
        if (wave != null)
        {
            wave.waveDamage *= dmg; wave.minInterval = BossShockwaveMinInterval;
            wave.cooldownMin += cdAdd; wave.cooldownMax += cdAdd; wave.cooldownFloor = cdFloor;
        }

        BossLaserPattern laser = ai.GetComponent<BossLaserPattern>();
        if (laser != null)
        {
            laser.damagePerSecond *= dmg; laser.trackSpeedScale = BossLaserTrackScale;
            laser.cooldownMin += cdAdd; laser.cooldownMax += cdAdd; laser.cooldownFloor = cdFloor;
        }

        BossGuardPattern guard = ai.GetComponent<BossGuardPattern>();
        if (guard != null)
        {
            guard.totemDamagePercent *= dmg;
            guard.recastCooldown = BossGuardRecast;
            guard.healMultiplier = BossGuardHealMultiplier; // 피해는 계속 무효, 덤 회복만 없앤다
        }
    }
}
