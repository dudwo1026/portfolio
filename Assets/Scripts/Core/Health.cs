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
        // 이미 죽었으면 무시. Destroy는 프레임 끝에 처리돼서, 산탄총 펠릿·관통 레이저처럼
        // 같은 프레임에 피해가 몰리면 죽음 처리(처치 게이지·보급상자 드랍)가 여러 번 돌았다
        if (invulnerable || CurrentHealth <= 0f) return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0f, maxHealth);
        Damaged?.Invoke();
    }


// 난이도처럼 시작할 때 최대 체력을 바꿀 때. 현재 체력도 가득 채운다
    public void SetMaxHealth(float value)
    {
        maxHealth = value;
        CurrentHealth = value;
    }

public void Heal(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
    }
}
