using System.Collections;
using UnityEngine;

// 보스 특수패턴 6: 본체에서 굵은 레이저를 쏜다.
// 워닝 1.5초(플레이어 추적) → 시작지점 확정 → 0.7초 후 발사 → 3초간 플레이어를 따라 휘두른다.
[RequireComponent(typeof(EnemyAI))]
public class BossLaserPattern : MonoBehaviour
{
    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public float warningTime = 1.5f;   // 워닝이 플레이어를 따라다니는 시간
    public float lockDelay = 0.7f;     // 확정 후 발사까지
    public float fireDuration = 3f;    // 발사 유지 시간
    // 레이저 끝이 플레이어 거리에서 초당 이만큼 움직이도록 회전한다 (플레이어 이동속도와 맞춤).
    // 각속도로 고정하면 거리에 따라 체감이 달라지므로, 선속도 기준으로 매 프레임 환산한다
    public float trackLinearSpeed = 6f;
    public bool matchPlayerMoveSpeed = true; // 켜면 플레이어의 실제 이동속도를 그대로 따라간다

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

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    float nextCastTime;
    bool running;

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
        actionLock.LockFor(warningTime + lockDelay + fireDuration); // 레이저 내내 다른 패턴 금지

        Transform p = ai.PlayerTransform;
        Vector3 dir = FlatDirTo(p != null ? p.position : transform.position + transform.forward);

        // 1) 워닝: 플레이어를 따라다닌다
        GameObject warning = MakeBar("BossLaserWarning", warningColor, 0.1f);
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            if (ai.PlayerTransform != null) dir = FlatDirTo(ai.PlayerTransform.position);
            PlaceBar(warning, dir, beamWidth, 0.1f, true);
            yield return null;
        }

        // 2) 시작지점 확정
        Renderer wr = warning != null ? warning.GetComponent<Renderer>() : null;
        if (wr != null) wr.sharedMaterial.color = lockedColor;
        PlaceBar(warning, dir, beamWidth, 0.1f, true);
        yield return new WaitForSeconds(lockDelay);
        if (warning != null) Destroy(warning);

        // 3) 발사: 3초간 추적
        GameObject beam = MakeBar("BossLaser", beamColor, beamHeight);
        float fired = 0f, tick = 0f;
        while (fired < fireDuration)
        {
            float dt = Time.deltaTime;
            fired += dt;

            if (ai.PlayerTransform != null)
            {
                Vector3 want = FlatDirTo(ai.PlayerTransform.position);
                dir = Vector3.RotateTowards(dir, want, TrackRadiansPerSecond() * dt, 0f); // 회전 속도 제한
            }
            float length = PlaceBar(beam, dir, beamWidth, beamHeight, false);

            tick -= dt;
            if (tick <= 0f)
            {
                tick = tickInterval;
                DamageInBeam(dir, length);
            }
            yield return null;
        }

        if (beam != null) Destroy(beam);
        running = false;
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
            Vector3 d = ai.PlayerTransform.position - transform.position;
            d.y = 0f;
            dist = Mathf.Max(0.5f, d.magnitude);
        }
        return speed / dist; // rad/s
    }

    Vector3 FlatDirTo(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    // 지형에 막히지 않고 뻗을 수 있는 길이.
    // 예전엔 빔 굵기만 한 BoxCast 로 쟀는데, 그러면 벽과 거의 나란한 방향일 때
    // 시작하자마자 벽을 스쳐 길이가 1로 잘렸다 (플레이어가 벽에 붙으면 코앞에서 막힌 것처럼 보임).
    // 실제로 앞을 가로막은 것만 재도록 중심선 한 줄로 훑는다.
    float ClearLength(Vector3 dir, float width, float height)
    {
        Vector3 origin = transform.position;
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
        Vector3 mid = transform.position + dir * (length * 0.5f);
        float y = onGround ? FindGroundY() + groundOffset + height * 0.5f : transform.position.y;
        bar.transform.position = new Vector3(mid.x, y, mid.z);
        bar.transform.rotation = Quaternion.LookRotation(dir);
        bar.transform.localScale = new Vector3(width, height, length);
        return length;
    }

    float FindGroundY()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }

    void DamageInBeam(Vector3 dir, float length)
    {
        Transform p = ai.PlayerTransform;
        if (p == null) return;

        Vector3 d = p.position - transform.position;
        d.y = 0f;
        float along = Vector3.Dot(d, dir);
        if (along < 0f || along > length) return;

        float perp = (d - dir * along).magnitude;
        if (perp > beamWidth * 0.5f + 0.5f) return; // 플레이어 반경만큼 여유

        Health h = p.GetComponent<Health>();
        if (h != null) h.TakeDamage(damagePerSecond * tickInterval);
    }
}
