using UnityEngine;

// 근접 공격 궤적. 실제 판정 범위(부채꼴)를 휘두르는 방향으로 쓸고 지나간 뒤 사라진다 —
// 스프라이트 모션만으로는 어디까지 닿는지 알 수 없었다.
// 바닥 위에 눕힌 부채꼴 메쉬를 매 프레임 다시 만든다. 휘두르는 끝(앞쪽)과 바깥 테두리가 가장 진하고,
// 지나간 자리와 안쪽은 옅어진다. 바닥(보도블록)이 거의 흰색이라 바깥에 어두운 테두리를 한 줄 더 둘러 경계를 세운다
public class MeleeSlashArc : MonoBehaviour
{
    const int Segments = 28;
    const int Rings = 4;              // 안쪽 · 중간 · 바깥 · 테두리 끝
    const float RimWidth = 0.12f;     // 어두운 테두리 두께 (월드 단위)
    const float RimDarken = 0.55f;    // 테두리 색 = 궤적 색 x 이 값

    float inner, outer, halfAngle, sweepTime, fadeTime, t;
    int direction; // 1 = 왼쪽에서 오른쪽, -1 = 반대
    Color color;
    Mesh mesh;
    Material mat;
    Vector3[] verts;
    Color[] colors;

    public static void Spawn(Vector3 groundCenter, Vector3 forward, float innerRadius, float outerRadius,
                             float angle, Color color, float sweepTime, float fadeTime, int direction)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;

        GameObject go = new GameObject("MeleeSlash", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetPositionAndRotation(groundCenter, Quaternion.LookRotation(forward.normalized));

        MeleeSlashArc a = go.AddComponent<MeleeSlashArc>();
        a.inner = innerRadius;
        a.outer = outerRadius;
        a.halfAngle = angle * 0.5f;
        a.color = color;
        a.sweepTime = sweepTime;
        a.fadeTime = fadeTime;
        a.direction = direction >= 0 ? 1 : -1;
        a.Init();
        Destroy(go, sweepTime + fadeTime + 0.05f);
    }

    void Init()
    {
        mesh = new Mesh { name = "MeleeSlash" };
        mesh.MarkDynamic();
        verts = new Vector3[(Segments + 1) * Rings];
        colors = new Color[verts.Length];

        // 링 사이 띠 3개 x 각도 칸. 칸마다 사각형 하나 = 삼각형 2개
        int[] tris = new int[Segments * (Rings - 1) * 6];
        int k = 0;
        for (int i = 0; i < Segments; i++)
            for (int ring = 0; ring < Rings - 1; ring++)
            {
                int a = i * Rings + ring, b = a + 1, c = a + Rings, d = c + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = b; tris[k++] = d; tris[k++] = c;
            }
        Build();
        mesh.triangles = tris;

        GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = GetComponent<MeshRenderer>();
        mat = new Material(Shader.Find("Sprites/Default")); // 정점 색·알파 그대로, 조명 없음, 양면
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void Update()
    {
        t += Time.deltaTime;
        Build();
    }

    void Build()
    {
        float sweep = sweepTime > 0f ? Mathf.Clamp01(t / sweepTime) : 1f;
        sweep = 1f - (1f - sweep) * (1f - sweep); // 처음에 빠르게, 끝에서 감속
        float fade = t <= sweepTime ? 1f : 1f - Mathf.Clamp01((t - sweepTime) / Mathf.Max(0.01f, fadeTime));

        float from = -halfAngle * direction;
        float to = Mathf.Lerp(from, halfAngle * direction, sweep);
        float mid = Mathf.Lerp(inner, outer, 0.6f);
        Color rim = new Color(color.r * RimDarken, color.g * RimDarken, color.b * RimDarken, 1f);

        for (int i = 0; i <= Segments; i++)
        {
            float along = (float)i / Segments; // 0 = 지나간 꼬리, 1 = 휘두르는 끝
            float ang = Mathf.Lerp(from, to, along) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            float a = color.a * fade * Mathf.Lerp(0.35f, 1f, along); // 꼬리도 완전히 비지 않게

            int v = i * Rings;
            verts[v] = d * inner;
            verts[v + 1] = d * mid;
            verts[v + 2] = d * outer;
            verts[v + 3] = d * (outer + RimWidth);
            colors[v] = new Color(color.r, color.g, color.b, a * 0.2f);
            colors[v + 1] = new Color(color.r, color.g, color.b, a * 0.75f);
            colors[v + 2] = new Color(rim.r, rim.g, rim.b, a);
            colors[v + 3] = new Color(rim.r, rim.g, rim.b, a);
        }
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (mat != null) Destroy(mat);
    }
}
