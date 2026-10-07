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

    float readyTime = 0f;
    Material runtimeMat;

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
