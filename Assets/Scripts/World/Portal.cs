using UnityEngine;

public class Portal : MonoBehaviour
{
    public Transform destination;
    public Portal destinationPortal;
    public Vector3 exitDirection = Vector3.forward;
    public float cooldown = 0.5f;

    [Header("개방 조건")]
    public bool requireGaugeFull = false;   // 처치 게이지가 가득 차야 열린다
    public bool requireKeys = false;        // 열쇠(KeyItemSpawner)를 다 모아야 열린다
    public Renderer visual;                 // 열림/닫힘을 색으로 보여줄 렌더러
    public Color closedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    public Color openColor = new Color(0.3f, 0.85f, 1f, 1f);
    public float openPulseSpeed = 2.5f;
    public HudMessage openMessage;          // 닫혀 있다가 열리는 순간 한 번 띄운다
    public string openText = "옥상으로 향하는 문이 열렸습니다";

    float readyTime = 0f;
    Material runtimeMat;
    bool wasOpen;

    void Start()
    {
        wasOpen = IsOpen; // 처음부터 열려 있는 포탈은 알리지 않는다
    }

    public bool IsOpen
    {
        get
        {
            if (requireGaugeFull && !(KillGauge.Instance != null && KillGauge.Instance.IsFull)) return false;
            if (requireKeys && !(KeyItemSpawner.Instance != null && KeyItemSpawner.Instance.HasAllKeys)) return false;
            return true;
        }
    }

    void Update()
    {
        bool open = IsOpen;
        if (open && !wasOpen && openMessage != null) openMessage.Show(openText);
        wasOpen = open;

        if (visual == null) return;
        if (runtimeMat == null) runtimeMat = visual.material; // 이 포탈만의 인스턴스 재질

        if (!IsOpen)
        {
            runtimeMat.color = closedColor;
            return;
        }

        float t = (Mathf.Sin(Time.time * openPulseSpeed) + 1f) * 0.5f;
        runtimeMat.color = Color.Lerp(openColor * 0.6f, openColor, t); // 열리면 맥동
    }

void OnTriggerEnter(Collider other)
    {
        if (destination == null) return;
        if (!IsOpen) return;
        if (other.GetComponent<PlayerCombat>() == null) return; // players only - enemies can't use portals
        if (Time.time < readyTime) return;

        Vector3 dir = destinationPortal != null ? destinationPortal.exitDirection : destination.forward;
        other.transform.position = destination.position + dir.normalized * 2f;

        readyTime = Time.time + cooldown;
        if (destinationPortal != null)
            destinationPortal.readyTime = Time.time + cooldown;
    }
}
