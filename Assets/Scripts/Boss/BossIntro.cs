using System.Collections;
using UnityEngine;

// 보스 등장 연출. 옥상에서 깨어나면(OnEnable) 플레이어의 워프 도착 연출이 끝나길 기다렸다가 arriveDelay 뒤에
// 카메라가 보스 쪽으로 넘어가며 당겨지고, 등장 그림(모션 "Spawn") 한 장으로 roarTime 동안 포효한 뒤 플레이어로 돌아온다.
// 포효 중엔 화면이 흔들리고 머리에서 충격파 고리가 연달아 퍼져 나간다 (할로우나이트 보스 포효 느낌).
// 연출 동안 플레이어는 움직이거나 쏠 수 없고, 보스·옥상 스포너도 기다린다
public class BossIntro : MonoBehaviour, IBossMotion
{
    public float arriveDelay = 1f;      // 플레이어가 완전히 나타난 뒤 연출 시작까지
    public float panTime = 0.6f;        // 카메라가 보스로 넘어가는 / 돌아오는 시간
    public float zoom = 0.7f;           // 보스를 비출 때 카메라 거리 배율 (작을수록 당겨진다)
    public float focusHeight = 2.5f;    // 보스 중심에서 카메라가 볼 높이
    public float roarTime = 1.5f;
    public float shakeAmplitude = 0.45f;
    public float ringInterval = 0.22f;
    public float ringLife = 0.7f;
    public float ringMaxSize = 26f;     // 고리가 퍼지는 최대 지름 (월드)
    public float headHeight = 3.5f;     // 보스 중심에서 고리가 나오는 높이
    public Color ringColor = new Color(1f, 1f, 1f, 0.85f);

    public string MotionName { get { return "Spawn"; } }
    public bool MotionActive { get { return roaring; } }
    public float MotionProgress { get { return 0f; } }

    bool played, roaring;
    // 등장 연출(카메라가 보스로 넘어갔다 돌아올 때까지) 중인지. 그 사이엔 일시정지를 열지 않는다
    public static bool Playing { get; private set; }
    static Texture2D ringTex;
    static Material ringMat;

    void OnEnable()
    {
        if (!played) StartCoroutine(Roar());
    }

    IEnumerator Roar()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        Rigidbody rb = GetComponent<Rigidbody>();
        if (ai != null) ai.patternDriving = true; // 연출이 끝날 때까지 제자리

        // 씬을 켤 때 보스가 잠깐 켜졌다가 AreaManager가 바로 끄므로, 기다림이 끝나야 본 것으로 친다 (꺼지면 코루틴도 같이 멈춘다)
        while (Portal.Warping) yield return null;
        yield return new WaitForSeconds(arriveDelay);
        played = true;
        Playing = true;

        float total = panTime * 2f + roarTime + 0.5f;
        HoldFightFor(total);
        GameObject p = GameObject.Find("Player");
        TopDownMove mover = p != null ? p.GetComponent<TopDownMove>() : null;
        PlayerCombat combat = p != null ? p.GetComponent<PlayerCombat>() : null;
        PlayerDash dash = p != null ? p.GetComponent<PlayerDash>() : null;
        bool wasLocked = mover != null && mover.movementLocked;
        if (mover != null) mover.movementLocked = true;
        if (combat != null) combat.enabled = false;
        if (dash != null) dash.enabled = false;
        if (rb != null) rb.linearVelocity = Vector3.zero;

        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.cutFocus = transform.position + Vector3.up * focusHeight;
            follow.cutDistance = follow.distance * zoom;
            for (float t = 0f; t < panTime; t += Time.deltaTime) { follow.cutWeight = Mathf.SmoothStep(0f, 1f, t / panTime); yield return null; }
            follow.cutWeight = 1f;
        }

        roaring = true;
        CameraFollow.Shake(shakeAmplitude, roarTime);
        float nextRing = 0f;
        for (float t = 0f; t < roarTime; t += Time.deltaTime)
        {
            if (t >= nextRing && t < roarTime - ringLife * 0.5f)
            {
                SpawnRing();
                nextRing += ringInterval;
            }
            yield return null;
        }
        roaring = false;

        if (follow != null)
        {
            for (float t = 0f; t < panTime; t += Time.deltaTime) { follow.cutWeight = 1f - Mathf.SmoothStep(0f, 1f, t / panTime); yield return null; }
            follow.cutWeight = 0f;
        }

        if (mover != null) mover.movementLocked = wasLocked;
        if (combat != null) combat.enabled = true;
        if (dash != null) dash.enabled = true;
        if (ai != null) ai.patternDriving = false;
        Playing = false;
    }

    void OnDisable()
    {
        Playing = false; // 연출 도중 꺼지거나 씬이 바뀌어도 남지 않게
    }

    // 연출 동안 보스 패턴·평타와 옥상 몹 소환을 미룬다 (AreaManager의 도착 유예와 같은 방식)
    void HoldFightFor(float seconds)
    {
        BossActionLock actionLock = GetComponent<BossActionLock>();
        if (actionLock == null) actionLock = gameObject.AddComponent<BossActionLock>();
        actionLock.LockFor(seconds);
        BossMeleeAttack melee = GetComponent<BossMeleeAttack>();
        if (melee != null) melee.DelayFor(seconds);
        EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
            if (spawners[i].roofSpawner) spawners[i].DelayNextSpawn(seconds);
    }

    void SpawnRing()
    {
        if (ringTex == null) BuildRing();
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "RoarRing";
        Destroy(q.GetComponent<Collider>());
        q.transform.position = transform.position + Vector3.up * headHeight;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = ringMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.AddComponent<RoarRing>().Init(ringLife, ringMaxSize, ringColor);
    }

    // 바깥 가장자리가 진하고 안쪽으로 빠르게 옅어지는 얇은 고리
    static void BuildRing()
    {
        const int N = 128;
        ringTex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        ringTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((1f - d) / 0.03f);          // 바깥 날은 선명하게
                float inner = Mathf.Clamp01((d - 0.78f) / 0.2f);        // 안쪽으로 번진다
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, outer * inner * inner));
            }
        ringTex.Apply();
        ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.mainTexture = ringTex;
    }
}

// 포효 고리 하나: 카메라를 보고, 빠르게 커지며 옅어진다
public class RoarRing : MonoBehaviour
{
    float time, life, maxSize;
    Color color;
    MaterialPropertyBlock mpb;
    Renderer rend;

    public void Init(float life, float maxSize, Color color)
    {
        this.life = life; this.maxSize = maxSize; this.color = color;
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        LateUpdate();
    }

    void LateUpdate()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / life);
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
        float e = 1f - (1f - t) * (1f - t) * (1f - t); // 처음에 확 퍼진다
        transform.localScale = Vector3.one * Mathf.Lerp(2f, maxSize, e);
        mpb.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * (1f - t)));
        rend.SetPropertyBlock(mpb);
        if (t >= 1f) Destroy(gameObject);
    }
}
