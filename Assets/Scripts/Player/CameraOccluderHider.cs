using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 카메라와 플레이어 사이를 가로막은 오브젝트를 서서히 투명하게 만든다.
// 껐다 켰다 하면 툭툭 끊겨 보이므로 알파를 부드럽게 오간다.
// 콜라이더는 건드리지 않으므로 이동·시야 판정은 그대로다.
public class CameraOccluderHider : MonoBehaviour
{
    public Transform target;              // 보통 Player
    public float checkInterval = 0.05f;   // 가림막 탐색 주기 (페이드는 매 프레임 진행된다)
    public float probeRadius = 0.7f;      // 플레이어 폭만큼 두껍게 본다
    public float targetMargin = 1.2f;     // 플레이어 바로 앞은 가림막으로 치지 않는다
    public float minHeightToHide = 1.5f;  // 이보다 납작한 것(바닥·도로·외곽선)은 건드리지 않는다
    public float groundProbe = 3f;        // 발밑 이만큼 아래까지 '딛고 선 것'을 찾는다

    [Header("페이드")]
    public float fadedAlpha = 0.12f;      // 가릴 때 남기는 불투명도
    public float fadeInSpeed = 6f;        // 사라지는 속도 (알파/초)
    public float fadeOutSpeed = 3f;       // 되돌아오는 속도

    class Faded
    {
        public Renderer renderer;
        public Material[] original;   // 원래 쓰던 공유 재질
        public Material[] instances;  // 투명하게 바꾼 복제본
        public float alpha = 1f;
        public bool blocking;
    }

    readonly Dictionary<Renderer, Faded> tracked = new Dictionary<Renderer, Faded>();
    readonly HashSet<Renderer> blockingNow = new HashSet<Renderer>();
    readonly List<Renderer> finished = new List<Renderer>();
    Renderer standingOn;   // 이번 Scan에서 플레이어가 딛고 있는 구조물
    float standingOnY;     // 그 접지면의 높이
    float timer;

    void Awake()
    {
        if (target == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }
    }

    void OnDisable() { RestoreAll(); }

    void LateUpdate()
    {
        if (target == null) return;

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            timer = checkInterval;
            Scan();
        }

