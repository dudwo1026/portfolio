using UnityEngine;
using UnityEngine.UI;

// 워프 빛기둥. 카메라를 보는 세로 판 하나에 가운데가 하얗고 가장자리가 파란 띠를 그린다.
// 빛을 더하는(additive) 이펙트는 밝은 바닥에서 묻혀서, 일반 반투명으로 칠한다
public class WarpBeam : MonoBehaviour
{
    static Texture2D tex;
    static Material mat;

    float time, duration, maxWidth, height;
    const float behind = 0.7f;
    bool shrinkOnly;
    Transform quad;

    static readonly Color Edge = new Color(0.25f, 0.65f, 1f); // 가장자리 파랑

    // shrinkOnly: 도착용. 처음부터 굵게 꽂혔다가 가늘어진다. 아니면 출발용: 굵어졌다가 가늘어진다
    public static void Spawn(Vector3 feet, float duration, float width, float height, bool shrinkOnly)
    {
        if (tex == null) BuildAssets();

        GameObject go = new GameObject("WarpBeam");
        go.transform.position = feet;
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(go.transform, false);
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        WarpBeam b = go.AddComponent<WarpBeam>();
        b.quad = q.transform;
        b.duration = duration; b.maxWidth = width; b.height = height; b.shrinkOnly = shrinkOnly;
        b.LateUpdate();
    }

    // 가운데 흰 심지 + 바깥으로 갈수록 색이 진해지다 투명해지는 가로 단면. 위아래 끝도 부드럽게 사라진다
    static void BuildAssets()
    {
        const int W = 64, H = 64;
        tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < H; y++)
        {
            float v = y / (H - 1f);
            float vFade = Mathf.Clamp01(v / 0.08f) * Mathf.Clamp01((1f - v) / 0.35f); // 바닥은 짧게, 위는 길게 흐려진다
            for (int x = 0; x < W; x++)
            {
                float u = Mathf.Abs(x / (W - 1f) - 0.5f) * 2f; // 0 = 가운데, 1 = 가장자리
                float core = Mathf.Clamp01(1f - u / 0.35f);    // 흰 심지
                float a = Mathf.Clamp01(1f - u) * vFade;
                Color c = Color.Lerp(Edge, Color.white, core);
                c.a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        mat = new Material(Shader.Find("Sprites/Default")); // 반투명, 양면
        mat.mainTexture = tex;
    }

    void LateUpdate()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);
        // 출발: 0.3까지 빠르게 굵어지고 나머지 동안 가늘어진다 / 도착: 처음이 최대
        float w = shrinkOnly ? 1f - t * t : (t < 0.3f ? Mathf.Sin(t / 0.3f * Mathf.PI * 0.5f) : 1f - (t - 0.3f) / 0.7f);
        w = Mathf.Max(0f, w);

        Camera cam = Camera.main;
        if (cam != null)
        {
            // 세로는 그대로 두고 가로만 카메라를 본다
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-toCam);
        }
        quad.localScale = new Vector3(maxWidth * w, height, 1f);
        // 카메라 반대쪽으로 조금 물려 캐릭터 뒤에 그려지게 한다 (반투명끼리는 먼 것부터 그린다)
        quad.localPosition = new Vector3(0f, height * 0.5f - 1f, behind); // 밑동은 바닥 아래로 묻어 땅에서 솟는 것처럼

        if (t >= 1f) Destroy(gameObject);
    }

    // 화면 전체를 잠깐 하얗게 덮었다가 걷는다
    public static void ScreenFlash(float alpha, float duration)
    {
        GameObject go = new GameObject("WarpFlash");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 500;
        Image img = new GameObject("Img", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.raycastTarget = false;
        go.AddComponent<WarpFlash>().Init(img, alpha, duration);
    }
}

public class WarpFlash : MonoBehaviour
{
    Image img; float alpha, duration, time;

    public void Init(Image i, float a, float d) { img = i; alpha = a; duration = d; Update(); }

    void Update()
    {
        time += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(time / duration);
        img.color = new Color(1f, 1f, 1f, alpha * (1f - t));
        if (t >= 1f) Destroy(gameObject);
    }
}
