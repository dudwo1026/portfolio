using System.Collections.Generic;
using UnityEngine;

// 중독: 일정 시간에 걸쳐 최대 체력의 일정 비율을 깎는 도트 피해. 중첩된다.
// 맹독은 같은 구조에 배율만 2배인 강화판.
public class PlayerPoison : MonoBehaviour
{
    public float duration = 10f;        // 한 중첩이 유지되는 시간
    public float percentOfMaxHealth = 10f; // 한 중첩이 그 시간 동안 깎는 총량 (최대 체력 대비 %)
    public float deadlyMultiplier = 2f; // 맹독 배율
    public float tickInterval = 0.5f;

    public int StackCount { get { return stacks.Count; } }
    public bool IsActive { get { return stacks.Count > 0; } }

    struct Stack
    {
        public float remaining;
        public float multiplier;
    }

    readonly List<Stack> stacks = new List<Stack>();
    Health health;
    float tickTimer;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    public void ApplyPoison() { AddStack(1f); }          // 중독
    public void ApplyDeadlyPoison() { AddStack(deadlyMultiplier); } // 맹독

    public void AddStack(float multiplier)
    {
        Stack s;
        s.remaining = duration;
        s.multiplier = multiplier;
        stacks.Add(s);
    }

    public void Clear()
    {
        stacks.Clear();
    }

    void Update()
    {
        if (stacks.Count == 0) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = tickInterval;

        float damage = 0f;
        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            Stack s = stacks[i];

            // 이번 틱만큼의 몫. 남은 시간이 틱보다 짧으면 그만큼만
            float slice = Mathf.Min(tickInterval, s.remaining);
            if (health != null)
                damage += health.maxHealth * (percentOfMaxHealth * 0.01f) * s.multiplier * (slice / duration);

            s.remaining -= tickInterval;
            if (s.remaining <= 0f) stacks.RemoveAt(i);
            else stacks[i] = s;
        }

        if (damage > 0f && health != null) health.TakeDamage(damage);
    }
}