        StepFade();
    }

    void Scan()
    {
        blockingNow.Clear();
        FindGround();

        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up * 0.5f;
        Vector3 delta = to - from;
        float dist = delta.magnitude - targetMargin;
        if (dist > 0.01f)
        {
            // 카메라에서 플레이어 쪽으로 쏘면, 카메라가 건물 안에 들어갔을 때 벽의 '뒷면'만
            // 만나는데 유니티는 기본적으로 뒷면을 맞히지 않는다 (queriesHitBackfaces=false).
            // 그래서 반대로 플레이어에서 카메라 쪽으로 쏜다. 사이에 낀 것은 늘 앞면을 보여 준다
            Vector3 dir = -delta / delta.magnitude;
            Vector3 origin = to + dir * targetMargin;   // 플레이어 바로 앞은 건너뛴다
            RaycastHit[] hits = Physics.SphereCastAll(origin, probeRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++) Consider(hits[i].collider);

            // 출발 지점에 이미 겹쳐 있던 것도 캐스트가 보고하지 않으므로 따로 훑는다
            Collider[] atOrigin = Physics.OverlapSphere(origin, probeRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < atOrigin.Length; i++) Consider(atOrigin[i]);
        }

        // 스피어캐스트는 '출발 지점에서 이미 겹쳐 있는' 콜라이더를 아예 보고하지 않는다.
        // 건물에 바짝 붙으면 카메라가 건물 안으로 들어가는데, 그게 정확히 이 경우라
        // 제일 가려야 할 순간에 투명화가 안 걸렸다. 그래서 출발 지점도 따로 훑는다
        Collider[] atCamera = Physics.OverlapSphere(from, probeRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < atCamera.Length; i++) Consider(atCamera[i]);

        foreach (var kv in tracked) kv.Value.blocking = blockingNow.Contains(kv.Key);
    }

    void Consider(Collider col)
    {
        Renderer r = FindRenderer(col);
        if (r == null || !ShouldFade(r)) return;
        blockingNow.Add(r);
        if (!tracked.ContainsKey(r)) tracked.Add(r, Begin(r));
    }

    void StepFade()
    {
        if (tracked.Count == 0) return;
        finished.Clear();

        foreach (var kv in tracked)
        {
            Faded f = kv.Value;
            if (f.renderer == null) { finished.Add(kv.Key); continue; }

            float goal = f.blocking ? fadedAlpha : 1f;
            float speed = f.blocking ? fadeInSpeed : fadeOutSpeed;
            f.alpha = Mathf.MoveTowards(f.alpha, goal, speed * Time.unscaledDeltaTime);
            SetAlpha(f, f.alpha);

            // 완전히 되돌아왔으면 원래 공유 재질로 복귀시켜 배칭을 되살린다
            if (!f.blocking && f.alpha >= 0.999f) finished.Add(kv.Key);
        }

        for (int i = 0; i < finished.Count; i++)
        {
            Faded f;
            if (tracked.TryGetValue(finished[i], out f)) End(f);
            tracked.Remove(finished[i]);
        }
    }

    Faded Begin(Renderer r)
    {
        var f = new Faded();
        f.renderer = r;
        f.original = r.sharedMaterials;
        f.instances = new Material[f.original.Length];
        for (int i = 0; i < f.original.Length; i++)
        {
            Material src = f.original[i];
            Material m = src != null ? new Material(src) : null;
            if (m != null) MakeTransparent(m);
            f.instances[i] = m;
        }
        r.materials = f.instances; // 복제본을 쓰므로 같은 재질을 공유하는 다른 건물은 영향 없다
        f.alpha = 1f;
        return f;
    }

    void End(Faded f)
    {
        if (f.renderer != null) f.renderer.sharedMaterials = f.original;
        for (int i = 0; i < f.instances.Length; i++)
            if (f.instances[i] != null) Destroy(f.instances[i]);
    }

    void RestoreAll()
    {
        foreach (var kv in tracked) End(kv.Value);
        tracked.Clear();
        blockingNow.Clear();
    }

    void SetAlpha(Faded f, float a)
    {
        for (int i = 0; i < f.instances.Length; i++)
        {
            Material m = f.instances[i];
            if (m == null) continue;
            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = a;
                m.SetColor("_BaseColor", c);
            }
            else m.color = new Color(m.color.r, m.color.g, m.color.b, a);
        }
    }

    // URP/Lit 을 반투명으로 돌리려면 표면 타입과 블렌드를 직접 잡아 줘야 한다
    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    // 플레이어 발밑에서 실제로 딛고 있는 구조물과 그 높이를 찾는다
    void FindGround()
    {
        standingOn = null;
        standingOnY = float.NegativeInfinity;
        if (target == null) return;

        RaycastHit[] hits = Physics.RaycastAll(target.position + Vector3.up * 0.5f, Vector3.down, groundProbe, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].collider.transform;
            if (t == target || t.IsChildOf(target)) continue; // 자기 자신은 바닥이 아니다
            if (hits[i].point.y <= standingOnY) continue;
            standingOnY = hits[i].point.y;
            standingOn = FindRenderer(hits[i].collider);
        }
    }

    // 지붕과 벽이 다른 렌더러로 나뉜 건물도 같은 구조물로 본다
    static bool SameStructure(Renderer a, Renderer b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        return a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
    }

    Renderer FindRenderer(Collider col)
    {
        Renderer r = col.GetComponent<Renderer>();
        if (r != null) return r;
        return col.GetComponentInParent<Renderer>();
    }

    bool ShouldFade(Renderer r)
    {
        Transform t = r.transform;
        if (target != null && (t == target || t.IsChildOf(target))) return false;
        if (r.GetComponentInParent<EnemyAI>() != null) return false;   // 적은 가려도 보여야 한다
        // 플레이어가 '그 구조물 위에 올라서 있으면' 가림막이 아니라 딛고 선 바닥이다.
        // 옥상에 올라갔을 때 건물이 통째로 투명해지는 것을 막는다.
        //
        // 같은 구조물이기만 하면 면제하면 안 된다 — 건물 밑동에 깔린 보도블록
        // (Set_B_Tiles_01, 높이 0.2)이 건물의 자식이라, 그 위에 서기만 해도
        // 건물이 면제되어 정작 가릴 때 안 가려졌다.
        // 그래서 접지면이 그 구조물 바닥보다 확실히 위일 때만 면제한다
        if (standingOn != null && SameStructure(r, standingOn)
            && standingOnY > r.bounds.min.y + minHeightToHide) return false;
        if (r.bounds.size.y < minHeightToHide) return false;           // 바닥·도로·외곽선
        return true;
    }
}
