using UnityEngine;

// 노말 난이도 수치를 한곳에 모은다.
// 하드는 씬·프리팹에 저장된 값 그대로이고, 노말일 때만 각 오브젝트가 시작하면서 아래 값으로 덮어쓴다
// (몹·정예·보스: EnemyAI.Awake / 처치 게이지: KillGauge.Awake / 정예 재등장: EliteSpawner.Start)
public static class NormalDifficulty
{
    public static bool Active { get { return GameSettings.difficulty == Difficulty.Normal; } }

    public const float EnemyHealthScale = 0.8f;   // 일반·정예 체력 (일반 100 → 80)
    public const float BossHealth = 5000f;        // 보스는 비율 대신 고정 (하드 7000)
    // 정예·보스가 플레이어에게 주는 피해 (일반 몹은 그대로). 몹끼리 주는 피해는 건드리지 않는다
    public const float EliteBossDamageScale = 0.7f;

    public const float EliteHuntAt = 200f;        // 추격 모드 시작 (하드 170). 게이지가 가득 찰 때
    public const float EliteRespawnDelay = 45f;   // 정예 재등장 (하드 30초)
    public const float ElitePatternCooldownScale = 1.25f; // 정예 특수패턴 쿨타임 (A 20→25, B·C 15→19, D 4→5, E 13→16)

    // 정예 상태이상 시간 (하드 → 노말)
    public const float StunDuration = 1.5f;       // C 기절 2초
    public const float BlindDuration = 6f;        // D 실명 10초
    public const float WeakenDuration = 2f;       // E 약화 3초
    public const float ConfusionDuration = 5f;    // F 혼란 8초
    public const float TauntDuration = 20f;       // F 도발 30초

    public static void ApplyToEnemy(EnemyAI ai)
    {
        if (!Active || ai == null) return;

        bool boss = ai.GetComponent<BossDefeat>() != null;
        Health h = ai.GetComponent<Health>();
        if (h != null) h.SetMaxHealth(boss ? BossHealth : h.maxHealth * EnemyHealthScale);

        const float dmg = EliteBossDamageScale;
        if (ai.isElite) ai.attackDamage *= dmg; // 정예 근접 (보스는 0이고 근접은 BossMeleeAttack이 따로 친다)

        EliteChargePattern charge = ai.GetComponent<EliteChargePattern>();
        if (charge != null) { charge.cooldown *= ElitePatternCooldownScale; charge.damagePercent *= dmg; } // mobDamagePercent(몹이 맞는 피해)는 그대로

        EliteGrabPattern grab = ai.GetComponent<EliteGrabPattern>();
        if (grab != null) grab.cooldown *= ElitePatternCooldownScale;

        EliteKnockbackPattern knock = ai.GetComponent<EliteKnockbackPattern>();
        if (knock != null) { knock.cooldown *= ElitePatternCooldownScale; knock.stunDuration = StunDuration; knock.damagePercent *= dmg; knock.impactDamagePercent *= dmg; }

        EliteRangedAttack ranged = ai.GetComponent<EliteRangedAttack>();
        if (ranged != null) { ranged.attackInterval *= ElitePatternCooldownScale; ranged.blindDuration = BlindDuration; ranged.damagePercent *= dmg; }

        EliteEruptionPattern erupt = ai.GetComponent<EliteEruptionPattern>();
        if (erupt != null) { erupt.cooldown *= ElitePatternCooldownScale; erupt.weakenDuration = WeakenDuration; erupt.damagePercent *= dmg; }

        EliteSelfDestructPattern bomb = ai.GetComponent<EliteSelfDestructPattern>();
        if (bomb != null) { bomb.confusionDuration = ConfusionDuration; bomb.tauntDuration = TauntDuration; bomb.damagePercent *= dmg; } // deathBlastDamage는 몹만 맞는다

        if (!boss) return;
        BossMeleeAttack melee = ai.GetComponent<BossMeleeAttack>();
        if (melee != null) melee.damage *= dmg;
        BossClonePattern clone = ai.GetComponent<BossClonePattern>();
        if (clone != null) { clone.cloneDamage *= dmg; clone.moleDamage *= dmg; }
        BossMeteorPattern meteor = ai.GetComponent<BossMeteorPattern>();
        if (meteor != null) meteor.damage *= dmg;
        BossShockwavePattern wave = ai.GetComponent<BossShockwavePattern>();
        if (wave != null) wave.waveDamage *= dmg;
        BossLaserPattern laser = ai.GetComponent<BossLaserPattern>();
        if (laser != null) laser.damagePerSecond *= dmg;
        BossGuardPattern guard = ai.GetComponent<BossGuardPattern>();
        if (guard != null) guard.totemDamagePercent *= dmg;
    }
}
