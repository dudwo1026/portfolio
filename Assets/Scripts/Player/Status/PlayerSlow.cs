using UnityEngine;

// 슬로우: 이동속도 감소. 대시는 그대로 쓸 수 있다.
public class PlayerSlow : MonoBehaviour
{
    public float duration = 3f;
    public float speedMultiplier = 0.5f;

    public bool IsActive => timer > 0f;

    float timer;
    TopDownMove mover;

    void Awake()
    {
        mover = GetComponent<TopDownMove>();
    }

    public void Apply()
    {
        Apply(duration);
    }

    public void Apply(float seconds)
    {
        if (seconds <= 0f) return;
        if (seconds > timer) timer = seconds;

        if (mover != null) mover.speedMultiplier = speedMultiplier;
    }

    void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = 0f;
        if (mover != null) mover.speedMultiplier = 1f;
    }
}
