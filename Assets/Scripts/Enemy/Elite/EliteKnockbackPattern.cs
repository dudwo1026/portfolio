using UnityEngine;

// Knockback pattern: same range mechanism as the grab (trigger range starts a short cast,
// apply range is re-checked when the cast finishes). On a hit the player is stunned and shoved away.
[RequireComponent(typeof(EnemyAI))]
public class EliteKnockbackPattern : MonoBehaviour, IElitePatternVisual
{
    public float cooldown = 15f;
    public float triggerRange = 2.5f; // 발동: how close the player must be for the cast to start
    public float applyRange = 4.5f;   // 적용: how far the hit can still land when the cast finishes
    // 선딜. 시전 시작부터 타격 판정까지. 스프라이트로는 바람이 터지는 장(Charge3)이 끝나는 시점이다
    public float castTime = 0.25f;
    // 후딜. 타격 뒤에도 이만큼 제자리에 묶여 마무리 모션을 재생한다.
    // 예전에는 타격과 동시에 패턴이 끝나서, 바람이 터지는 그림이 지나간 한참 뒤에 밀려났다.
    // 둘을 나눠야 그림과 판정이 같은 순간에 맞는다. 덤으로 밀어낸 직후 반격 틈이 생긴다
    public float recoverTime = 0f;

    public float damagePercent = 10f;       // 넉백공격 명중 시, 플레이어 최대 체력 대비 %
    public float impactDamagePercent = 10f; // 벽 충돌 시 추가 대미지의 최대치 (밀려나지 못한 비율만큼만 적용)
    public float stunDuration = 2f;
    public float knockbackDistance = 15f;
    public float maxStunBonusRatio = 1f; // up to +100% stun when the shove is fully blocked
    public Material castMaterial;        // shared "casting" tint

    EnemyAI ai;
    PlayerStun stun;
    Renderer rend;
    Material normalMaterial;

    bool casting;
    float castTimer;
    float recoverTimer;
    float nextCastTime;

    float TotalTime { get { return castTime + Mathf.Max(0f, recoverTime); } }

    // 시전 자세를 그리는 데 쓴다 (EnemySpriteAnimator).
    // 돌진과 달리 길이가 고정이라, 진행도를 넘겨 그림을 그 안에 고르게 펼친다.
    // 선딜+후딜 전체를 기준으로 재므로, 둘 중 어느 값을 바꿔도 애니메이션이 저절로 따라온다
    public bool PatternActive { get { return casting; } }
    public float PatternProgress
    {
        get
        {
            float total = TotalTime;
            if (total <= 0f) return 1f;
            return Mathf.Clamp01(1f - (castTimer + recoverTimer) / total);
        }
    }

void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
    }

    void OnDisable()
    {
        if (casting) EndCast();
    }

    void FixedUpdate()
    {
        if (casting)
        {
            UpdateCast();
            return;
        }

        if (ai.IsStunned) return;
        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextCastTime) return;

        Transform p = ai.PlayerTransform;
        if (p == null || !InRange(p, triggerRange)) return;

        BeginCast(p);
    }

    bool InRange(Transform p, float range)
    {
        Vector3 toPlayer = p.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude <= range;
    }

void BeginCast(Transform p)
    {
        casting = true;
        castTimer = castTime;
        recoverTimer = Mathf.Max(0f, recoverTime);
        nextCastTime = Time.time + cooldown; // counts from the cast start, hit or miss

        ai.patternDriving = true; // rooted in place for the whole cast
        FaceTarget(p);
        SetCastVisual(true);
    }

    void UpdateCast()
    {
        if (ai.IsStunned)
        {
            EndCast();
            return;
        }

        if (castTimer > 0f)
        {
            castTimer -= Time.fixedDeltaTime;
            if (castTimer > 0f) return;
            castTimer = 0f;
            Strike();
            if (recoverTimer > 0f) return; // 후딜이 있으면 묶인 채로 마무리 모션을 더 보여 준다
            EndCast();
            return;
        }

        // 후딜. 판정은 이미 끝났고 자세만 이어진다
        recoverTimer -= Time.fixedDeltaTime;
        if (recoverTimer <= 0f)
        {
            recoverTimer = 0f;
            EndCast();
        }
    }

    void Strike()
    {
        Transform p = ai.PlayerTransform;
        if (p == null || !InRange(p, applyRange)) return;

        Health h = ai.PlayerHealth;
        if (h != null) h.TakeDamage(Mathf.Round(ai.PercentOfPlayerMaxHealth(damagePercent))); // 1. 넉백공격 자체

        if (stun == null) stun = p.GetComponent<PlayerStun>();
        // 2. 벽에 부딪히면 밀려나지 못한 비율만큼 추가로. 끝까지 밀려나면 0
        if (stun != null) stun.Apply(transform.position, stunDuration, knockbackDistance, maxStunBonusRatio,
                                     ai.PercentOfPlayerMaxHealth(impactDamagePercent));
    }

void EndCast()
    {
        casting = false;
        castTimer = 0f;
        recoverTimer = 0f;
        ai.patternDriving = false;
        SetCastVisual(false);
    }

    void FaceTarget(Transform p)
    {
        Vector3 dir = p.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f); // 발동
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f); // 적용
        Gizmos.DrawWireSphere(transform.position, applyRange);
    }


void SetCastVisual(bool on)
    {
        if (rend == null) return;
        if (on && castMaterial != null) rend.sharedMaterial = castMaterial;
        else if (!on && normalMaterial != null) rend.sharedMaterial = normalMaterial;
    }
}
