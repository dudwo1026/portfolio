using UnityEngine;

// Lobs an AoE shell at the player on a fixed interval. The arc is solved for a fixed flight time
// under normal gravity, so launch speed/angle follow from the distance instead of being pinned.
[RequireComponent(typeof(EnemyAI))]
public class EliteRangedAttack : MonoBehaviour, IElitePatternVisual
{
    public float attackInterval = 4f;
    public float range = 20f;
    public float flightTimeAtMaxRange = 2f; // launch -> impact at full range; scales down with distance
    public float minFlightTime = 0.15f;
    public float gravity = 9.81f;
    public float projectileScale = 1f;   // roughly the player's width
    public float damagePercent = 10f;    // 플레이어 최대 체력 대비 %
    public float aoeRadius = 2.5f;       // diameter 5 blast at the impact point

    [Header("Special shot")]
    public int specialEveryNShots = 4;   // every 4th attack
    public float specialAoeRadius = 3.5f;
    public float blindDuration = 10f;
    public Material specialMaterial;     // dark grey, so the blind shell reads differently
    public float launchHeight = 1f;

    [Header("투사체 그림 (비워 두면 구체)")]
    public Texture2D normalBallTexture;  // 일반탄
    public Texture2D blindBallTexture;   // 암흑탄
    public Material ballMaterial;        // 반투명 (빛번짐 가장자리를 살린다)
    public float ballVisualScale = 1.3f; // 공 지름 = 판정 구체 지름 x 이 값

    [Header("Aim prediction")]
    public float predictionStrength = 1f; // 0 = aim where the player is, 1 = full lead

    EnemyAI ai;
    Renderer rend;
    TopDownMove playerMover;
    float nextShotTime;
    int shotCount;

    // 사격 모션 전체 길이. 모션이 시작되고 fireAtProgress 지점에서 실제로 발사한다 (EnemySpriteAnimator)
    public float shotMotionTime = 0.6f;
    // 4장 중 3번째 장(attack3)이 시작되는 지점 = 2/4. 장수를 바꾸면 이것도 맞춘다
    public float fireAtProgress = 0.5f;
    float lastShotTime = -999f;
    float fireAt = -1f; // 발사 예약 시각. -1이면 예약 없음
    public bool PatternActive { get { return Time.time - lastShotTime < shotMotionTime; } }
    public float PatternProgress { get { return Mathf.Clamp01((Time.time - lastShotTime) / shotMotionTime); } }

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
    }

    void OnEnable() { ai.onMeleeHit += OnMeleeHit; }
    void OnDisable() { ai.onMeleeHit -= OnMeleeHit; }

    // 사격 모션 중(발사 전)에 근접공격을 맞으면 끊긴다 (발사 간격은 이미 소모됨)
    void OnMeleeHit()
    {
        if (fireAt < 0f) return;
        fireAt = -1f;
        lastShotTime = -999f; // 모션도 같이 끊는다
    }

void FixedUpdate()
    {
        if (ai.IsStunned || ai.patternDriving) { fireAt = -1f; return; } // 선딜 중 기절하면 취소
        Transform p = ai.PlayerTransform;
        if (p == null) return;

        // 예약된 발사. 조준은 이 순간에 다시 잡는다 — 선딜 동안 플레이어가 움직였으므로
        if (fireAt >= 0f)
        {
            if (Time.time < fireAt) return;
            fireAt = -1f;
            Fire(PredictAimPoint(p));
            return;
        }

        if (ai.State != EnemyState.Attack) return;
        if (Time.time < nextShotTime) return;

        Vector3 flat = p.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist > range || dist < 0.01f) return;

        // 모션을 먼저 시작하고, attack3이 나오는 순간에 쏜다
        nextShotTime = Time.time + attackInterval;
        lastShotTime = Time.time;
        fireAt = Time.time + shotMotionTime * fireAtProgress;
    }

