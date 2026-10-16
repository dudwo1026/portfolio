using UnityEngine;

// 서쪽에서 동쪽으로 가로지르는 충격파. 자기 폭 안에 들어온 플레이어를 한 번만 때린다.
public class BossShockwave : MonoBehaviour
{
    public float damage = 50f;
    public float weakenDuration = 5f; // 쇼크웨이브 허약은 다른 소스보다 길게 간다
    public float speed = 17.5f;
    public float endX;
    public Vector3 size; // 판정 상자 크기. 0이면 localScale (상자 그대로 쓸 때)

    // 앞에서 계속 터뜨리는 이펙트(물보라). 칸 폭 안 아무 데서나
    public GameObject splashEffect;
    public float splashEffectScale = 4f;
    public float splashInterval = 0.07f;
    public float splashForward = 2.5f;
    public float trailLinger = 2f; // 끝에 닿은 뒤 달고 다니던 먼지가 잦아드는 시간

    Transform target;
    Health targetHealth;
    PlayerWeaken targetWeaken;
    bool hit;
    float nextSplash;

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
            Vector3 half = (size != Vector3.zero ? size : transform.localScale) * 0.5f;
            Vector3 d = target.position - transform.position;
            if (Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.z) <= half.z && Mathf.Abs(d.y) <= half.y + 1.5f)
            {
                hit = true;
                if (targetHealth != null) targetHealth.TakeDamage(damage);
                if (targetWeaken != null) targetWeaken.Apply(weakenDuration); // 타격 시 허약
            }
        }

        if (splashEffect != null && Time.time >= nextSplash)
        {
            nextSplash = Time.time + splashInterval;
            Vector3 half = size * 0.5f;
            Vector3 p = transform.position + new Vector3(splashForward, -half.y + 0.2f, Random.Range(-half.z, half.z));
            GameObject fx = Instantiate(splashEffect, p, Quaternion.identity);
            fx.transform.localScale = Vector3.one * splashEffectScale;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule m = ps.main;
                m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            Destroy(fx, 3f);
        }

        if (transform.position.x >= endX) End();
    }

    // 달고 다니던 먼지는 떼어 내 새로 뿜지 않고 남은 입자만 잦아들게 한다. 같이 지우면 뚝 끊긴다
    void End()
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.transform.parent != transform) continue; // 이펙트 루트만
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(ps.gameObject, trailLinger);
        }
        Destroy(gameObject);
    }
}
