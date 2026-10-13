using System.Collections;
using UnityEngine;

// 보스 특수패턴 6: 본체에서 굵은 레이저를 쏜다.
// 워닝 1.5초(플레이어 추적) → 시작지점 확정 → 0.7초 후 발사 → 2.5초간 플레이어를 따라 휘두른다.
[RequireComponent(typeof(EnemyAI))]
public class BossLaserPattern : MonoBehaviour, IBossMotion
{
    // 스프라이트 모션: 워닝+확정 동안 0→1(입을 벌리며 충전), 발사 내내 1~2(발사 장 고정),
    // 발사가 끝난 뒤 recoveryTime 동안 2→3(입을 다무는 장들)
    public string MotionName { get { return "Laser"; } }
    public bool MotionActive { get { return running || Time.time < recoverEnd; } }
    public float MotionProgress
    {
        get
        {
            if (running) return motionProgress;
            return 3f - Mathf.Clamp01((recoverEnd - Time.time) / Mathf.Max(0.01f, recoveryTime));
        }
    }
    public float recoveryTime = 0.45f;
    float motionProgress;
    float recoverEnd = -999f;

    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public float warningTime = 1.5f;   // 워닝이 플레이어를 따라다니는 시간
    public float lockDelay = 0.7f;     // 확정 후 발사까지
    public float fireDuration = 2.5f;  // 발사 유지 시간
    // 레이저 끝이 플레이어 거리에서 초당 이만큼 움직이도록 회전한다 (플레이어 이동속도와 맞춤).
    // 각속도로 고정하면 거리에 따라 체감이 달라지므로, 선속도 기준으로 매 프레임 환산한다
    public float trackLinearSpeed = 6f;
    public bool matchPlayerMoveSpeed = true; // 켜면 플레이어의 실제 이동속도를 그대로 따라간다
    public float trackSpeedScale = 1f;       // 위 속도에 곱한다. 1보다 작으면 계속 움직이는 플레이어를 놓친다 (노말 0.8)

    [Header("피해")]
    public float damagePerSecond = 100f;
    public float tickInterval = 0.2f;

    [Header("모양")]
    public float beamWidth = 3f;       // 굵기
    public float beamHeight = 2f;
    public float maxLength = 70f;
    public float groundOffset = 0.05f;
    public float probeHeight = 1f;     // 길이 측정 광선의 높이 (바닥 기준)
    public Color warningColor = new Color(1f, 0.5f, 0.1f);
    public Color lockedColor = new Color(1f, 0.1f, 0.05f);
    public Color beamColor = new Color(1f, 0.25f, 0.6f);

