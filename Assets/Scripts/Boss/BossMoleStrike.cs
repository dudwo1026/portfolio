using UnityEngine;

// 특수패턴3 후속타: 분신이 스쳐 지나간 자리에서 보스가 두더지처럼 바닥을 뚫고 솟구친다.
// 워닝이 먼저 깔리고, 솟는 순간 반경 안의 플레이어를 한 번 때린 뒤 다시 가라앉는다.
public class BossMoleStrike : MonoBehaviour
{
    public float warningTime = 1.2f;  // 분신이 지나간 뒤 솟아오르기까지
    public float damage = 40f;
    public float hitRadius = 5f;
    public float riseTime = 0.15f;
    public float stayTime = 0.4f;
    public float sinkTime = 0.35f;

    public Color warningColor = new Color(1f, 0.3f, 0.05f, 1f);
    public float warningThickness = 0.06f;

    enum Phase { Warning, Rise, Stay, Sink }

    Transform target;
    Health targetHealth;
    Phase phase = Phase.Warning;
    float timer;
    float floorY;
    float bodyHeight;
    Transform warning;
    bool hit;

    public void Init(Transform player, float groundY, Material bodyMaterial)
    {
        target = player;
        if (player != null) targetHealth = player.GetComponent<Health>();

        floorY = groundY;
        bodyHeight = transform.localScale.y;
        transform.position = new Vector3(transform.position.x, BottomY(), transform.position.z);

        Renderer r = GetComponent<Renderer>();
        if (r != null && bodyMaterial != null) r.sharedMaterial = bodyMaterial;

        BuildWarning();
    }

    float BottomY() { return floorY - bodyHeight * 0.5f; }  // 바닥 아래로 완전히 숨은 높이
    float TopY() { return floorY + bodyHeight * 0.5f; }     // 다 솟아올라 바닥에 선 높이

    void BuildWarning()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "BossMoleWarning";
        Destroy(go.GetComponent<Collider>()); // 표시일 뿐
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = warningColor;
        go.GetComponent<Renderer>().sharedMaterial = m;
        go.transform.position = new Vector3(transform.position.x, floorY + warningThickness * 0.5f, transform.position.z);
        go.transform.localScale = new Vector3(hitRadius * 2f, warningThickness, hitRadius * 2f);
        warning = go.transform;
    }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;

        if (phase == Phase.Warning)
        {
            if (timer < warningTime) return;
            phase = Phase.Rise;
            timer = 0f;
            if (warning != null) Destroy(warning.gameObject);
            return;
        }

        if (phase == Phase.Rise)
        {
            float t = riseTime <= 0f ? 1f : Mathf.Clamp01(timer / riseTime);
            SetHeight(Mathf.Lerp(BottomY(), TopY(), t));
            if (!hit) TryHit(); // 솟구치는 동안 범위에 있으면 맞는다
            if (t < 1f) return;
            phase = Phase.Stay;
            timer = 0f;
            return;
        }

        if (phase == Phase.Stay)
        {
            if (timer < stayTime) return;
            phase = Phase.Sink;
            timer = 0f;
            return;
        }

        float s = sinkTime <= 0f ? 1f : Mathf.Clamp01(timer / sinkTime);
        SetHeight(Mathf.Lerp(TopY(), BottomY(), s));
        if (s >= 1f) Destroy(gameObject);
    }

    void SetHeight(float y)
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, y, p.z);
    }

    void TryHit()
    {
        if (target == null) return;

        Vector3 d = target.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > hitRadius * hitRadius) return;

        hit = true;
        if (targetHealth != null) targetHealth.TakeDamage(damage);
    }

    void OnDestroy()
    {
        if (warning != null) Destroy(warning.gameObject);
    }
}
