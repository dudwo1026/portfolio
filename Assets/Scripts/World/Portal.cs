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

    [Header("진입 확인")]
    public GameObject confirmPanel;                  // 넣어 두면 닿았을 때 바로 보내지 않고 예/아니오를 묻는다
    public UnityEngine.Rendering.Volume blurVolume;  // 묻는 동안 weight 1 (약한 가우시안 피사계심도)
    Transform pending;

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

        if (confirmPanel != null)
        {
            // 바로 보내지 않고 묻는다. 멈춘 동안 화면은 흐리게
            pending = other.transform;
            confirmPanel.SetActive(true);
            if (blurVolume != null) blurVolume.weight = 1f;
            Time.timeScale = 0f;
            return;
        }
        Teleport(other.transform);
    }

    // 확인창 버튼에서 부른다
    public void ConfirmYes()
    {
        CloseConfirm();
        if (pending != null) Teleport(pending);
        pending = null;
    }

    // 아니오: 그 자리에 남는다. 다시 묻게 하려면 포탈에서 나갔다 들어와야 한다
    public void ConfirmNo()
    {
        CloseConfirm();
        pending = null;
    }

    void CloseConfirm()
    {
        confirmPanel.SetActive(false);
        if (blurVolume != null) blurVolume.weight = 0f;
        Time.timeScale = 1f;
    }

    void Teleport(Transform player)
    {
        Vector3 dir = destinationPortal != null ? destinationPortal.exitDirection : destination.forward;
        player.position = destination.position + dir.normalized * 2f;

        readyTime = Time.time + cooldown;
        if (destinationPortal != null)
            destinationPortal.readyTime = Time.time + cooldown;
    }
}
