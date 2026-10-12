using UnityEngine;

// 유탄: 수평으로 발사돼 중력에 의해 점점 떨어진다.
// 수평 속도는 '낙하에 걸리는 시간 안에 최대 사거리를 지나가도록' 역산하므로,
// 평지에서 막히지 않으면 정확히 maxDistance 지점에 떨어진다.
public class PlayerGrenade : MonoBehaviour
{
    public float maxDistance = 15f;
    public float gravity = 9.81f;
    public float explosionRadius = 3f;
    public float damage = 200f;
    public float minSelfDamage = 50f; // 플레이어가 폭발 가장자리에서 받는 최소 피해
    public float markerLifetime = 0.4f;
    public Color explosionColor = new Color(1f, 0.55f, 0.1f, 1f);

    // 폭발 연출 프리팹(CFXR2 WW Explosion). 유탄은 런타임에 만들어지므로
    // 인스펙터가 없다 — PlayerCombat이 발사 직전에 넣어 준다
    public GameObject explosionVfx;
    public float vfxScale = 2f; // 프리팹 기본 크기가 폭발 반경보다 작아서 키운다

    // 터질 때 화면 흔들림. 세기는 카메라가 밀리는 거리(카메라 거리 약 14.4 기준)
    public float shakeAmplitude = 0.35f;
    public float shakeDuration = 0.3f;

    Vector3 velocity;
    GameObject owner;
    float horizontalTraveled;
    bool spent;

    public void Launch(Vector3 dir, GameObject shooter, float dmg, float range, float radius)
    {
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        owner = shooter;
        damage = dmg;
        maxDistance = range;
        explosionRadius = radius;

        // 발사 높이에서 지면까지 떨어지는 시간 = t, 그 사이에 남은 사거리를 지나가는 수평 속도 = 거리 / t
        // 유탄 구체의 반지름만큼 먼저 지면에 닿고, 총구가 이미 앞으로 나가 있으므로 둘 다 빼준다
        float ballRadius = Mathf.Max(0.05f, transform.localScale.x * 0.5f);
        float groundY = FindGroundY();
        float height = Mathf.Max(0.05f, transform.position.y - groundY - ballRadius);
        float fallTime = Mathf.Sqrt(2f * height / gravity);

        float ahead = 0f;
        if (shooter != null)
        {
            Vector3 offset = transform.position - shooter.transform.position;
            offset.y = 0f;
            ahead = offset.magnitude;
        }
        float remaining = Mathf.Max(0.1f, maxDistance - ahead);
        velocity = dir * (remaining / fallTime); // 초기 수직 속도는 0 = 완전한 수평 발사
    }

    float FindGroundY()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    void FixedUpdate()
    {
        if (spent) return;

        float dt = Time.fixedDeltaTime;
        velocity.y -= gravity * dt;      // 수평으로 나가다 점점 처진다
        Vector3 stepVec = velocity * dt;
        float step = stepVec.magnitude;
        if (step <= 0.0001f) return;

        // 지면·건물·차량은 Rigidbody가 없어 트리거 이벤트가 오지 않는다. 이동 구간을 직접 훑는다
        float radius = Mathf.Max(0.05f, transform.localScale.x * 0.5f);
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, stepVec / step, step, ~0, QueryTriggerInteraction.Ignore);
        float nearest = -1f;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.gameObject == gameObject) continue;
            if (owner != null && hits[i].collider.gameObject == owner) continue;
            if (nearest < 0f || hits[i].distance < nearest) nearest = hits[i].distance;
        }
        if (nearest >= 0f)
        {
            transform.position += (stepVec / step) * nearest;
            Explode();
            return;
        }

        transform.position += stepVec;

        Vector3 flat = stepVec; flat.y = 0f;
        horizontalTraveled += flat.magnitude;
        if (horizontalTraveled >= maxDistance) Explode(); // 안전장치: 지형이 없어도 사거리에서 끝낸다
    }

    void OnTriggerEnter(Collider other)
    {
        if (spent) return;
        if (other.isTrigger) return;
        if (owner != null && other.gameObject == owner) return;

        Explode();
    }

    void Explode()
    {
        spent = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Health h = hits[i].GetComponent<Health>();
            if (h == null) continue;

            // 플레이어만 거리 감쇠를 받는다. 몬스터는 반경 안이면 어디서든 전탄 피해
            if (hits[i].GetComponent<PlayerCombat>() != null)
            {
                float dist = Vector3.Distance(transform.position, hits[i].transform.position);
                float t = explosionRadius > 0f ? Mathf.Clamp01(dist / explosionRadius) : 0f;
                h.TakeDamage(Mathf.Round(Mathf.Lerp(damage, minSelfDamage, t)));
                continue;
            }

            h.TakeDamage(damage);
        }

        SpawnVfx();
        SpawnMarker();
        CameraFollow.Shake(shakeAmplitude, shakeDuration);
        Destroy(gameObject);
    }

    // CFXR2 WW Explosion. 프리팹의 CFXR_Effect가 clearBehavior=Destroy 라
    // 연출이 끝나면 스스로 사라진다 — 여기서 수명을 따로 끊지 않는다
    void SpawnVfx()
    {
        if (explosionVfx == null) return;

        GameObject fx = Instantiate(explosionVfx, transform.position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * Mathf.Max(0.01f, vfxScale);
    }

    void SpawnMarker()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = explosionColor;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "GrenadeBlast";
        Destroy(marker.GetComponent<Collider>()); // 연출용
        marker.transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
        marker.transform.localScale = new Vector3(explosionRadius * 2f, 0.1f, explosionRadius * 2f);
        marker.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(marker, markerLifetime);
    }
}
