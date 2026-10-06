using UnityEngine;

// 보스 일반공격: 워닝사인을 깔고 1초 뒤 반경 10을 내려찍는다.
// 보스는 이동하지 않으므로 EnemyAI의 기본 근접공격(attackDamage) 대신 이쪽을 쓴다.
[RequireComponent(typeof(EnemyAI))]
public class BossMeleeAttack : MonoBehaviour
{
    public float radius = 10f;
    public float cooldown = 4f;         // 워닝 시작 기준
    public float windupTime = 1.5f;     // 워닝사인 출현 ~ 실제 타격까지
    public float damage = 40f;
    public bool applyPoison = true;        // 타격 시 중독 1중첩
    public bool requireLineOfSight = true; // 장애물 뒤에 숨으면 맞지 않는다

    [Header("연출")]
    public Color warningColor = new Color(1f, 0.55f, 0.1f);
    public Color slamColor = new Color(1f, 0.35f, 0.2f);
    public float warningThickness = 0.05f;
    public float slamLifetime = 0.25f;

    EnemyAI ai;
    Renderer rend;
    Material warningMaterial;
    Material slamMaterial;
    Transform warning;
    float nextAttackTime;
    float strikeTimer;
    bool winding;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        rend = GetComponent<Renderer>();
    }

    // 옥상 입장 직후처럼, 한동안 손을 놓아야 할 때
    public void DelayFor(float seconds)
    {
        float until = Time.time + seconds;
        if (until > nextAttackTime) nextAttackTime = until;
    }

    void OnDisable()
    {
        ClearWarning();
        winding = false;
    }

    void FixedUpdate()
    {
        if (winding) { UpdateWindup(); return; }

        if (ai.IsStunned) return;
        if (Time.time < nextAttackTime) return;

        Transform p = ai.PlayerTransform;
        if (p == null) return;
        if (!InRange(p)) return;

        // 예고 시작. 쿨타임은 여기서부터 센다
        nextAttackTime = Time.time + cooldown;
        winding = true;
        strikeTimer = windupTime;
        SpawnWarning();
    }

    void UpdateWindup()
    {
        if (ai.IsStunned) { ClearWarning(); winding = false; return; } // 스턴당하면 취소

        strikeTimer -= Time.fixedDeltaTime;
        if (strikeTimer > 0f) return;

        winding = false;
        ClearWarning();
        Strike();
    }

    // 타격 순간에 다시 판정한다. 워닝 동안 반경 밖으로 빠지면 맞지 않는다
    void Strike()
    {
        SpawnSlam();

        Transform p = ai.PlayerTransform;
        if (p == null || !InRange(p)) return;

        Health h = ai.PlayerHealth;
        if (h != null) h.TakeDamage(damage);

        if (applyPoison)
        {
            PlayerPoison poison = p.GetComponent<PlayerPoison>();
            if (poison != null) poison.ApplyPoison(); // 타격 시 중독
        }
    }

    bool InRange(Transform p)
    {
        Vector3 d = p.position - transform.position;
        d.y = 0f;
        if (d.magnitude > radius) return false;
        if (requireLineOfSight && !ai.HasLineOfSight(transform.position, p)) return false;
        return true;
    }

    float GroundY()
    {
        return rend != null ? rend.bounds.min.y : transform.position.y;
    }

    void SpawnWarning()
    {
        if (warningMaterial == null)
        {
            warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            warningMaterial.color = warningColor;
        }

        GameObject go = MakeDisc("BossSlamWarning", warningMaterial, warningThickness);
        warning = go.transform;
    }

    void ClearWarning()
    {
        if (warning != null) Destroy(warning.gameObject);
        warning = null;
    }

    void SpawnSlam()
    {
        if (slamMaterial == null)
        {
            slamMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            slamMaterial.color = slamColor;
        }

        GameObject slam = MakeDisc("BossSlam", slamMaterial, 0.05f);
        Destroy(slam, slamLifetime);
    }

    // 보스의 자식으로 두면 보스 스케일(6)에 눌려 찌그러진다. 루트에 그대로 만든다
    GameObject MakeDisc(string name, Material mat, float thickness)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>()); // 연출용
        go.transform.position = new Vector3(transform.position.x, GroundY() + thickness, transform.position.z);
        go.transform.localScale = new Vector3(radius * 2f, thickness, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
