using UnityEngine;
using UnityEngine.InputSystem;

// 열린 포탈 가까이(interactRadius)에서 E를 holdTime 동안 누르고 있으면 들어간다 (보급 장판과 같은 방식).
// 진행도는 IChargePad로 내보내 장판 캐스팅바를 같이 쓴다. 떼거나 멀어지면 0부터
public class Portal : MonoBehaviour, IChargePad
{
    public float interactRadius = 2.5f;  // 포탈 면에서 이 거리 안
    public float holdTime = 0.5f;
    float holdProgress;
    Transform player;

    public float Progress { get { return holdTime > 0f ? Mathf.Clamp01(holdProgress / holdTime) : 0f; } }
    public Color BarColor { get { return openColor; } }

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
        UpdateHold(open);

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

    void UpdateHold(bool open)
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            player = p.transform;
        }

        bool held = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        if (!open || destination == null || !held || Time.time < readyTime || !InReach())
        {
            holdProgress = 0f;
            return;
        }

        holdProgress += Time.deltaTime;
        if (holdProgress < holdTime) return;
        holdProgress = 0f;
        Enter(player);
    }

    // 포탈은 얇고 넓은 판이라 중심이 아니라 판 표면에서 잰다
    bool InReach()
    {
        Collider c = GetComponent<Collider>();
        Vector3 nearest = c != null ? c.bounds.ClosestPoint(player.position) : transform.position;
        Vector3 d = player.position - nearest;
        d.y = 0f;
        return d.sqrMagnitude <= interactRadius * interactRadius;
    }

    void Enter(Transform who)
    {
        if (confirmPanel != null)
        {
            // 바로 보내지 않고 묻는다. 멈춘 동안 화면은 흐리게
            pending = who;
            confirmPanel.SetActive(true);
            if (blurVolume != null) blurVolume.weight = 1f;
            Time.timeScale = 0f;
            return;
        }
        Teleport(who);
    }

    // 확인창 버튼에서 부른다
    public void ConfirmYes()
    {
        CloseConfirm();
        if (pending != null) Teleport(pending);
        pending = null;
    }

    // 아니오: 그 자리에 남는다. 다시 들어가려면 E를 다시 누르고 있으면 된다
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
