using System.Collections.Generic;
using UnityEngine;

// 구획 결계. 켜면 구획 테두리를 따라 주황 반투명 벽(룬 두 줄이 서로 반대로 흐른다)과
// 꼭짓점 수정 기둥을 세우고, 벽마다 높은 상자 충돌로 밖으로 못 나가게 막는다
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

    static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

    GameObject root;
    readonly List<Renderer> runes = new List<Renderer>();
    readonly List<Vector2> runeTiling = new List<Vector2>(); // x = 가로 반복 수, y = 흐르는 방향(±1)
    MaterialPropertyBlock block;

    public bool Raised { get { return root != null && root.activeSelf; } }

    public void SetRaised(bool on)
    {
        if (on && root == null) Build();
        if (root != null) root.SetActive(on);
    }

    void Update()
    {
        if (!Raised) return;
        float t = Time.time * runeScrollSpeed;
        for (int i = 0; i < runes.Count; i++)
        {
            runes[i].GetPropertyBlock(block);
            block.SetVector(BaseMapSt, new Vector4(runeTiling[i].x, 1f, t * runeTiling[i].y, 0f));
            runes[i].SetPropertyBlock(block);
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
                    Instantiate(pillarPrefab, new Vector3(corners[i].x, GroundY(corners[i]), corners[i].z), Quaternion.identity, root.transform);

        for (int e = 0; e < 4; e++)
        {
            if (!wall[e]) continue;
            Vector3 a = corners[e], b = corners[(e + 1) % 4];
            Vector3 dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up)); // 벽 면이 구획 안팎을 향한다
            Vector3 mid = (a + b) * 0.5f;

            Quad(mid + Vector3.up * height * 0.5f, rot, len, height, glowMaterial);
            for (int i = 0; i < runeRows.Length; i++)
            {
                Renderer r = Quad(mid + Vector3.up * runeRows[i], rot, len, runeRowHeight, runeMaterial);
                runes.Add(r);
                runeTiling.Add(new Vector2(len / (runeRowHeight * 8f), i % 2 == 0 ? 1f : -1f));
            }

            BoxCollider col = new GameObject("Wall").AddComponent<BoxCollider>();
            col.transform.SetParent(root.transform, false);
            col.transform.SetPositionAndRotation(mid + Vector3.up * 4f, rot);
            col.size = new Vector3(len + colliderThickness, 8f, colliderThickness);
        }
    }

    Renderer Quad(Vector3 pos, Quaternion rot, float w, float h, Material mat)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root.transform, false);
        q.transform.SetPositionAndRotation(pos, rot);
        q.transform.localScale = new Vector3(w, h, 1f);
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
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
