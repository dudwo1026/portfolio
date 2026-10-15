using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 구획 결계. 켜면 꼭짓점 수정 기둥이 하늘에서 하나씩 떨어져 꽂히고(먼지), 그다음 구획 테두리를 따라
// 주황 반투명 벽(룬 두 줄이 서로 반대로 흐른다)이 서서히 나타난다. 막는 충돌 벽은 켜는 순간부터 있다
[RequireComponent(typeof(CombatZone))]
public class ZoneBarrier : MonoBehaviour
{
    public Material glowMaterial;   // 아래가 진한 주황 그라데이션
    public Material runeMaterial;   // 룬 띠 (가로로 반복)
    public GameObject pillarPrefab; // 꼭짓점 기둥
    public float height = 6f;       // 벽 높이 (y=0부터. 광장 바닥이 1이라 보이는 건 약 5)
    public float[] runeRows = { 2.4f, 4.4f }; // 룬 줄 가운데 높이. 줄마다 흐르는 방향이 번갈아 바뀐다
    public float runeRowHeight = 1.2f;
    public float runeScrollSpeed = 0.12f;
    public float colliderThickness = 1f;
    // 맵 테두리 (x 최소·최대, z 최소·최대). 이 위에 놓인 변은 맵 벽이 이미 막으니 결계를 세우지 않는다
    public Vector4 mapEdges = new Vector4(-90f, 80f, 20f, 180f);

    [Header("등장 연출")]
    public float pillarDropHeight = 14f;
    public float pillarDropTime = 0.3f;
    public float pillarStagger = 0.12f;  // 기둥끼리 떨어지는 간격
    public GameObject dustPrefab;        // 기둥이 꽂힌 자리
    public float dustScale = 1f;
    public float fadeDelay = 1f;         // 마지막 기둥이 꽂히고 벽이 나타나기 시작할 때까지
    public float fadeTime = 0.6f;        // 벽이 나타나는 시간 (사라질 때도)

    static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    GameObject root;
    readonly List<Renderer> quads = new List<Renderer>();
    readonly List<Vector2> quadTiling = new List<Vector2>(); // x = 가로 반복 수, y = 흐르는 방향(±1, 0 = 안 흐름)
    readonly List<Transform> pillars = new List<Transform>();
    readonly List<Collider> walls = new List<Collider>();
    MaterialPropertyBlock block;
    float alpha;

    public bool Raised { get; private set; }
    // 기둥이 다 꽂히고 벽이 다 나타나기까지 걸리는 시간
    public float RaiseDuration { get { return pillarStagger * Mathf.Max(0, pillars.Count - 1) + pillarDropTime + fadeDelay + fadeTime; } }

    public void SetRaised(bool on)
    {
        if (on == Raised) return;
        Raised = on;
        if (on && root == null) Build();
        if (root == null) return;
        StopAllCoroutines();
        foreach (Collider c in walls) c.enabled = on; // 막는 건 바로
        StartCoroutine(on ? RaiseRoutine() : LowerRoutine());
    }

    IEnumerator RaiseRoutine()
    {
        root.SetActive(true);
        alpha = 0f;
        Vector3[] ends = new Vector3[pillars.Count];
        for (int i = 0; i < pillars.Count; i++)
        {
            ends[i] = pillars[i].localPosition;
            pillars[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < pillars.Count; i++)
        {
            StartCoroutine(Drop(pillars[i], ends[i]));
            if (i < pillars.Count - 1) yield return new WaitForSeconds(pillarStagger);
        }
        yield return new WaitForSeconds(pillarDropTime + fadeDelay); // 마지막 기둥이 꽂히고 fadeDelay 뒤
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            alpha = t / fadeTime;
            yield return null;
        }
        alpha = 1f;
    }

