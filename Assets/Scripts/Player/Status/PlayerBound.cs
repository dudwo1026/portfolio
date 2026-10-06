using UnityEngine;

// 구속: no movement, no dash, no ranged attacks. Melee is still allowed and breaks the bind
// (PlayerCombat calls Release() when a melee swing goes off).
public class PlayerBound : MonoBehaviour
{
    public bool IsActive { get; private set; }
    public event System.Action Released;

    TopDownMove mover;
    PlayerDash dash;
    Health health;

    void Awake()
    {
        mover = GetComponent<TopDownMove>();
        dash = GetComponent<PlayerDash>();
        health = GetComponent<Health>();
    }

    public void Bind()
    {
        if (IsActive) return;
        IsActive = true;

        if (dash != null)
        {
            dash.CancelDash(); // a disabled dash would otherwise resume after release
            dash.enabled = false;
        }
        if (mover != null) mover.movementLocked = true;
    }

    public void Release()
    {
        if (!IsActive) return;
        IsActive = false;

        if (dash != null) dash.enabled = true;
        if (mover != null) mover.movementLocked = false;

        if (Released != null) Released();
    }

    void Update()
    {
        // safety: a dead player can never melee out, which would freeze the grabber forever
        if (IsActive && health != null && health.CurrentHealth <= 0f) Release();
    }
}
