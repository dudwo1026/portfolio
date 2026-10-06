using UnityEngine;

// 보급키트: 옥상 몹이 떨어뜨리는 작은 진회색 상자. 플레이어 손 높이에서 둥둥 떠 있다가
// 닿으면 탄창을 하나씩 채워주고 사라진다 (유탄은 채워지지 않는다).
public class SupplyKit : MonoBehaviour
{
    public static float DropChance = 0.25f; // 옥상 몹 처치 시 드랍 확률

    public float hoverHeight = 1.2f;   // 지면에서 띄우는 높이
    public float bobAmplitude = 0.2f;  // 위아래로 흔들리는 폭
    public float bobSpeed = 2f;
    public float spinSpeed = 45f;
    public float pickupRadius = 1.2f;  // 트리거가 놓쳐도 이 거리면 줍는다

    float baseY;
    float bobPhase;
    Transform player;
    bool taken;

    // 옥상에서 스폰된 몹이 죽을 때만 호출된다
    public static void TryDrop(Vector3 deathPosition)
    {
        if (Random.value > DropChance) return;
        Spawn(deathPosition);
    }

    public static SupplyKit Spawn(Vector3 deathPosition)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SupplyKit";
        go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

        Collider col = go.GetComponent<Collider>();
        col.isTrigger = true; // 몸으로 밀리지 않고 통과하면서 주워지도록

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = new Color(0.22f, 0.23f, 0.25f); // 진회색
        go.GetComponent<Renderer>().sharedMaterial = m;

        SupplyKit kit = go.AddComponent<SupplyKit>();
        kit.Place(deathPosition);
        return kit;
    }

    void Place(Vector3 deathPosition)
    {
        // 몹이 죽은 자리 바로 아래 지면을 찾아 그 위에 띄운다.
        // Destroy는 프레임 끝에 처리되므로 죽는 중인 몹의 콜라이더가 아직 살아 있다 — 몹은 건너뛴다
        float groundY = deathPosition.y;
        RaycastHit[] hits = Physics.RaycastAll(deathPosition + Vector3.up * 2f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hits[i].point.y > deathPosition.y + 0.5f) continue; // 머리 위에 걸린 것은 지면이 아니다
            if (hits[i].point.y > best) best = hits[i].point.y;
        }
        if (best > float.NegativeInfinity) groundY = best;

        baseY = groundY + hoverHeight;
        bobPhase = Random.Range(0f, Mathf.PI * 2f); // 여러 개가 같이 떠도 따로 놀게
        transform.position = new Vector3(deathPosition.x, baseY, deathPosition.z);
    }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        bobPhase += Time.deltaTime * bobSpeed;
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(bobPhase) * bobAmplitude;
        transform.position = pos;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 트리거는 빠른 이동에서 새기도 한다. 거리로도 한 번 더 본다
        if (taken || player == null) return;
        Vector3 d = player.position - transform.position;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius) Collect(player.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        Collect(other.gameObject);
    }

    void Collect(GameObject go)
    {
        if (taken) return;

        PlayerCombat pc = go.GetComponent<PlayerCombat>();
        if (pc == null) return;

        taken = true;
        pc.AddMagazine();
        Destroy(gameObject);
    }
}