    [Header("이펙트 (비워 두면 큐브 막대)")]
    public GameObject beamEffectPrefab;
    // 발사 이만큼 전에 이펙트를 재생한다. 마법진이 1.32초에 걸쳐 다 펼쳐진 뒤 레이저가 나오도록,
    // Laser AOE Single은 레이저 첫 발이 재생 1.6초 뒤(= 발사 0.1초 전)에 나오게 맞춰 두었다.
    // 이 값을 바꾸면 그 프리팹의 최상위 버스트 시각(1.2)도 같이 옮겨야 한다
    public float effectLeadTime = 1.7f;
    // 마법진은 보스 주변을 레이저 방향으로 따라 돌고, 레이저는 그 중심에서 나간다.
    // 큐브 몸체는 숨기고 스프라이트로 그리므로, 레이저가 입 속 빛구슬(발사 장)에서 나가는 거리
    public float effectOrbitRadius = 0.8f;
    // 발사 시작점(입)을 보스 중심에서 월드 기준으로 이만큼 옮긴다 (+X = 동쪽).
    // 판정·길이 측정·워닝도 같은 지점에서 나가므로 보이는 빔과 맞는다
    public Vector3 muzzleOffset = new Vector3(2f, 0f, 0f);
    // 마법진은 시작점에서 레이저 방향으로 이만큼 앞에 둔다. 레이저가 마법진을 관통해 나간다
    public float effectCircleForward = 3.7f;
    // 원본 마법진 지름 약 12.8 → 6.7 (반지름 3.36)
    public float effectCircleScale = 0.525f;
    // 보스 중심은 바닥 위 3이라 반지름 3.36짜리 마법진은 바닥에 박힌다. 그만큼 이펙트를 띄운다 (판정은 평면이라 무관)
    public float effectLift = 0.5f;

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    float nextCastTime;
    bool running;
    GameObject fx;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        obstacles = GetComponent<BossObstaclePattern>();
        actionLock = GetComponent<BossActionLock>();
        if (actionLock == null) actionLock = gameObject.AddComponent<BossActionLock>();
    }

    void Start()
    {
        nextCastTime = Time.time + NextCooldown();
    }

    // 18~23초 사이 무작위, 페이즈가 오를수록 짧아진다
    float NextCooldown()
    {
        int phase = obstacles != null ? obstacles.TriggeredCount : 0;
        float roll = Random.Range(cooldownMin, cooldownMax);
        return Mathf.Max(cooldownFloor, roll - cooldownStepPerPhase * phase);
    }

    void Update()
    {
        if (running) return;
        if (ai.State != EnemyState.Attack) return;
        if (ai.PlayerTransform == null) return;
        if (Time.time < nextCastTime) return;
        if (actionLock.IsLocked) return;

        nextCastTime = Time.time + NextCooldown();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        motionProgress = 0f;
        float windup = Mathf.Max(0.01f, warningTime + lockDelay);
        actionLock.LockFor(warningTime + lockDelay + fireDuration); // 레이저 내내 다른 패턴 금지

        Transform p = ai.PlayerTransform;
        Vector3 dir = FlatDirTo(p != null ? p.position : Origin + transform.forward);

        // 1) 워닝: 플레이어를 따라다닌다
        GameObject warning = MakeBar("BossLaserWarning", warningColor, 0.1f);
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            motionProgress = Mathf.Clamp01(t / windup);
            if (ai.PlayerTransform != null) dir = FlatDirTo(ai.PlayerTransform.position);
            PlaceBar(warning, dir, beamWidth, 0.1f, true);
            TickEffect(warningTime - t + lockDelay, dir);
            yield return null;
        }

        // 2) 시작지점 확정
        Renderer wr = warning != null ? warning.GetComponent<Renderer>() : null;
        if (wr != null) wr.sharedMaterial.color = lockedColor;
        PlaceBar(warning, dir, beamWidth, 0.1f, true);
        float locked = 0f;
        while (locked < lockDelay)
        {
            locked += Time.deltaTime;
            motionProgress = Mathf.Clamp01((warningTime + locked) / windup);
            TickEffect(lockDelay - locked, dir);
            yield return null;
        }
        if (warning != null) Destroy(warning);

        // 3) 발사: fireDuration 동안 추적
        GameObject beam = fx != null ? null : MakeBar("BossLaser", beamColor, beamHeight);
        float fired = 0f, tick = 0f;
        while (fired < fireDuration)
        {
            float dt = Time.deltaTime;
            fired += dt;
            motionProgress = 1f + Mathf.Clamp01(fired / fireDuration) * 0.999f; // 발사 내내 발사 장

            if (ai.PlayerTransform != null)
            {
                Vector3 want = FlatDirTo(ai.PlayerTransform.position);
                dir = Vector3.RotateTowards(dir, want, TrackRadiansPerSecond() * dt, 0f); // 회전 속도 제한
            }
            float length = fx != null ? PlaceEffect(fx, dir) : PlaceBar(beam, dir, beamWidth, beamHeight, false);

            tick -= dt;
            if (tick <= 0f)
            {
                tick = tickInterval;
                DamageInBeam(dir, length);
            }
            yield return null;
        }

        if (beam != null) Destroy(beam);
        if (fx != null) Destroy(fx);
        fx = null;
        recoverEnd = Time.time + recoveryTime; // 이제 입을 다문다
        running = false;
    }

    // 발사까지 남은 시간이 effectLeadTime 이하가 되면 이펙트를 만들고, 그 뒤로는 매 프레임 자리를 맞춘다
    void TickEffect(float untilFire, Vector3 dir)
    {
        if (beamEffectPrefab == null) return;
        if (fx == null)
        {
            if (untilFire > effectLeadTime) return;
            fx = Instantiate(beamEffectPrefab);
            fx.name = "BossLaser"; // 보스가 죽으면 BossDefeat가 이름으로 치운다
            SetupEffect(fx);
        }
        PlaceEffect(fx, dir);
    }

    // 레이저 = 늘림 배율이 음수인 파티클, 마법진 = Local 정렬 파티클
    static bool IsBeam(ParticleSystemRenderer r)
    {
        return r.renderMode == ParticleSystemRenderMode.Stretch && r.lengthScale < 0f;
    }

    // 가장 굵은 레이저가 판정 굵기(beamWidth)가 되도록 레이저들을 같은 비율로 맞추고, 마법진을 줄인다
    void SetupEffect(GameObject fx)
    {
        ParticleSystemRenderer[] rs = fx.GetComponentsInChildren<ParticleSystemRenderer>();
        float widest = 0f;
        foreach (ParticleSystemRenderer r in rs)
            if (IsBeam(r)) widest = Mathf.Max(widest, r.GetComponent<ParticleSystem>().main.startSize.constantMax);

        foreach (ParticleSystemRenderer r in rs)
        {
            if (IsBeam(r) && widest > 0f)
            {
                ParticleSystem.MainModule m = r.GetComponent<ParticleSystem>().main;
                m.startSize = m.startSize.constantMax * beamWidth / widest;
            }
            else if (r.alignment == ParticleSystemRenderSpace.Local)
            {
                r.transform.localScale = Vector3.one * effectCircleScale;
                r.transform.localPosition = Vector3.up * effectCircleForward; // 이펙트의 위쪽 = 레이저 방향
            }
        }
    }

    // 이펙트는 위로 뻗는 레이저라 위쪽을 dir로 눕힌다. 마법진도 같이 세워져 레이저 방향을 바라보고,
    // 보스 주변 effectOrbitRadius 위치를 따라 돈다. 레이저 길이는 마법진부터 벽까지
    float PlaceEffect(GameObject fx, Vector3 dir)
    {
        float length = ClearLength(dir, beamWidth, beamHeight);
        fx.transform.SetPositionAndRotation(Origin + dir * effectOrbitRadius + Vector3.up * effectLift, Quaternion.FromToRotation(Vector3.up, dir));

        float visible = Mathf.Max(0.5f, length - effectOrbitRadius);
        foreach (ParticleSystemRenderer r in fx.GetComponentsInChildren<ParticleSystemRenderer>())
            if (IsBeam(r)) r.lengthScale = -visible / r.GetComponent<ParticleSystem>().main.startSize.constantMax; // 길이 = 크기 x 배율
        return length;
    }

    // 플레이어 거리에서의 선속도를 각속도로 환산
    float TrackRadiansPerSecond()
    {
        float speed = trackLinearSpeed;
        if (matchPlayerMoveSpeed && ai.PlayerTransform != null)
        {
            TopDownMove mover = ai.PlayerTransform.GetComponent<TopDownMove>();
            if (mover != null) speed = mover.moveSpeed;
        }

        float dist = 0.5f;
        if (ai.PlayerTransform != null)
        {
            Vector3 d = ai.PlayerTransform.position - Origin;
            d.y = 0f;
            dist = Mathf.Max(0.5f, d.magnitude);
        }
        return speed * trackSpeedScale / dist; // rad/s
    }

    // 레이저가 나가는 지점 (입). 높이는 보스 중심 그대로
    Vector3 Origin { get { return transform.position + muzzleOffset; } }

    Vector3 FlatDirTo(Vector3 point)
    {
        Vector3 d = point - Origin;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    // 지형에 막히지 않고 뻗을 수 있는 길이.
    // 예전엔 빔 굵기만 한 BoxCast 로 쟀는데, 그러면 벽과 거의 나란한 방향일 때
    // 시작하자마자 벽을 스쳐 길이가 1로 잘렸다 (플레이어가 벽에 붙으면 코앞에서 막힌 것처럼 보임).
    // 실제로 앞을 가로막은 것만 재도록 중심선 한 줄로 훑는다.
    float ClearLength(Vector3 dir, float width, float height)
    {
        Vector3 origin = Origin;
        origin.y = FindGroundY() + probeHeight; // 바닥에서 가슴 높이 — 낮은 장애물도 빔을 끊는다

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxLength, ~0, QueryTriggerInteraction.Ignore);
        float best = maxLength;
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject go = hits[i].collider.gameObject;
            if (go == gameObject) continue;
            if (ai.PlayerTransform != null && go == ai.PlayerTransform.gameObject) continue;
            // 몹은 레이저를 끊지 않는다. 몹 한 마리에 빔이 잘리면 예고도 실제 사거리도 읽을 수 없다
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            // 방어 토템도 끊지 않는다. 보스 바로 앞(9유닛)에 서는 물건이라 그냥 두면
            // 방어 모드 내내 레이저가 코앞에서 잘려 패턴 자체가 무력화된다
            if (BossTotem.Is(hits[i].collider)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return Mathf.Max(1f, best);
    }

    GameObject MakeBar(string name, Color color, float height)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>()); // 판정은 직접 한다
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = color;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    float PlaceBar(GameObject bar, Vector3 dir, float width, float height, bool onGround)
    {
        if (bar == null) return 0f;

        float length = ClearLength(dir, width, height);
        Vector3 mid = Origin + dir * (length * 0.5f);
        float y = onGround ? FindGroundY() + groundOffset + height * 0.5f : transform.position.y;
        bar.transform.position = new Vector3(mid.x, y, mid.z);
        bar.transform.rotation = Quaternion.LookRotation(dir);
        bar.transform.localScale = new Vector3(width, height, length);
        return length;
    }

    float FindGroundY()
    {
        return BossGround.Y(transform.position, transform.position.y);
    }

    void DamageInBeam(Vector3 dir, float length)
    {
        Transform p = ai.PlayerTransform;
        if (p == null) return;

        Vector3 d = p.position - Origin;
        d.y = 0f;
        float along = Vector3.Dot(d, dir);
        if (along < 0f || along > length) return;

        float perp = (d - dir * along).magnitude;
        if (perp > beamWidth * 0.5f + 0.5f) return; // 플레이어 반경만큼 여유

        Health h = p.GetComponent<Health>();
        if (h != null) h.TakeDamage(damagePerSecond * tickInterval);
    }
}
