using UnityEngine;

// 서쪽에서 동쪽으로 가로지르는 충격파. 자기 폭 안에 들어온 플레이어를 한 번만 때린다.
public class BossShockwave : MonoBehaviour
{
    public float damage = 50f;
    public float weakenDuration = 5f; // 쇼크웨이브 허약은 다른 소스보다 길게 간다
    public float speed = 17.5f;
    public float endX;

    Transform target;
    Health targetHealth;
    PlayerWeaken targetWeaken;
    bool hit;

    public void Init(Transform player, float dmg, float spd, float finishX)
    {
        target = player;
        damage = dmg;
        speed = spd;
        endX = finishX;
        if (player != null)
        {
            targetHealth = player.GetComponent<Health>();
            targetWeaken = player.GetComponent<PlayerWeaken>();
        }
    }

    void FixedUpdate()
    {
        transform.position += Vector3.right * speed * Time.fixedDeltaTime;

        if (!hit && target != null)
        {
            Vector3 half = transform.localScale * 0.5f;
            Vector3 d = target.position - transform.position;
            if (Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.z) <= half.z && Mathf.Abs(d.y) <= half.y + 1.5f)
            {
                hit = true;
                if (targetHealth != null) targetHealth.TakeDamage(damage);
                if (targetWeaken != null) targetWeaken.Apply(weakenDuration); // 타격 시 허약
            }
        }

        if (transform.position.x >= endX) Destroy(gameObject);
    }
}
