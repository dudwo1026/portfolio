using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public event System.Action Damaged;
    [System.NonSerialized] public bool invulnerable; // 테스트용 무적 (KillGauge의 0 키)


    void Awake()
    {
        CurrentHealth = maxHealth;
    }

public void TakeDamage(float amount)
    {
        if (invulnerable) return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0f, maxHealth);
        Damaged?.Invoke();
    }


public void Heal(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
    }
}
