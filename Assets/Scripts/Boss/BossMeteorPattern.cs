using System.Collections;
using UnityEngine;

// 보스 특수패턴 4: 2초 캐스팅 후 플레이어 머리 위로 운석을 연달아 떨군다.
// 캐스팅이 끝나면 보스는 바로 다른 행동을 할 수 있고, 운석 낙하는 알아서 진행된다.
[RequireComponent(typeof(EnemyAI))]
public class BossMeteorPattern : MonoBehaviour, IBossMotion
{
    // 스프라이트 모션: 캐스팅 동안 0→1(빛기둥을 쏘아 올리는 장들), 끝난 뒤 castRecoveryTime 동안 1→2(마무리 장)
    public string MotionName { get { return "Meteor"; } }
    public bool MotionActive { get { return running || Time.time - castEndTime < castRecoveryTime; } }
    public float MotionProgress
    {
        get
        {
            if (running) return castTime > 0f ? Mathf.Clamp01((Time.time - castStartTime) / castTime) : 1f;
            return 1f + Mathf.Clamp01((Time.time - castEndTime) / Mathf.Max(0.01f, castRecoveryTime));
        }
    }
    public float castRecoveryTime = 0.25f;
    float castStartTime;
    float castEndTime = -999f;

    [Header("쿨타임")]
    public float cooldownMin = 18f;
    public float cooldownMax = 23f;
    public float cooldownStepPerPhase = 1f; // 페이즈마다 이만큼 짧아진다
    public float cooldownFloor = 5f;
    public float castTime = 2f;        // 이 시간 동안 보스가 캐스팅
    public float warningDelay = 1.5f;  // 워닝 생성 후 낙하까지
    public float spawnInterval = 0.5f; // 운석과 운석 사이 간격
    public int baseCount = 4;          // 기본 개수. 페이즈마다 +1

    [Header("운석")]
    public float damage = 30f;
    public float radius = 2.5f;        // 지름 5
    public float fallHeight = 25f;
    public float meteorScale = 2f;

    [Header("운석 그림 (비워 두면 구체)")]
    public Texture2D[] meteorFrames;          // 번갈아 재생한다
    public Material meteorMaterial;           // 몹 스프라이트와 같은 재질
    public float meteorFramesPerSecond = 15f; // 그림 번갈아 재생 속도만. 낙하 시간은 warningDelay
    public float meteorSpriteWidth = 6f;      // 그림 가로 폭(월드). 바위 머리 지름이 약 2.5
    public Vector2 meteorHeadPx = new Vector2(106f, 128f); // 그림 속 바위 머리 중심 (아래에서 위로 px)
    public float meteorArtAngle = 204f;       // 그림 속 진행 방향(도, 화면 오른쪽 = 0, 반시계). 꼬리가 오른쪽 위로 끌린다
    // 그림 운석은 이 높이에서 떨어진다. 카메라가 가까이 내려다봐서 fallHeight(25)에서 떨구면 대부분 화면 밖이다
    public float spriteFallHeight = 10f;
    // 화면 오른쪽 위에서 떨어진다. 낙하 높이 대비 가로 거리. 카메라가 40도 내려다봐서 높이는 화면에 0.77배로 보이므로
    // 화면상 각도 = atan(0.77 / 이 값). 0.17이면 약 77도
    public float fallSlant = 0.17f;
    public float meteorLandHeight = 1f;       // 바위 머리가 이 높이에 닿는 순간 터진다

    [Header("색")]
    public Color castColor = new Color(1f, 0.45f, 0.05f);
    public Color warningColor = new Color(1f, 0.25f, 0.05f);
    public Color meteorColor = new Color(0.25f, 0.06f, 0.02f);
    public Color impactColor = new Color(1f, 0.6f, 0.15f);
    public float impactLifeTime = 0.35f;

    EnemyAI ai;
    BossObstaclePattern obstacles;
    BossActionLock actionLock;
    Renderer rend;
    Material normalMaterial;
    float nextCastTime;
    bool running;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        obstacles = GetComponent<BossObstaclePattern>();
        actionLock = GetComponent<BossActionLock>();
        if (actionLock == null) actionLock = gameObject.AddComponent<BossActionLock>();
        rend = GetComponent<Renderer>();
        if (rend != null) normalMaterial = rend.sharedMaterial;
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
        if (actionLock.IsLocked) return; // 다른 패턴이 보스를 묶고 있다

