using UnityEngine;

// 빛의 구체. 카메라를 보는 판 하나에 가운데가 하얗고 바깥이 금빛으로 번지는 원을 그린다.
// 일반 반투명이라 밝은 바닥에서도 보인다. 시간이 멈춘 연출(timeScale 0) 중에도 돌도록 unscaled 시간을 쓴다.
// 움직이는 동안 지나간 자리에 잔상을 남겨 꼬리처럼 보이게 한다
public class LightOrb : MonoBehaviour
{
    static Texture2D tex;
    static Material mat;

    public float size = 1.4f;
    public bool trail = true;
    float pulse;
    Vector3 lastTrail;

    public static LightOrb Spawn(Vector3 at, float size)
    {
        GameObject q = Quad("LightOrb", at);
        LightOrb o = q.AddComponent<LightOrb>();
        o.size = size;
        o.lastTrail = at;
        o.LateUpdate();
        return o;
    }

    // 한 번 크게 번졌다가 사라지는 빛 (착지 순간)
    public static void Burst(Vector3 at, float fromSize, float toSize, float life)
    {
        GameObject q = Quad("LightBurst", at);
        q.AddComponent<OrbFade>().Init(fromSize, toSize, life, 1f);
    }

    static GameObject Quad(string name, Vector3 at)
    {
        if (tex == null) Build();
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.position = at;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return q;
    }

    static void Build()
    {
        const int N = 128;
        Color gold = new Color(1f, 0.68f, 0.12f); // 밝은 바닥에서도 보이게 진한 금색
        tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - d / 0.35f);         // 하얀 심
                float glow = Mathf.Pow(Mathf.Clamp01(1f - d), 1.3f); // 바깥으로 번지는 금빛
                Color c = Color.Lerp(gold, Color.white, core);
                c.a = Mathf.Clamp01(glow * 1.6f);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
        pulse += Time.unscaledDeltaTime * 9f;
        transform.localScale = Vector3.one * size * (1f + 0.08f * Mathf.Sin(pulse)); // 살짝 일렁인다

        if (!trail) return;
        if ((transform.position - lastTrail).sqrMagnitude < 0.25f * size * size) return;
        lastTrail = transform.position;
        GameObject q = Quad("LightOrbTrail", transform.position);
        q.AddComponent<OrbFade>().Init(size * 0.8f, size * 0.2f, 0.35f, 0.6f);
    }
}

// 잔상·번쩍임: 크기를 바꾸며 옅어지다 사라진다 (unscaled)
public class OrbFade : MonoBehaviour
{
    float time, from, to, life, alpha;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public void Init(float from, float to, float life, float alpha)
    {
        this.from = from; this.to = to; this.life = life; this.alpha = alpha;
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        LateUpdate();
    }

    void LateUpdate()
    {
        time += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(time / life);
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
        float e = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(from, to, e);
        mpb.SetColor("_Color", new Color(1f, 1f, 1f, alpha * (1f - t)));
        rend.SetPropertyBlock(mpb);
        if (t >= 1f) Destroy(gameObject);
    }
}
