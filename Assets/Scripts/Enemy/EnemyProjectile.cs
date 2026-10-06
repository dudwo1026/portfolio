using UnityEngine;

// Lobbed enemy shell: flies a ballistic arc for a fixed flight time, then bursts and damages
// everything inside aoeRadius. Also bursts early if it slams into something solid.
public class EnemyProjectile : MonoBehaviour
{
    public float damage = 20f;
    public float aoeRadius = 2.5f;   // diameter 5
    public float gravity = 9.81f;
    public float flightTime = 2f;
    public bool appliesBlind;        // special shell: also inflicts 암흑
    public float blindDuration = 10f;

    Vector3 velocity;
    float elapsed;
    GameObject owner;
    bool spent;

public void Launch(Vector3 startVelocity, float g, float dmg, float radius, float time, GameObject shooter)
    {
        velocity = startVelocity;
        gravity = g;
        damage = dmg;
        aoeRadius = radius;
        flightTime = time;
        owner = shooter;
    }

    void FixedUpdate()
    {
        if (spent) return;

        elapsed += Time.fixedDeltaTime;
        velocity.y -= gravity * Time.fixedDeltaTime;
        transform.position += velocity * Time.fixedDeltaTime;

        if (elapsed >= flightTime) Explode(); // lands on target after exactly flightTime
    }

    void OnTriggerEnter(Collider other)
    {
        if (spent) return;
        if (other.isTrigger) return;
        if (owner != null && other.gameObject == owner) return;
        if (other.GetComponent<EnemyAI>() != null) return; // friendly fire off

        Explode();
    }

void Explode()
    {
        spent = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (owner != null && hits[i].gameObject == owner) continue;
            if (hits[i].GetComponent<EnemyAI>() != null) continue;

            Health h = hits[i].GetComponent<Health>();
            if (h != null) h.TakeDamage(damage);

            if (appliesBlind)
            {
                PlayerBlind blind = hits[i].GetComponent<PlayerBlind>();
                if (blind != null) blind.Apply(blindDuration);
            }
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, aoeRadius);
    }


public void SetBlind(float seconds)
    {
        appliesBlind = true;
        blindDuration = seconds;
    }
}
