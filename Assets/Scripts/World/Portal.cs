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
    public string Prompt { get { return player != null && IsOpen && destination != null && InReach() ? ChargeCastBarUI.HoldPrompt("옥상 진입") : null; } }

    public Transform destination;
    public Portal destinationPortal;
    public Vector3 exitDirection = Vector3.forward;
    public float cooldown = 0.5f;

    [Header("개방 조건")]
    public bool requireKeys = false;        // 열쇠(KeyItemSpawner)를 다 모아야 열린다
    public Renderer visual;                 // 열림/닫힘을 색으로 보여줄 렌더러
    public GameObject openEffect;           // 넣어 두면 열렸을 때 이걸 켜고 visual 판은 숨긴다 (포탈 이펙트)
    public Color closedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    public Color openColor = new Color(0.3f, 0.85f, 1f, 1f);
    public float openPulseSpeed = 2.5f;
    public HudMessage openMessage;          // 닫혀 있다가 열리는 순간 한 번 띄운다
    public string openText = "옥상으로 향하는 문이 열렸습니다";

    [Header("진입 확인")]
    public GameObject confirmPanel;                  // 넣어 두면 닿았을 때 바로 보내지 않고 예/아니오를 묻는다
    public UnityEngine.Rendering.Volume blurVolume;  // 묻는 동안 weight 1 (약한 가우시안 피사계심도)
    Transform pending;

    [Header("워프 연출 (WarpBeam)")]
    public bool warpBeam = true;
    public float stretchTime = 0.45f;  // 캐릭터가 가늘고 길게 늘어나며 빛기둥으로 솟는 시간 (그 뒤 이동)
    public float landTime = 0.35f;     // 도착해서 납작했다가 원래대로 튕겨 돌아오는 시간
    public float fadeTime = 0.4f;      // 화면이 검게 닫히는 / 다시 열리는 시간
    public float blackHoldTime = 0.2f; // 검은 화면으로 멈춰 있는 시간
    public GameObject departAura;     // 늘어나는 동안 캐릭터를 감는 이펙트 (얼음 회오리)
    public GameObject arriveBurst;     // 착지 순간 터지는 이펙트 (파란 임팩트)
    bool warping;
    // 워프 연출(도착해서 원래 모습으로 돌아올 때까지) 중인지. 보스 등장 연출이 이게 끝나길 기다린다
    public static bool Warping { get; private set; }

    float readyTime = 0f;
    Material runtimeMat;
    bool wasOpen;

    void Start()
    {
        Warping = false; // static이라 플레이 세션 사이에 남을 수 있다
        wasOpen = IsOpen; // 처음부터 열려 있는 포탈은 알리지 않는다
    }

    public bool IsOpen
    {
        get
        {
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

        if (openEffect != null && openEffect.activeSelf != open)
        {
            openEffect.SetActive(open);
            if (visual != null) visual.enabled = !open;
        }

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
        if (warping) return;
        PlayerSpriteAnimator sprite = player.GetComponentInChildren<PlayerSpriteAnimator>();
        if (!warpBeam || sprite == null) { MovePlayer(player); return; }
        StartCoroutine(Warp(player, sprite));
    }

    // 출발: 제자리에 묶인 채 캐릭터가 가늘고 길게 늘어나며 빛기둥으로 솟고 사라진다 → 페이드아웃 → 이동 → 페이드인
    // 도착: 빛기둥이 굵게 꽂혔다 가늘어지고, 캐릭터는 납작했다가 튕기듯 원래대로
    System.Collections.IEnumerator Warp(Transform player, PlayerSpriteAnimator sprite)
    {
        warping = true;
        Warping = true;
        TopDownMove mover = player.GetComponent<TopDownMove>();
        bool wasLocked = mover != null && mover.movementLocked;
        if (mover != null) mover.movementLocked = true;

        WarpBeam.Spawn(Feet(player), stretchTime + 0.25f, 1.6f, 14f, false);
        SpawnFx(departAura, player.position, stretchTime + 0.1f); // 반복 재생 이펙트라 떠날 때 지운다
        for (float t = 0f; t < stretchTime; t += Time.deltaTime)
        {
            float k = t / stretchTime;
            k *= k; // 처음엔 천천히, 끝에 확 빨려 올라간다
            sprite.warpScale = new Vector2(Mathf.Lerp(1f, 0.05f, k), Mathf.Lerp(1f, 2.2f, k));
            yield return null;
        }

        // 캐릭터를 지우고 화면을 검게 닫았다가, 옥상으로 옮긴 뒤 다시 연다. 다 열린 다음에 도착 연출
        sprite.warpScale = Vector2.zero;
        UnityEngine.UI.Image fade = WarpBeam.ScreenOverlay(Color.black);
        for (float t = 0f; t < fadeTime; t += Time.deltaTime) { fade.color = new Color(0f, 0f, 0f, t / fadeTime); yield return null; }
        fade.color = Color.black;
        MovePlayer(player);
        yield return new WaitForSeconds(blackHoldTime); // 카메라가 옥상에 자리 잡을 시간
        for (float t = 0f; t < fadeTime; t += Time.deltaTime) { fade.color = new Color(0f, 0f, 0f, 1f - t / fadeTime); yield return null; }
        Destroy(fade.canvas.gameObject);

        if (mover != null) mover.movementLocked = wasLocked;
        WarpBeam.Spawn(Feet(player), 0.45f, 2.2f, 14f, true);
        SpawnFx(arriveBurst, player.position, 2f);

        for (float t = 0f; t < landTime; t += Time.deltaTime)
        {
            float k = t / landTime;
            float bounce = Mathf.Cos(k * Mathf.PI * 1.5f) * (1f - k); // 납작 → 살짝 길게 → 원래대로
            sprite.warpScale = new Vector2(1f + 0.45f * bounce, 1f - 0.45f * bounce);
            yield return null;
        }
        sprite.warpScale = Vector2.one;
        warping = false;
        Warping = false;
    }

    static void SpawnFx(GameObject prefab, Vector3 at, float life)
    {
        if (prefab != null) Destroy(Instantiate(prefab, at, Quaternion.identity), life);
    }

    // 플레이어 중심은 발에서 1 위다 (PlayerSpriteAnimator와 같은 기준)
    static Vector3 Feet(Transform player)
    {
        return player.position + Vector3.down;
    }

    void MovePlayer(Transform player)
    {
        Vector3 dir = destinationPortal != null ? destinationPortal.exitDirection : destination.forward;
        player.position = destination.position + dir.normalized * 2f;

        readyTime = Time.time + cooldown;
        if (destinationPortal != null)
            destinationPortal.readyTime = Time.time + cooldown;
    }
}
