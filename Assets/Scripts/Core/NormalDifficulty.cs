using UnityEngine;

// 노말 난이도 수치를 한곳에 모은다.
// 하드는 씬·프리팹에 저장된 값 그대로이고, 노말일 때만 각 오브젝트가 시작하면서 아래 값으로 덮어쓴다
// (몹·정예·보스: EnemyAI.Awake / 처치 게이지: KillGauge.Awake / 정예 재등장: EliteSpawner.Start)
public static class NormalDifficulty
{
    public static bool Active { get { return GameSettings.difficulty == Difficulty.Normal; } }

    public const float EnemyHealthScale = 0.8f;   // 일반·정예 체력 (일반 100 → 80)
    public const float BossHealth = 5000f;        // 보스는 비율 대신 고정 (하드 7000)

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

        Health h = ai.GetComponent<Health>();
        if (h != null) h.SetMaxHealth(ai.GetComponent<BossDefeat>() != null ? BossHealth : h.maxHealth * EnemyHealthScale);

        EliteChargePattern charge = ai.GetComponent<EliteChargePattern>();
        if (charge != null) charge.cooldown *= ElitePatternCooldownScale;

        EliteGrabPattern grab = ai.GetComponent<EliteGrabPattern>();
        if (grab != null) grab.cooldown *= ElitePatternCooldownScale;

        EliteKnockbackPattern knock = ai.GetComponent<EliteKnockbackPattern>();
        if (knock != null) { knock.cooldown *= ElitePatternCooldownScale; knock.stunDuration = StunDuration; }

        EliteRangedAttack ranged = ai.GetComponent<EliteRangedAttack>();
        if (ranged != null) { ranged.attackInterval *= ElitePatternCooldownScale; ranged.blindDuration = BlindDuration; }

        EliteEruptionPattern erupt = ai.GetComponent<EliteEruptionPattern>();
        if (erupt != null) { erupt.cooldown *= ElitePatternCooldownScale; erupt.weakenDuration = WeakenDuration; }

        EliteSelfDestructPattern bomb = ai.GetComponent<EliteSelfDestructPattern>();
        if (bomb != null) { bomb.confusionDuration = ConfusionDuration; bomb.tauntDuration = TauntDuration; }
    }
}
