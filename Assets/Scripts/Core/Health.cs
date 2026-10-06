using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public event System.Action Damaged;


    void Awake()
    {
        CurrentHealth = maxHealth;
    }

public void TakeDamage(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0f, maxHealth);
        Damaged?.Invoke();
    }


public void Heal(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
    }
}
