using UnityEngine;

public class StunEffect : MonoBehaviour
{
    public int ballCount = 4;
    public float ballScale = 0.2f;   // same size as bullets
    public float orbitRadius = 0.5f;
    public float headGap = 0.35f;
    public float rotateSpeed = 240f; // degrees per second
    public Color ballColor = new Color(1f, 0.95f, 0.2f, 1f);

    EnemyAI ai;
    Transform pivot;
    float headOffset;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();

        // 몸 높이는 콜라이더에서 잰다. 스프라이트를 입힌 몹은 메시 렌더러를 꺼 두고
        // 1x1x1 큐브만 남겨 두기 때문에, 렌더러로 재면 머리 높이가 몸보다 한참 낮게 나온다.
        // 콜라이더는 어느 쪽이든 실제 몸 크기 그대로라 양쪽 모두에서 맞는다
        Collider c = GetComponent<Collider>();
        Renderer r = GetComponent<Renderer>();
        float halfHeight = c != null ? c.bounds.extents.y : (r != null ? r.bounds.extents.y : 1f);
        headOffset = halfHeight + headGap;
    }

    void Update()
    {
        bool stunned = ai != null && ai.IsStunned;

        if (pivot == null)
        {
            if (!stunned) return; // built lazily: costs nothing until something actually gets stunned
            Build();
        }

        if (pivot.gameObject.activeSelf != stunned) pivot.gameObject.SetActive(stunned);
        if (!stunned) return;

        pivot.position = transform.position + Vector3.up * headOffset;
        pivot.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
    }

    void Build()
    {
        // kept outside the enemy's hierarchy so the parent's (often non-uniform) scale doesn't distort it
        GameObject pivotGO = new GameObject(name + "_StunEffect");
        pivot = pivotGO.transform;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = ballColor;

        for (int i = 0; i < ballCount; i++)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "StunBall_" + i;
            Destroy(ball.GetComponent<Collider>()); // visual only
            ball.transform.SetParent(pivot, false);

            float angle = 360f / ballCount * i;
            ball.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius;
            ball.transform.localScale = Vector3.one * ballScale;
            ball.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    void OnDestroy()
    {
        if (pivot != null) Destroy(pivot.gameObject);
    }
}
