using UnityEngine;
using UnityEngine.InputSystem;

// 결계로 잠기는 전투 구획 하나. 범위는 이 오브젝트 위치를 중심으로 가로(x) size.x, 세로(z) size.y.
// portalPosition의 포탈에서 E를 holdTime 동안 누르고 있으면 구획 안쪽으로 들어가고 결계(ZoneBarrier)가 선다.
// 클리어하면 결계가 풀리고 포탈은 다시 열리지 않는다
public class CombatZone : MonoBehaviour, IChargePad
{
    public enum State { Idle, Active, Cleared }

    public Vector2 size = new Vector2(80f, 50f);
    public int keyReward = 1; // 클리어하면 떨어지는 열쇠 수
    public Vector3 portalPosition; // 이 구획으로 들어가는 포탈 자리 (월드)

    [Header("포탈")]
    public GameObject portalPrefab;
    public float portalScale = 2.4f;
    public float interactRadius = 2.5f;
    public float holdTime = 0.5f;
    public float enterDepth = 4f; // 들어가면 포탈에서 구획 안쪽으로 이만큼 들어간 자리에 선다
    public Color barColor = new Color(1f, 0.6f, 0.15f, 1f);
    public bool debugKeys = true; // \ : 들어가 있는 구획을 클리어 처리 (테스트용)

    public static CombatZone Current { get; private set; } // 지금 갇혀 있는 구획

    public State Status { get; private set; }

    Transform player;
    GameObject portal;
    float holdProgress;

    public float Progress { get { return holdTime > 0f ? Mathf.Clamp01(holdProgress / holdTime) : 0f; } }
    public Color BarColor { get { return barColor; } }
    public string Prompt { get { return CanEnter() ? ChargeCastBarUI.HoldPrompt("구획 진입") : null; } }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        if (portalPrefab != null) portal = SpawnPortal();
    }

    void Update()
    {
        if (debugKeys && Current == this && Keyboard.current != null && Keyboard.current.backslashKey.wasPressedThisFrame)
        {
            Clear();
            return;
        }

        bool held = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        if (!held || !CanEnter())
        {
            holdProgress = 0f;
            return;
        }
        holdProgress += Time.deltaTime;
        if (holdProgress >= holdTime) Enter();
    }

    bool CanEnter()
    {
        if (Status != State.Idle || Current != null || player == null) return false;
        Vector3 d = player.position - portalPosition;
        d.y = 0f;
        return d.sqrMagnitude <= interactRadius * interactRadius;
    }

    void Enter()
    {
        holdProgress = 0f;
        Status = State.Active;
        Current = this;
        player.position = portalPosition + Inward() * enterDepth;
        if (portal != null) portal.SetActive(false);
        ZoneBarrier barrier = GetComponent<ZoneBarrier>();
        if (barrier != null) barrier.SetRaised(true);
    }

    public void Clear()
    {
        Status = State.Cleared;
        if (Current == this) Current = null;
        ZoneBarrier barrier = GetComponent<ZoneBarrier>();
        if (barrier != null) barrier.SetRaised(false);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null; // static이라 씬을 다시 불러도 남는다
    }

    // 포탈에서 가장 가까운 구획 변의 안쪽 방향
    Vector3 Inward()
    {
        Vector3 d = portalPosition - transform.position;
        float ex = size.x * 0.5f - Mathf.Abs(d.x), ez = size.y * 0.5f - Mathf.Abs(d.z); // 각 변까지 남은 거리
        return ex < ez ? new Vector3(-Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(d.z));
    }

    // 고리가 구획 안쪽을 바라보게 세우고, 아래 끝이 바닥 위 0.1에 오게 띄운다
    GameObject SpawnPortal()
    {
        GameObject go = Instantiate(portalPrefab, portalPosition, Quaternion.LookRotation(Inward()), transform);
        go.transform.localScale = Vector3.one * portalScale;
        float ground = portalPosition.y;
        RaycastHit hit;
        if (Physics.Raycast(portalPosition + Vector3.up * 5f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
        go.transform.position = new Vector3(portalPosition.x, ground + 0.96f * portalScale + 0.1f, portalPosition.z); // 고리 반지름 ≈ 0.96
        return go;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.2f, size.y));
        Gizmos.DrawWireSphere(portalPosition, 1.5f);
    }
}
