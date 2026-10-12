using UnityEngine;
using UnityEngine.InputSystem;

// 보급 장판. 이 위에 서서 E를 chargeTime 동안 누르고 있으면 회복 소모품이 chargeAmount만큼 찬다.
// 장판에서 벗어나거나 키를 떼면 진행도는 0으로 돌아간다 — 끊어 누르며 야금야금 채울 수 없다.
//
// 콜라이더를 두지 않고 수평 거리로만 판정한다. 바닥에 깔린 얇은 원반에 콜라이더를 붙이면
// 이동·내비메시·돌진 판정에 다 걸린다
public class ResupplyPad : MonoBehaviour, IChargePad
{
    public float radius = 3f;
    public float chargeTime = 3f;

    [Header("색")]
    // 탄약 장판(AmmoPadMat)이 밝은 파랑이라 같은 채도의 초록으로 맞춘다.
    // 어두운 색은 바닥에 묻혀 보이지 않는다
    public Color idleColor = new Color(0.15f, 0.75f, 0.35f, 1f);     // 평소: 밝은 초록
    public Color chargingColor = new Color(0.70f, 1.00f, 0.55f, 1f); // 충전 중: 연두로 밝아진다
    public Color fullColor = new Color(0.38f, 0.40f, 0.42f, 1f);     // 가득 찼을 때: 회색(쓸 수 없음)

    public float Progress { get { return chargeTime > 0f ? Mathf.Clamp01(progress / chargeTime) : 0f; } }

    // 캐스팅바 색. 장판은 충전 중에 색이 밝아지므로, 기준이 되는 평소 색을 돌려준다
    public Color BarColor { get { return idleColor; } }
    public string Prompt { get { return player != null && OnPad() ? (usable ? "[E] 길게 눌러 회복약 보급" : "회복약이 가득 찼습니다") : null; } }
    bool usable;

    Transform player;
    PlayerConsumables consumables;
    Renderer rend;
    MaterialPropertyBlock block;
    float progress;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (player == null && !FindPlayer()) return;

        bool onPad = OnPad();
        // consumables는 기절·에어본 중에 꺼진다. 그 상태에서는 충전도 막는다
        usable = consumables != null && consumables.enabled && !consumables.IsFull;
        bool holding = Keyboard.current != null && Keyboard.current.eKey.isPressed;

        if (onPad && usable && holding)
        {
            progress += Time.deltaTime;
            if (progress >= chargeTime)
            {
                progress = 0f;
                consumables.Refill(); // 탄약 장판과 동일하게 한 번에 가득 채운다
            }
        }
        else
        {
            progress = 0f;
        }

        Tint(usable);
    }

    bool FindPlayer()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return false;
        player = p.transform;
        consumables = p.GetComponent<PlayerConsumables>();
        return true;
    }

    bool OnPad()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f; // 높이는 보지 않는다. 장판은 평지에 깔려 있다
        return d.sqrMagnitude <= radius * radius;
    }

    // 진행도를 색으로 보여 준다. 3초를 아무 표시 없이 누르고 있으면 되는지 알 수 없다
    void Tint(bool usable)
    {
        if (rend == null) return;

        Color c = usable ? Color.Lerp(idleColor, chargingColor, Progress) : fullColor;
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        rend.SetPropertyBlock(block); // 재질 인스턴스를 만들지 않아 에셋이 더러워지지 않는다
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.95f, 0.45f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
