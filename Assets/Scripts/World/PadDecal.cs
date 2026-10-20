using UnityEngine;

// 장판(원기둥) 위에 문양 그림을 깔고 원기둥은 숨긴다. 문양은 천천히 돌 수 있고, 위에 이펙트를 하나 얹을 수 있다.
// 판정·스크립트는 원래 장판 그대로 쓴다. 장판이 색으로 진행도를 보여 주면(ResupplyPad) 이 문양을 칠한다
public class PadDecal : MonoBehaviour
{
    public Texture2D texture;
    public Color color = Color.white;
    public float spinSpeed = 0f;     // 도/초
    public GameObject effect;        // 장판 가운데에 얹는 이펙트 (없어도 된다)
    public float effectScale = 1f;

    public Renderer Decal { get; private set; }
    Transform spin;

    void Awake()
    {
        Renderer baseRend = GetComponent<Renderer>();
        if (baseRend != null) baseRend.enabled = false;

        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Decal";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.localPosition = new Vector3(0f, 1.05f, 0f); // 원기둥 윗면 바로 위 (원기둥 높이 2, 가운데 기준)
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.mainTexture = texture;
        m.color = color;
        Decal = q.GetComponent<Renderer>();
        Decal.sharedMaterial = m;
        Decal.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spin = q.transform;

        if (effect != null)
        {
            GameObject fx = Instantiate(effect, transform.position + Vector3.up * 0.05f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * effectScale;
        }
    }

    void Update()
    {
        if (spinSpeed != 0f) spin.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self); // 눕힌 판이라 자기 z축이 위쪽
    }
}
