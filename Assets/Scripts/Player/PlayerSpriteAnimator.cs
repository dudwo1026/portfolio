using UnityEngine;

// 플레이어 스프라이트를 상태에 따라 갈아끼운다.
// 멈춰 있으면 idle, 움직이면 moveFrames를 순환한다.
//
// 그림마다 해상도와 캐릭터가 차지하는 영역이 달라서, 프레임을 바꿀 때
// 쿼드 크기와 위치를 같이 보정해야 키가 들쭉날쭉하거나 발이 뜨지 않는다.
//  quadSize  : 그려진 캐릭터의 키가 프레임마다 같아지도록 계산한 쿼드 크기
//  localY    : 캐릭터 발이 지면에 닿도록 하는 세로 보정
//  uvOffsetX : 캐릭터 발이 회전축 한가운데 오도록 하는 가로 보정.
//              트랜스폼을 옮기면 빌보드 회전 때문에 어긋나므로 UV를 민다
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [System.Serializable]
    public class Frame
    {
        public Texture2D texture;
        [Tooltip("그림 속 캐릭터의 높이(px)")]
        public float charHeightPx = 1f;
        [Tooltip("그림 아래 끝에서 캐릭터 발까지의 여백(px)")]
        public float charBottomPx;
        [Tooltip("캐릭터 발의 가로 중심(px)")]
        public float footCenterPx;
        [Tooltip("미세 보정. 포즈마다 실루엣 높이가 달라 체감 크기가 어긋날 때 이걸로 맞춘다")]
        public float sizeScale = 1f;
    }

    [Tooltip("비워 두면 부모에서 찾는다")]
    public TopDownMove mover;
    [Tooltip("비워 두면 부모에서 찾는다. 공격 모션 시점을 여기서 읽는다")]
    public PlayerCombat combat;

    [Tooltip("그려진 캐릭터의 기준 키(월드 단위). 캡슐 높이와 맞춰 두면 된다")]
    public float characterHeight = 2.6f;

    public Frame idle;
    public Frame[] moveFrames;
    [Tooltip("이동 중 프레임 전환 속도")]
    public float framesPerSecond = 3f;

    [Header("제자리 공격")]
    [Tooltip("제자리에서 원거리 공격할 때 쓸 프레임. 순서대로 재생된다")]
    public Frame[] attackFrames;
    [Tooltip("한 발 쏜 뒤 이 시간 동안 공격 자세를 유지한다")]
    public float attackHoldTime = 0.25f;
    [Tooltip("공격 프레임 전환 속도")]
    public float attackFramesPerSecond = 14f;

    [Header("대시")]
    [Tooltip("비워 두면 부모에서 찾는다")]
    public PlayerDash dash;
    [Tooltip("대시 중 자세. 한 장이라 그대로 유지된다")]
    public Frame dashFrame;

    [Header("이동 중 공격")]
    [Tooltip("이동하면서 원거리 공격할 때 쓸 프레임. 다리가 매 프레임 교대하도록 배열되어 있다")]
    public Frame[] moveAttackFrames;
    [Tooltip("이동 중 공격 프레임 전환 속도. 걷기와 같게 두면 다리 속도가 일정하다")]
    public float moveAttackFramesPerSecond = 3f;

    [Header("좌우 반전")]
    [Tooltip("원본 그림이 화면 왼쪽을 보고 있으면 켠다. 반대로 나오면 끄면 된다")]
    public Frame[] meleeFrames;
    public float meleeHoldTime = 0.5f;          // 근접 쿨타임과 같게 두면 끊김 없이 이어진다
    public float meleeFramesPerSecond = 6f;

    public Frame[] deathFrames;
    public float deathFramesPerSecond = 4f;

    public bool artFacesScreenLeft = true;
    [Tooltip("이 값보다 좌우 성분이 작으면(거의 위아래로만 이동) 보던 방향을 유지한다")]
    public float flipDeadzone = 0.15f;

    Renderer rend;
    MaterialPropertyBlock mpb;
    float timer;
    int index;
    Health health;
    float deathTimer;
    PlayerAirborne airborne;
    PlayerStun stun;
    float stunTimer;
    public float stunFirstFrameTime = 0.3f; // 기절: 사망 1장을 이만큼 보여 준 뒤 2장
    int facing = 1;   // 1 = 원본, -1 = 좌우 반전. 멈춰도 마지막 방향을 유지한다

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        if (mover == null && transform.parent != null)
            mover = transform.parent.GetComponent<TopDownMove>();
        if (combat == null && transform.parent != null)
            combat = transform.parent.GetComponent<PlayerCombat>();
        if (dash == null && transform.parent != null)
            dash = transform.parent.GetComponent<PlayerDash>();
        if (health == null && transform.parent != null)
            health = transform.parent.GetComponent<Health>();
        timer = 0f;
        deathTimer = 0f;
        index = 0;
        Apply(idle);
    }

    void LateUpdate()
    {
        // 사망이 최우선. GameOverUI가 체력 0인 프레임에 곧바로 timeScale을 0으로 만들기 때문에
        // 여기서는 unscaledDeltaTime을 쓴다. 마지막 장에서 멈춘다
        if (health != null && health.CurrentHealth <= 0f && deathFrames != null && deathFrames.Length > 0)
        {
            deathTimer += Time.unscaledDeltaTime;
            float dStep = deathFramesPerSecond > 0f ? 1f / deathFramesPerSecond : 1f;
            Apply(deathFrames[Mathf.Min(Mathf.FloorToInt(deathTimer / dStep), deathFrames.Length - 1)]);
            return;
        }
        deathTimer = 0f;

        // 행동불가(에어본·기절)는 사망 모션 그림을 빌려 쓴다: 에어본은 공중 1장 / 착지 후 2장, 기절은 1→2장
        if (deathFrames != null && deathFrames.Length >= 2 && transform.parent != null)
        {
            if (airborne == null) airborne = transform.parent.GetComponent<PlayerAirborne>();
            if (stun == null) stun = transform.parent.GetComponent<PlayerStun>();
            if (airborne != null && airborne.IsActive)
            {
                Apply(deathFrames[airborne.InAir ? 0 : 1]);
                return;
            }
            if (stun != null && stun.IsActive)
            {
                stunTimer += Time.deltaTime;
                Apply(deathFrames[stunTimer < stunFirstFrameTime ? 0 : 1]);
                return;
            }
            stunTimer = 0f;
        }

        bool moving = mover != null && mover.MoveDirection.sqrMagnitude > 0.0001f;

        // 대시는 무엇보다 우선한다. 짧고(0.2초) 무적/회피 동작이라 자세가 바뀌면 안 된다
        if (dash != null && dash.IsDashing && dashFrame != null && dashFrame.texture != null)
        {
            UpdateFacing(dash.DashDirection);
            timer = 0f;
            index = 0;
            Apply(dashFrame);
            return;
        }

        // 근접은 원거리/이동보다 우선한다. 휘두르는 동안 다른 자세로 덮이면 안 된다
        if (combat != null && meleeFrames != null && meleeFrames.Length > 0)
        {
            float sinceMelee = Time.time - combat.LastMeleeTime;
            if (sinceMelee >= 0f && sinceMelee <= meleeHoldTime)
            {
                if (mover != null) UpdateFacing(mover.transform.forward); // 겨눈 쪽을 본다
                timer = 0f;
                index = 0;
                Apply(meleeFrames[Mathf.Clamp(Mathf.FloorToInt(sinceMelee * meleeFramesPerSecond), 0, meleeFrames.Length - 1)]);
                return;
            }
        }

        bool firing = combat != null
            && Time.time - combat.LastRangedFireTime >= 0f
            && Time.time - combat.LastRangedFireTime <= attackHoldTime;

        if (firing)
        {
            // 쏘는 동안은 이동 방향이 아니라 조준 방향(플레이어가 바라보는 쪽)을 본다.
            // 옆으로 이동하며 다른 쪽을 겨누는 경우가 있기 때문
            if (mover != null) UpdateFacing(mover.transform.forward);

            // 이동 중 공격: 다리가 계속 걷도록 주기적으로 순환시킨다.
            // 발사마다 처음으로 되돌리면 연사 중에 다리가 한 자세에 묶인다
            if (moving && moveAttackFrames != null && moveAttackFrames.Length > 0)
            {
                float maStep = moveAttackFramesPerSecond > 0f ? 1f / moveAttackFramesPerSecond : 1f;
                timer += Time.deltaTime;
                while (timer >= maStep) { timer -= maStep; index = (index + 1) % moveAttackFrames.Length; }
                Apply(moveAttackFrames[index % moveAttackFrames.Length]);
                return;
            }

            // 제자리 공격: 한 발마다 처음부터 재생
            if (!moving && attackFrames != null && attackFrames.Length > 0)
            {
                float since = Time.time - combat.LastRangedFireTime;
                int ai = Mathf.Clamp(Mathf.FloorToInt(since * attackFramesPerSecond), 0, attackFrames.Length - 1);
                timer = 0f;
                index = 0;
                Apply(attackFrames[ai]);
                return;
            }
        }

        if (moving) UpdateFacing(mover.MoveDirection);

        if (!moving || moveFrames == null || moveFrames.Length == 0)
        {
            // 멈추면 첫 프레임부터 다시 걷게 초기화한다
            timer = 0f;
            index = 0;
            Apply(idle);
            return;
        }

        float step = framesPerSecond > 0f ? 1f / framesPerSecond : 1f;
        timer += Time.deltaTime;
        while (timer >= step)
        {
            timer -= step;
            index = (index + 1) % moveFrames.Length;
        }
        // index는 이동(2장)과 이동사격(4장)이 같이 쓰는 값이라, 이동사격에서 2·3까지 올라간 채로
        // 넘어올 수 있다. 위 while의 나머지연산은 루프가 돌 때만 적용되므로 timer가 아직 안 찼으면
        // 보정되지 않는다. 그래서 쓰는 시점에 한 번 더 감아 준다 (여기서 IndexOutOfRange가 났었다).
        // 다리 순환을 일부러 이어 가는 설계라 index를 0으로 되돌리지는 않는다
        Apply(moveFrames[index % moveFrames.Length]);
    }

    // 화면 기준 어느 쪽으로 가는지로 정한다. 스프라이트가 늘 카메라를 보므로
    // 월드 방향이 아니라 카메라의 오른쪽 축에 투영해야 맞다
    void UpdateFacing(Vector3 dir)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.000001f) return;

        float side = Vector3.Dot(dir.normalized, cam.transform.right);
        if (Mathf.Abs(side) < flipDeadzone) return;   // 거의 위아래로만 갈 땐 그대로 둔다

        bool flip = artFacesScreenLeft ? side > 0f : side < 0f;
        facing = flip ? -1 : 1;
    }

    void Apply(Frame f)
    {
        if (f == null || f.texture == null || rend == null) return;

        // 쿼드 크기와 위치는 그림 속 캐릭터 치수에서 매번 계산한다.
        // 이렇게 해 두면 sizeScale을 만져도 발 위치와 가로 정렬이 저절로 따라온다
        float texW = f.texture.width;
        float texH = f.texture.height;
        float charPx = Mathf.Max(1f, f.charHeightPx);
        float quadH = characterHeight * Mathf.Max(0.01f, f.sizeScale) * texH / charPx;
        float quadW = quadH * texW / texH;
        float localY = quadH * 0.5f - (f.charBottomPx / texH) * quadH - 1.0f;
        float offsetX = -((f.footCenterPx - texW * 0.5f) / texW) * quadW;

        // 스케일 x를 음수로 두면 좌우가 뒤집힌다. 머티리얼이 양면(_Cull Off)이라 그대로 보인다
        transform.localScale = new Vector3(quadW * facing, quadH, 1f);

        // 가로 보정을 UV로 처리하면(_BaseMap_ST) wrap이 Clamp라 쿼드 밖으로 밀려난 부분이 잘린다.
        // 이동 프레임은 좌우 여백이 10px 남짓이라 지팡이 끝이 바로 잘려나갔다.
        // 그래서 텍스처는 건드리지 않고 쿼드 자체를 화면 가로 방향으로 민다.
        // 로컬 좌표로 밀면 플레이어가 회전할 때 같이 돌아 어긋나므로 카메라의 가로축을 쓴다
        Transform parent = transform.parent;
        Vector3 basePos = (parent != null ? parent.position : transform.position) + Vector3.up * localY;
        Camera cam = Camera.main;
        Vector3 screenRight = cam != null ? cam.transform.right : Vector3.right;
        transform.position = basePos + screenRight * (offsetX * facing);

        if (mpb == null) mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetTexture(BaseMapId, f.texture);
        mpb.SetTexture(MainTexId, f.texture);
        mpb.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f)); // UV는 원본 그대로 = 안 잘린다
        rend.SetPropertyBlock(mpb);
    }
}
