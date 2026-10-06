using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerConsumables : MonoBehaviour
{
    public string itemName = "Health Potion";
    public float healPercent = 50f; // 최대 체력 대비 %
    public int count = 3;
    public int maxCount = 3;        // 보급 장판에서 여기까지만 충전된다

    public bool IsFull { get { return count >= maxCount; } }

    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        count = Mathf.Clamp(count, 0, maxCount);
    }

    // 보급 장판에서 한 번에 가득 채운다 (PlayerCombat.RefillAmmo와 같은 방식).
    // 실제로 늘어났으면 true
    public bool Refill()
    {
        if (count >= maxCount) return false;
        count = maxCount;
        return true;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (!kb.leftCtrlKey.wasPressedThisFrame && !kb.rightCtrlKey.wasPressedThisFrame) return;

        if (count <= 0 || health == null) return;

        health.Heal(Mathf.Round(health.maxHealth * healPercent * 0.01f));
        count--;
    }
}
