using UnityEngine;

// 허약: 대시(점프) 봉인. 이동속도는 건드리지 않는다 — 감속은 PlayerSlow가 맡는다.
public class PlayerWeaken : MonoBehaviour
{
    public float duration = 3f;

    public bool IsActive => timer > 0f;

    float timer;
    PlayerDash dash;
    PlayerBound bound;

    void Awake()
    {
        dash = GetComponent<PlayerDash>();
        bound = GetComponent<PlayerBound>();
    }

    public void Apply()
    {
        Apply(duration);
    }

    public void Apply(float seconds)
    {
        if (seconds <= 0f) return;
        if (seconds > timer) timer = seconds; // 갱신은 하되 줄이지는 않는다

        if (dash != null)
        {
            dash.CancelDash(); // 꺼두기만 하면 남은 대시가 나중에 되살아난다
            dash.enabled = false;
        }
    }

    void Update()
    {
        if (timer <= 0f) return;

        // 구속 해제·공중 복귀가 대시를 다시 켜므로 허약이 끝날 때까지 계속 덮어쓴다
        if (dash != null && dash.enabled) { dash.CancelDash(); dash.enabled = false; }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = 0f;
        if (dash != null && (bound == null || !bound.IsActive)) dash.enabled = true;
    }
}
