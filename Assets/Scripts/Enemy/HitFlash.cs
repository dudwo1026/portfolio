using UnityEngine;

// 맞으면 잠깐 하얗게 번쩍인다. 적중했는지 알아볼 정도로만 — 연사·레이저는 초당 8번씩 맞으므로
// 너무 밝으면 화면이 계속 깜빡여 눈이 아프다.
//
// EnemyAI가 Awake에서 붙인다 (모든 몹·정예·보스).
//  - 스프라이트(EnemySpriteFlash 셰이더): _FlashAmount로 하얀색을 섞는다
//  - 그 밖의 메쉬(보스 큐브, URP Lit): 발광(_EmissionColor)을 더한다.
//    보스는 방어 모드에서 _BaseColor를 바꿔 칠하므로 그쪽은 건드리지 않는다
public class HitFlash : MonoBehaviour
{
    public float peak = 0.3f;       // 맞은 순간 하얀색을 섞는 비율 (0~1)
    public float duration = 0.1f;   // 이 시간에 걸쳐 0으로 돌아온다
    public Color color = Color.white;
    public float meshEmission = 0.6f; // 메쉬는 발광이라 같은 비율이면 약해 보인다. 여기에 곱한다

    Health health;
    Renderer sprite;
    Renderer mesh;
    Material ownMaterial;
    MaterialPropertyBlock block;
    float timeLeft;
    bool shown; // 마지막에 0이 아닌 값을 넣었는지. 0으로 한 번 되돌려 놓아야 끝난다

    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        health = GetComponent<Health>();
        block = new MaterialPropertyBlock();

        EnemySpriteAnimator anim = GetComponentInChildren<EnemySpriteAnimator>(true);
        if (anim != null)
        {
            Renderer r = anim.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(FlashAmountId)) sprite = r;
        }

        // 스프라이트가 없는 몹(보스)만 자기 메쉬를 쓴다. 정예의 몸체 메쉬는 꺼져 있다.
        // URP Lit은 _EMISSION 키워드가 켜져야 발광을 그리는데, 공용 재질에 켜 두면 발광색이 검정이라는
        // 이유로 URP가 도로 꺼 버린다. 이 몹만의 재질 사본에서 켠다 (보스는 한 마리라 부담 없다)
        if (sprite == null)
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null && r.enabled && r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionId))
            {
                mesh = r;
                ownMaterial = r.material; // 사본
                ownMaterial.EnableKeyword("_EMISSION");
                ownMaterial.SetColor(EmissionId, Color.black);
            }
        }
    }

    void OnDestroy()
    {
        if (ownMaterial != null) Destroy(ownMaterial);
    }

    void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void OnDamaged()
    {
        timeLeft = duration;
    }

    void LateUpdate()
    {
        if (timeLeft <= 0f && !shown) return;

        timeLeft -= Time.deltaTime;
        float a = timeLeft > 0f && duration > 0f ? peak * (timeLeft / duration) : 0f;
        Apply(a);
        shown = a > 0f;
    }

    void Apply(float a)
    {
        // 다른 스크립트(EnemySpriteAnimator의 텍스처, 보스 방어 색)가 넣은 값을 지우지 않도록 읽어서 고친다
        if (sprite != null)
        {
            sprite.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, a);
            block.SetColor(FlashColorId, color);
            sprite.SetPropertyBlock(block);
        }
        if (mesh != null)
        {
            mesh.GetPropertyBlock(block);
            block.SetColor(EmissionId, color * (a * meshEmission));
            mesh.SetPropertyBlock(block);
        }
    }
}
