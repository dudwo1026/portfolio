using UnityEngine;

// Grab pattern (홀드): stands still for a short cast, then grabs. No damage.
// On a hit the player is 구속(bound) and this enemy is frozen until the player melees free.
// The body turns blue from cast start until the hold ends. Attach only to elites that should have it.
[RequireComponent(typeof(EnemyAI))]
public class EliteGrabPattern : MonoBehaviour, IElitePatternVisual
{
    public float cooldown = 15f;
    public float triggerRange = 2.5f;    // 발동: how close the player must be for the cast to start
    public float applyRange = 4.5f;      // 적용: how far the grab can still connect when the cast finishes
    public float holdDistance = 1.5f;    // where the grabbed player is dragged to, measured from the grabber
    public float pullDuration = 0.15f;
    public float castTime = 0.25f;       // rooted in place before the grab lands
    public Material castMaterial;        // blue "casting / holding" look

    EnemyAI ai;
    Renderer rend;
    Material normalMaterial;
    PlayerBound bound;

    bool casting;
    bool holding;
    float castTimer;
    float nextGrabTime;

    // 스프라이트가 1-2-3-2-1(뻗었다 당기기)을 재생하는 데 쓴다.
    // 시전(뻗기) + 끌어당김(수축)을 하나의 진행도로 이어 붙이고,
    // 끌어당김이 끝난 뒤의 구속 유지 구간은 1.0에 고정해 마지막 장(=움켜쥔 자세)을 유지한다
    public bool PatternActive { get { return casting || holding; } }
    public float PatternProgress
    {
        get
        {
            float total = castTime + pullDuration;
            if (total <= 0f) return 1f;
            float elapsed = casting
                ? castTime - castTimer
                : castTime + (pullDuration - Mathf.Max(0f, pullTimer));
            return Mathf.Clamp01(elapsed / total);
        }
    }

    Rigidbody playerRb;
    Vector3 pullStart;
    Vector3 pullTarget;
    float pullTimer;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
    }

void OnDisable()
    {
        ai.onMeleeHit -= OnMeleeHit;

        // 잡고 있던 중에 이 개체가 죽거나 꺼지면 구속도 같이 풀어줘야 한다.
        // 안 그러면 잡은 놈이 사라졌는데 플레이어만 묶인 채로 남는다
        if (holding && bound != null && bound.IsActive)
        {
            bound.Released -= OnPlayerReleased; // 아래 Release가 콜백을 되부르지 않도록 먼저 해제
            bound.Release();
        }

        if (casting || holding) EndPattern();
    }

    void FixedUpdate()
    {
        if (holding)
        {
            UpdatePull();
            return;
        }

        if (casting)
        {
            UpdateCast();
            return;
        }

        if (ai.IsStunned) return;
        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextGrabTime) return;

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
        nextGrabTime = Time.time + cooldown; // counts from the cast start, hit or miss

        ai.patternDriving = true; // rooted in place for the whole cast
        FaceTarget(p);
        SetCastVisual(true);
    }

    void UpdateCast()
    {
        if (ai.IsStunned)
        {
            casting = false;
            EndPattern();
            return;
        }

        castTimer -= Time.fixedDeltaTime;
        if (castTimer > 0f) return;

        casting = false;

        Transform p = ai.PlayerTransform;
        if (p == null || !InRange(p, applyRange)) // player left the apply range during the cast: whiffed
        {
            EndPattern();
            return;
        }

        Grab(p);
    }

void Grab(Transform p)
    {
        if (bound == null) bound = p.GetComponent<PlayerBound>();
        if (bound == null || bound.IsActive)
        {
            EndPattern();
            return;
        }

        bound.Bind();  // no damage: the grab only applies 구속
        bound.Released += OnPlayerReleased;

        holding = true;
        ai.patternDriving = true; // the grabber stays locked down for as long as the hold lasts
        FaceTarget(p);

        // drag the player in to the grabber
        Vector3 dir = p.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        playerRb = p.GetComponent<Rigidbody>();
        pullStart = p.position;
        pullTarget = transform.position + dir * holdDistance;
        pullTarget.y = p.position.y; // keep the player at their own ground height
        pullTimer = pullDuration;
    }

    void OnPlayerReleased()
    {
        if (bound != null) bound.Released -= OnPlayerReleased;
        holding = false;
        EndPattern();
    }

    void EndPattern()
    {
        casting = false;
        holding = false;
        ai.patternDriving = false;
        SetCastVisual(false);
    }

    void SetCastVisual(bool on)
    {
        if (rend == null) return;
        if (on && castMaterial != null) rend.sharedMaterial = castMaterial;
        else if (!on && normalMaterial != null) rend.sharedMaterial = normalMaterial;
    }

    void FaceTarget(Transform p)
    {
        Vector3 dir = p.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
    }

void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.8f); // 발동
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = new Color(0f, 0.9f, 1f, 0.35f); // 적용
        Gizmos.DrawWireSphere(transform.position, applyRange);
    }


void UpdatePull()
    {
        if (pullTimer <= 0f) return;

        pullTimer -= Time.fixedDeltaTime;
        float t = 1f - Mathf.Clamp01(pullTimer / pullDuration);
        Vector3 pos = Vector3.Lerp(pullStart, pullTarget, t);

        if (playerRb != null) playerRb.MovePosition(pos);
    }


void OnMeleeHit()
    {
        // meleed before the grab lands -> the cast is cancelled (the cooldown stays spent)
        if (casting) EndPattern();
    }


void OnEnable()
    {
        ai.onMeleeHit += OnMeleeHit;
    }
}
