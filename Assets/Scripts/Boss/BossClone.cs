using UnityEngine;

// 보스 분신: 일정 시간 플레이어를 조준하다 방향을 확정하고, 잠시 뒤 그 방향으로 날아간다.
// 조준하는 동안 바닥에 진행 경로를 표시하고, 발사 후에는 지형에 막힌다.
public class BossClone : MonoBehaviour
{
    public float aimDuration = 2f;    // 조준
    public float lockDelay = 1f;      // 방향 확정 후 발사까지
    public float speed = 60f;
    public float lifeTime = 6f;
    public float damage = 20f;
    public float hitRadius = 2.5f;

    [Header("페이크 조준 (특수패턴3)")]
    public bool fakeAim = false;
    public float swingDuration = 0.2f;  // 정조준에서 빗맞추는 방향으로 홱 도는 시간
    public float missDistance = 4f;     // 플레이어 오른쪽으로 이만큼 비껴간다

    public Color aimColor = new Color(0.35f, 0.02f, 0.08f);
    public Color lockedColor = new Color(1f, 0.3f, 0.1f);

    // 그림을 넣으면 상자 대신 카메라를 보는 그림으로 그린다. 조준·확정 색은 그림 위에 살짝 섞는다
    public Texture2D spriteTexture;
    public Material spriteMaterial;   // EnemySpriteFlash 재질 (몹 스프라이트와 같은 것)
    public float spriteHeight = 6f;   // 그림 세로 길이(월드)
    public float spriteTint = 0.35f;
    Transform sprite;
    Renderer spriteRend;
    MaterialPropertyBlock spriteBlock;

    [Header("바닥 경고 표시")]
    public float warningLength = 60f;
    public float warningThickness = 0.08f;
    public Color warningAimColor = new Color(1f, 0.55f, 0.1f, 1f);
    public Color warningLockedColor = new Color(1f, 0.1f, 0.05f, 1f);
    public float groundY = 40f;

    Transform target;
    GameObject owner;
    Health targetHealth;
    PlayerPoison targetPoison;
    Vector3 lockedDir;
    Vector3 swingFrom, swingTo;
    float timer;
    int phase;      // 0 조준, 1 확정 대기, 2 비행
    bool spent;
    Renderer rend;
    Transform warning;