void Fire(Vector3 aimPoint)
    {
        Vector3 flat = aimPoint - transform.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance < 0.01f) return;
        Vector3 dir = flat / distance;

        shotCount++;
        bool special = specialEveryNShots > 0 && shotCount % specialEveryNShots == 0;
        float radius = special ? specialAoeRadius : aoeRadius;

        Vector3 start = transform.position + Vector3.up * launchHeight + dir * 0.8f;

        // flight time is proportional to distance (2s at max range), which keeps the horizontal speed constant
        float time = FlightTimeFor(distance);
        float horizontal = distance / time;
        float vertical = (aimPoint.y - start.y) / time + 0.5f * gravity * time;
        Vector3 velocity = dir * horizontal + Vector3.up * vertical;

        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = name + (special ? "_SpecialProjectile" : "_Projectile");
        proj.transform.position = start;
        proj.transform.localScale = Vector3.one * projectileScale * (special ? 1.4f : 1f);
        if (special && specialMaterial != null) proj.GetComponent<Renderer>().sharedMaterial = specialMaterial;
        else if (rend != null) proj.GetComponent<Renderer>().sharedMaterial = rend.sharedMaterial; // matches the shooter

        Collider col = proj.GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody prb = proj.AddComponent<Rigidbody>();
        prb.useGravity = false;
        prb.isKinematic = true; // the shell integrates its own arc

        // 그림이 있으면 구체는 판정용으로만 남기고 그림을 띄운다
        Texture2D ballTex = special ? blindBallTexture : normalBallTexture;
        if (ballTex != null && ballMaterial != null)
        {
            proj.GetComponent<Renderer>().enabled = false;
            ProjectileSprite.Attach(proj, ballTex, ballMaterial, ballVisualScale);
        }

        EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
        ep.Launch(velocity, gravity, ai.PercentOfPlayerMaxHealth(damagePercent), radius, time, gameObject);
        if (special) ep.SetBlind(blindDuration);

        transform.rotation = Quaternion.LookRotation(dir);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
    }


float FlightTimeFor(float distance)
    {
        float time = range > 0.01f ? flightTimeAtMaxRange * (distance / range) : flightTimeAtMaxRange;
        return Mathf.Max(time, minFlightTime);
    }


Vector3 PredictAimPoint(Transform p)
    {
        if (playerMover == null) playerMover = p.GetComponent<TopDownMove>();

        Vector3 v = Vector3.zero;
        if (playerMover != null) v = playerMover.MoveDirection * playerMover.CurrentSpeed * predictionStrength;
        v.y = 0f;

        // 포탄은 수평 속도 s가 일정하다(FlightTimeFor). |P + v*t| = s*t 를 t에 대해 정확히 푼다.
        // 예전엔 반복 3회 근사 + 사거리 18 제한이었는데, 멀어지는 표적에선 수렴 비율이 v/s(=0.67)라
        // 덜 수렴한 데다 조준점이 18에서 잘려 한참 못 미쳤다 (거리 13.5에서 도망치면 40.5를 노려야 함)
        float s = range / flightTimeAtMaxRange;
        Vector3 P = p.position - transform.position;
        P.y = 0f;
        float a = Vector3.Dot(v, v) - s * s;
        float b = 2f * Vector3.Dot(P, v);
        float c = Vector3.Dot(P, P);
        float t = -1f;
        if (Mathf.Abs(a) < 0.0001f) { if (b < 0f) t = -c / b; }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc >= 0f)
            {
                float sq = Mathf.Sqrt(disc);
                float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
                t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            }
        }
        return t > 0f ? p.position + v * t : p.position; // 따라잡을 수 없으면 지금 위치를 노린다
    }
}

// 투사체 그림. 카메라를 정면으로 보되, 화면상 진행 방향으로 돌려 꼬리가 뒤에 오게 한다.
// 그림은 공이 정중앙, 꼬리가 왼쪽(= 그림의 +x가 진행 방향)이 되도록 잘라 둔 것을 쓴다
public class ProjectileSprite : MonoBehaviour
{
    EnemyProjectile shell;
    float angle;

    public static void Attach(GameObject proj, Texture2D tex, Material mat, float visualScale)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "BallSprite";
        Object.Destroy(q.GetComponent<Collider>()); // 판정은 구체가 한다
        q.transform.SetParent(proj.transform, false);

        // 공 지름 = 판정 구체 지름 x visualScale. 부모 스케일이 균일하므로 로컬은 그 배율만 곱하면 된다
        float h = visualScale;
        q.transform.localScale = new Vector3(h * tex.width / tex.height, h, 1f);

        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var mpb = new MaterialPropertyBlock();
        mpb.SetTexture("_BaseMap", tex);
        r.SetPropertyBlock(mpb);

        q.AddComponent<ProjectileSprite>().shell = proj.GetComponent<EnemyProjectile>();
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (shell == null) shell = GetComponentInParent<EnemyProjectile>(); // Attach 직후엔 아직 안 붙어 있다
        if (shell != null)
        {
            Vector3 v = shell.Velocity;
            float x = Vector3.Dot(v, cam.transform.right), y = Vector3.Dot(v, cam.transform.up);
            if (x * x + y * y > 0.0001f) angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg; // 거의 카메라 축 방향이면 직전 각도 유지
        }
        transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, angle);
    }
}