    IEnumerator Drop(Transform pillar, Vector3 end)
    {
        pillar.gameObject.SetActive(true);
        Vector3 start = end + Vector3.up * pillarDropHeight;
        for (float t = 0f; t < pillarDropTime; t += Time.deltaTime)
        {
            float k = t / pillarDropTime;
            pillar.localPosition = Vector3.Lerp(start, end, k * k); // 가속하며 떨어진다
            yield return null;
        }
        pillar.localPosition = end;
        if (dustPrefab != null)
        {
            GameObject fx = Instantiate(dustPrefab, pillar.position, dustPrefab.transform.rotation);
            fx.transform.localScale = Vector3.one * dustScale;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule m = ps.main;
                m.loop = false; // Dust puff는 반복 재생이라 그냥 두면 2초 뒤에 한 번 더 인다
            }
            Destroy(fx, 3f);
        }
    }

    IEnumerator LowerRoutine()
    {
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            alpha = Mathf.Min(alpha, 1f - t / fadeTime);
            yield return null;
        }
        root.SetActive(false);
    }

    void Update()
    {
        if (root == null || !root.activeSelf) return;
        float t = Time.time * runeScrollSpeed;
        Color c = new Color(1.6f, 1.6f, 1.6f, alpha);
        for (int i = 0; i < quads.Count; i++)
        {
            quads[i].GetPropertyBlock(block);
            block.SetVector(BaseMapSt, new Vector4(quadTiling[i].x, 1f, t * quadTiling[i].y, 0f));
            block.SetColor(BaseColor, c);
            quads[i].SetPropertyBlock(block);
        }
    }

    void Build()
    {
        block = new MaterialPropertyBlock();
        CombatZone zone = GetComponent<CombatZone>();
        Vector3 c = transform.position;
        float hx = zone.size.x * 0.5f, hz = zone.size.y * 0.5f;
        Vector3[] corners =
        {
            new Vector3(c.x - hx, 0f, c.z - hz), new Vector3(c.x + hx, 0f, c.z - hz),
            new Vector3(c.x + hx, 0f, c.z + hz), new Vector3(c.x - hx, 0f, c.z + hz),
        };

        root = new GameObject("Barrier");
        root.transform.SetParent(transform, false);

        bool[] wall = new bool[4]; // 변 e = corners[e] → corners[e+1]
        for (int e = 0; e < 4; e++) wall[e] = !OnMapEdge(corners[e], corners[(e + 1) % 4]);

        // 기둥은 결계 변이 닿는 꼭짓점에만. 벽 충돌보다 먼저 세운다 (바닥을 찾는 광선이 걸리지 않게)
        if (pillarPrefab != null)
            for (int i = 0; i < 4; i++)
                if (wall[i] || wall[(i + 3) % 4])
                    pillars.Add(Instantiate(pillarPrefab, new Vector3(corners[i].x, GroundY(corners[i]), corners[i].z), Quaternion.identity, root.transform).transform);

        for (int e = 0; e < 4; e++)
        {
            if (!wall[e]) continue;
            Vector3 a = corners[e], b = corners[(e + 1) % 4];
            Vector3 dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up)); // 벽 면이 구획 안팎을 향한다
            Vector3 mid = (a + b) * 0.5f;

            Quad(mid + Vector3.up * height * 0.5f, rot, len, height, glowMaterial, new Vector2(1f, 0f));
            for (int i = 0; i < runeRows.Length; i++)
                Quad(mid + Vector3.up * runeRows[i], rot, len, runeRowHeight, runeMaterial,
                     new Vector2(len / (runeRowHeight * 8f), i % 2 == 0 ? 1f : -1f));

            BoxCollider col = new GameObject("Wall").AddComponent<BoxCollider>();
            col.transform.SetParent(root.transform, false);
            col.transform.SetPositionAndRotation(mid + Vector3.up * 4f, rot);
            col.size = new Vector3(len + colliderThickness, 8f, colliderThickness);
            walls.Add(col);
        }
    }

    void Quad(Vector3 pos, Quaternion rot, float w, float h, Material mat, Vector2 tiling)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root.transform, false);
        q.transform.SetPositionAndRotation(pos, rot);
        q.transform.localScale = new Vector3(w, h, 1f);
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quads.Add(r);
        quadTiling.Add(tiling);
    }

    bool OnMapEdge(Vector3 a, Vector3 b)
    {
        const float eps = 0.5f;
        if (Mathf.Abs(a.x - b.x) < eps) return Mathf.Abs(a.x - mapEdges.x) < eps || Mathf.Abs(a.x - mapEdges.y) < eps;
        return Mathf.Abs(a.z - mapEdges.z) < eps || Mathf.Abs(a.z - mapEdges.w) < eps;
    }

    // 꼭짓점 바닥 높이. 못 찾으면 구획 높이
    float GroundY(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }
}
