using UnityEngine;

public class PlayerAirborne : MonoBehaviour
{
    public float riseFallDuration = 1f;  // launch -> back on the ground
    public float groundedDuration = 1f;  // still unable to act after landing
    public float peakHeight = 1.5f;

    public bool IsActive => timer > 0f;
    public bool InAir => timer > groundedDuration; // 뜨고 내려오는 중 (그 뒤는 착지 후 행동불가)

    float timer;
    float total;
    Vector3 lockedPos;
    float groundY;

    Rigidbody rb;
    TopDownMove mover;
    PlayerCombat combat;
    PlayerDash dash;
    PlayerConsumables consumables;
    PlayerBound bound;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mover = GetComponent<TopDownMove>();
        combat = GetComponent<PlayerCombat>();
        dash = GetComponent<PlayerDash>();
        consumables = GetComponent<PlayerConsumables>();
        bound = GetComponent<PlayerBound>();
    }

    public void Launch()
    {
        if (IsActive) return;

        total = riseFallDuration + groundedDuration;
        timer = total;
        lockedPos = rb.position;
        groundY = rb.position.y;

        // cancel first: a disabled PlayerDash keeps its remaining time and would resume after the airborne
        if (dash != null) dash.CancelDash();

        // disabling the action scripts is the simplest full "행동불가": no move / attack / dash / item
        if (mover != null) mover.movementLocked = true;
        if (combat != null) combat.enabled = false;
        if (dash != null) dash.enabled = false;
        if (consumables != null) consumables.enabled = false;
        rb.linearVelocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (timer <= 0f) return;

        float elapsed = total - timer;
        float y = groundY;
        if (elapsed < riseFallDuration)
        {
            // parabola: leaves the ground at t=0, peaks at t=0.5, lands exactly at t=1
            float t = elapsed / riseFallDuration;
            y = groundY + peakHeight * 4f * t * (1f - t);
        }

        rb.MovePosition(new Vector3(lockedPos.x, y, lockedPos.z));
        rb.linearVelocity = Vector3.zero;

        timer -= Time.fixedDeltaTime;
        if (timer <= 0f) End();
    }

void End()
    {
        timer = 0f;
        rb.position = new Vector3(lockedPos.x, groundY, lockedPos.z);
        rb.linearVelocity = Vector3.zero;

        bool stillBound = bound != null && bound.IsActive; // don't hand control back while 구속 is up
        if (mover != null) mover.movementLocked = stillBound;
        if (combat != null) combat.enabled = true;
        if (dash != null) dash.enabled = !stillBound;
        if (consumables != null) consumables.enabled = true;
    }
}
