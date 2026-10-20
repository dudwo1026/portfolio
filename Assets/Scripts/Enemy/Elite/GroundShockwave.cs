using UnityEngine;

// 바닥에 깔려 퍼지는 충격파 고리. 폭발 범위(radius)까지 빠르게 번진다.
// 뒤따라 범위 가장자리 쪽에 작은 폭발을 빙 둘러 터뜨려, 가운데 폭발이 범위 끝까지 닿는 것처럼 보이게 한다.
// 자폭한 몹은 바로 지워지므로 이 오브젝트가 혼자 끝까지 돈다
public class GroundShockwave : MonoBehaviour
{
    static Texture2D tex;

    float time, life, radius;
    Color color;
    GameObject ringBlast;
    int ringCount;
    float ringBlastScale;
    bool ringSpawned;
    Material mat;

    public static void Spawn(Vector3 ground, float radius, Color color, float life, GameObject ringBlast, int ringCount, float ringBlastScale)
    {
        if (tex == null) Build();
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "GroundShockwave";
        Destroy(q.GetComponent<Collider>());
        q.transform.position = ground + Vector3.up * 0.08f;
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        GroundShockwave w = q.AddComponent<GroundShockwave>();
        w.mat = new Material(Shader.Find("Sprites/Default"));
        w.mat.mainTexture = tex;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = w.mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        w.radius = radius; w.color = color; w.life = life;
        w.ringBlast = ringBlast; w.ringCount = ringCount; w.ringBlastScale = ringBlastScale;
        w.Update();
    }

    // 바깥 날이 하얗게 선명하고 안쪽으로 색이 번지는 굵은 고리
    static void Build()
    {
        const int N = 256;
        tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Clamp01((1f - d) / 0.025f);
                float band = Mathf.Clamp01((d - 0.6f) / 0.4f);
                float core = Mathf.Clamp01((d - 0.9f) / 0.1f); // 바깥 날 가까이는 하얗게
                Color c = new Color(1f, 1f, 1f, edge * band * band);
                c.r = c.g = c.b = Mathf.Lerp(0.85f, 1f, core);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
    }

    void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / life);
        float e = 1f - (1f - t) * (1f - t) * (1f - t); // 처음에 확 번진다
        transform.localScale = Vector3.one * Mathf.Lerp(1f, radius * 2f, e);
        mat.color = new Color(color.r, color.g, color.b, color.a * (1f - t * t));

        if (!ringSpawned && t >= 0.25f && ringBlast != null)
        {
            ringSpawned = true;
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < ringCount; i++)
            {
                float a = (a0 + i * 360f / ringCount) * Mathf.Deg2Rad;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.75f;
                GameObject fx = Instantiate(ringBlast, at, Quaternion.identity);
                fx.transform.localScale = Vector3.one * ringBlastScale;
                Destroy(fx, 3f);
            }
        }
        if (t >= 1f) Destroy(gameObject);
    }
}
