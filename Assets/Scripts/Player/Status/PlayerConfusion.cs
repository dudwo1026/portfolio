using UnityEngine;

// 혼란: the movement keys are inverted for a while.
public class PlayerConfusion : MonoBehaviour
{
    public float duration = 8f;

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
        if (seconds > timer) timer = seconds; // refresh, never shorten
        if (mover != null) mover.inputInverted = true;
    }

    void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = 0f;
        if (mover != null) mover.inputInverted = false;
    }
}