    public void Init(Transform player, float dmg, float floorY, GameObject bossOwner)
    {
        target = player;
        owner = bossOwner; // 보스 본체는 자기 분신을 막지 않는다
        damage = dmg;
        groundY = floorY;
        if (player != null)
        {
            targetHealth = player.GetComponent<Health>();
            targetPoison = player.GetComponent<PlayerPoison>();
        }
    }

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = aimColor;
    }

    void Start()
    {
        BuildSprite();
        BuildWarning();
    }

    void BuildSprite()
    {
        if (spriteTexture == null || spriteMaterial == null) return;
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "CloneSprite";
        Destroy(q.GetComponent<Collider>());
        sprite = q.transform; // 부모(상자)에 붙이지 않는다: 상자는 날아가는 방향으로 돌고 크기도 다르다
        spriteRend = q.GetComponent<Renderer>();
        spriteRend.sharedMaterial = spriteMaterial;
        spriteRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 원본 그림은 보스 그림처럼 화면 오른쪽을 본다. 보스가 늘 화면 왼쪽을 보므로 똑같이 뒤집는다 (재질이 양면)
        sprite.localScale = new Vector3(-spriteHeight * spriteTexture.width / spriteTexture.height, spriteHeight, 1f);
        spriteBlock = new MaterialPropertyBlock();
        spriteRend.GetPropertyBlock(spriteBlock);
        spriteBlock.SetTexture("_BaseMap", spriteTexture);
        spriteBlock.SetTexture("_MainTex", spriteTexture);
        spriteRend.SetPropertyBlock(spriteBlock);
        if (rend != null) rend.enabled = false;
        Tint(aimColor);
        LateUpdate();
    }

    void Tint(Color c)
    {
        if (spriteRend == null) { if (rend != null) rend.material.color = c; return; }
        spriteRend.GetPropertyBlock(spriteBlock);
        spriteBlock.SetColor("_FlashColor", c);
        spriteBlock.SetFloat("_FlashAmount", spriteTint);
        spriteRend.SetPropertyBlock(spriteBlock);
    }

    // 바닥(상자 밑면)에 발을 두고 카메라를 마주 본다
    void LateUpdate()
    {
        if (sprite == null) return;
        Camera cam = Camera.main;
        if (cam != null) sprite.rotation = cam.transform.rotation;
        float bottom = transform.position.y - transform.lossyScale.y * 0.5f;
        sprite.position = new Vector3(transform.position.x, bottom, transform.position.z) + sprite.up * (spriteHeight * 0.5f);
    }


    void BuildWarning()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BossCloneWarning";
        Destroy(go.GetComponent<Collider>()); // 표시일 뿐
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = warningAimColor;
        go.GetComponent<Renderer>().sharedMaterial = m;
        warning = go.transform;
        UpdateWarning();
    }

    void UpdateWarning()
    {
        if (warning == null) return;

        // 분신 앞쪽으로 뻗은 띠. 장애물에 막히면 거기서 끊어 실제로 닿는 범위만 보여준다
        Vector3 dir = (phase == 0 || phase == 3) ? transform.forward : lockedDir;
        float length = ClearDistance(dir, warningLength);

        warning.position = new Vector3(transform.position.x + dir.x * length * 0.5f,
                                       groundY + warningThickness * 0.5f,
                                       transform.position.z + dir.z * length * 0.5f);
        warning.rotation = Quaternion.LookRotation(dir);
        warning.localScale = new Vector3(transform.localScale.x, warningThickness, length);
    }

    // 진행 방향으로 막히지 않고 갈 수 있는 거리. 플레이어는 장애물로 치지 않는다
    float ClearDistance(Vector3 dir, float max)
    {
        RaycastHit[] hits = Physics.BoxCastAll(transform.position, transform.localScale * 0.45f, dir,
                                               Quaternion.identity, max, ~0, QueryTriggerInteraction.Ignore);
        float best = max;
        for (int i = 0; i < hits.Length; i++)
        {
            if (!Blocks(hits[i].collider)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return Mathf.Max(1f, best);
    }

    // 경로를 실제로 끊는 것만 막는 것으로 친다.
    // 몹은 통과시킨다 — 몹 한 마리가 워닝을 잘라먹으면 경로를 읽을 수 없다
    bool Blocks(Collider col)
    {
        GameObject go = col.gameObject;
        if (go == gameObject) return false;
        if (owner != null && go == owner) return false;             // 보스 본체
        if (target != null && go == target.gameObject) return false; // 플레이어
        if (col.GetComponentInParent<EnemyAI>() != null) return false; // 몹
        if (BossTotem.Is(col)) return false;                           // 보스 자신의 방어 토템
        return true;
    }

    void FixedUpdate()
    {
        if (spent) return;

        timer += Time.fixedDeltaTime;

        if (phase == 0)
        {
            FaceTarget();
            UpdateWarning();
            if (timer >= aimDuration)
            {
                if (fakeAim)
                {
                    swingFrom = transform.forward;   // 정조준 방향
                    swingTo = MissDirection();       // 일부러 빗맞추는 방향
                    phase = 3;
                    timer = 0f;
                }
                else
                {
                    LockAim(transform.forward);
                }
            }
            return;
        }

        if (phase == 3) // 페이크: 짧은 시간에 홱 돌린다
        {
            float t = Mathf.Clamp01(timer / swingDuration);
            Vector3 dir = Vector3.Slerp(swingFrom, swingTo, t);
            transform.rotation = Quaternion.LookRotation(dir);
            UpdateWarning();
            if (t >= 1f) LockAim(swingTo);
            return;
        }

        if (phase == 1)
        {
            if (timer >= lockDelay)
            {
                phase = 2;
                timer = 0f;
                if (warning != null) Destroy(warning.gameObject); // 발사와 동시에 표시 제거
            }
            return;
        }

        float step = speed * Time.fixedDeltaTime;

        if (CheckHit()) return;

        // 지형에 막힌다. 분신보다 살짝 작게 훑어 바닥을 스치는 것은 무시.
        // 워닝이 보여준 경로와 어긋나지 않도록, 막는 판정은 ClearDistance와 같은 기준을 쓴다
        Vector3 half = transform.localScale * 0.45f;
        RaycastHit[] ahead = Physics.BoxCastAll(transform.position, half, lockedDir, Quaternion.identity, step, ~0, QueryTriggerInteraction.Ignore);
        float nearest = -1f;
        for (int i = 0; i < ahead.Length; i++)
        {
            if (!Blocks(ahead[i].collider)) continue;
            if (nearest < 0f || ahead[i].distance < nearest) nearest = ahead[i].distance;
        }
        if (nearest >= 0f)
        {
            transform.position += lockedDir * nearest;
            Destroy(gameObject);
            return;
        }

        transform.position += lockedDir * step;
        if (CheckHit()) return;

        if (timer >= lifeTime) Destroy(gameObject);
    }

    void LockAim(Vector3 dir)
    {
        lockedDir = dir;
        transform.rotation = Quaternion.LookRotation(dir);
        phase = 1;
        timer = 0f;
        Tint(lockedColor);
        if (warning != null) warning.GetComponent<Renderer>().sharedMaterial.color = warningLockedColor;
        UpdateWarning();
    }

    // 플레이어의 오른쪽(분신 기준)을 스쳐 지나가는 방향. 여러 분신이 같이 쏘면 경로가 다각형을 이룬다
    Vector3 MissDirection()
    {
        if (target == null) return transform.forward;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.01f) return transform.forward;

        float ratio = Mathf.Clamp(missDistance / dist, -1f, 1f);
        float angle = Mathf.Asin(ratio) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, angle, 0f) * (to / dist);
    }

    void FaceTarget()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    bool CheckHit()
    {
        if (target == null) return false;

        Vector3 d = target.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > hitRadius * hitRadius) return false;

        spent = true;
        if (targetHealth != null) targetHealth.TakeDamage(damage);
        if (targetPoison != null) targetPoison.ApplyPoison(); // 타격 시 중독
        Destroy(gameObject);
        return true;
    }

    void OnDestroy()
    {
        if (warning != null) Destroy(warning.gameObject);
        if (sprite != null) Destroy(sprite.gameObject);
    }
}