        actionLock.LockFor(castTime); // 캐스팅 동안은 다른 패턴이 끼어들지 못한다
        nextCastTime = Time.time + NextCooldown();
        StartCoroutine(Run());
    }

    int MeteorCount()
    {
        int extra = obstacles != null ? obstacles.TriggeredCount : 0; // 페이즈가 오를수록 하나씩
        return baseCount + extra;
    }

    IEnumerator Run()
    {
        running = true;
        castStartTime = Time.time;

        SetCastVisual(true);
        yield return new WaitForSeconds(castTime);
        SetCastVisual(false);

        castEndTime = Time.time;
        running = false; // 캐스팅만 끝나면 보스는 자유. 낙하는 이어서 진행된다

        int count = MeteorCount();
        for (int i = 0; i < count; i++)
        {
            Transform p = ai.PlayerTransform;
            if (p == null) yield break;

            StartCoroutine(DropMeteor(p.position)); // 그 순간의 플레이어 위치를 노린다
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    IEnumerator DropMeteor(Vector3 target)
    {
        float groundY = FindGroundY(target);
        Vector3 impact = new Vector3(target.x, groundY, target.z);

        GameObject warning = MakeDisc("MeteorWarning", impact, radius * 2f, 0.08f, warningColor);

        if (meteorFrames != null && meteorFrames.Length > 0 && meteorMaterial != null)
        {
            yield return FallSprite(impact);
            if (warning != null) Destroy(warning);
            Explode(impact);
            yield break;
        }

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Meteor";
        Destroy(rock.GetComponent<Collider>()); // 낙하 중에는 부딪히지 않는다
        rock.transform.localScale = Vector3.one * meteorScale;
        rock.GetComponent<Renderer>().sharedMaterial = MakeMaterial(meteorColor);

        float t = 0f;
        Vector3 from = impact + Vector3.up * fallHeight;
        Vector3 to = impact + Vector3.up * meteorScale * 0.5f;
        while (t < warningDelay)
        {
            t += Time.deltaTime;
            if (rock != null) rock.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / warningDelay));
            yield return null;
        }

        if (warning != null) Destroy(warning);
        if (rock != null) Destroy(rock);

        Explode(impact);
    }

    // 카메라를 마주 보는 판에 운석 그림을 번갈아 그리며, 화면 오른쪽 위에서 착탄 지점으로 비스듬히 떨어뜨린다.
    // 판은 실제 화면상 낙하 방향으로 돌려서 꼬리가 진행 반대쪽으로 끌리게 한다
    IEnumerator FallSprite(Vector3 impact)
    {
        Camera cam = Camera.main;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        Vector3 to = impact + Vector3.up * meteorLandHeight;
        Vector3 from = to + Vector3.up * spriteFallHeight + right * spriteFallHeight * fallSlant;

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Quad);
        rock.name = "Meteor";
        Destroy(rock.GetComponent<Collider>()); // 낙하 중에는 부딪히지 않는다
        Renderer r = rock.GetComponent<Renderer>();
        r.sharedMaterial = meteorMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        Texture2D first = meteorFrames[0];
        float w = meteorSpriteWidth;
        float h = first != null ? w * first.height / first.width : w;
        rock.transform.localScale = new Vector3(w, h, 1f);
        // 판 중심에서 바위 머리까지 (판 로컬, 월드 단위)
        Vector3 headLocal = first != null
            ? new Vector3((meteorHeadPx.x / first.width - 0.5f) * w, (meteorHeadPx.y / first.height - 0.5f) * h, 0f)
            : Vector3.zero;

        float t = 0f;
        while (t < warningDelay)
        {
            t += Time.deltaTime;
            if (rock == null) yield break;

            Texture2D tex = meteorFrames[Mathf.FloorToInt(t * meteorFramesPerSecond) % meteorFrames.Length];
            r.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", tex);
            mpb.SetTexture("_MainTex", tex);
            r.SetPropertyBlock(mpb);

            Quaternion rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (cam != null)
            {
                Vector3 move = to - from;
                float pathDeg = Mathf.Atan2(Vector3.Dot(move, cam.transform.up), Vector3.Dot(move, cam.transform.right)) * Mathf.Rad2Deg;
                rot = rot * Quaternion.Euler(0f, 0f, pathDeg - meteorArtAngle);
            }
            Vector3 head = Vector3.Lerp(from, to, Mathf.Clamp01(t / warningDelay));
            rock.transform.SetPositionAndRotation(head - rot * headLocal, rot);
            yield return null;
        }

        if (rock != null) Destroy(rock);
    }

    void Explode(Vector3 impact)
    {
        Collider[] hits = Physics.OverlapSphere(impact + Vector3.up * 1f, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponent<PlayerCombat>() == null) continue; // 플레이어만
            Health h = hits[i].GetComponent<Health>();
            if (h != null) h.TakeDamage(damage);
        }

        GameObject flash = MakeDisc("MeteorImpact", impact, radius * 2f, 0.12f, impactColor);
        Destroy(flash, impactLifeTime);
    }

    float FindGroundY(Vector3 point)
    {
        return BossGround.Y(point, point.y); // 플레이어·토템·장애물 위가 아니라 그 밑 바닥
    }

    GameObject MakeDisc(string name, Vector3 pos, float diameter, float thickness, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = new Vector3(pos.x, pos.y + thickness * 0.5f, pos.z);
        go.transform.localScale = new Vector3(diameter, thickness, diameter);
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
        return go;
    }

    Material MakeMaterial(Color c)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        return m;
    }

    void SetCastVisual(bool on)
    {
        if (rend == null) return;
        if (on) rend.material.color = castColor;
        else if (normalMaterial != null) rend.sharedMaterial = normalMaterial;
    }
}
