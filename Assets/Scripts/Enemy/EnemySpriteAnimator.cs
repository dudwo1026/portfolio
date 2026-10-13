using UnityEngine;

// 잡몹 스프라이트를 상태에 따라 갈아끼운다. idle / move / attack 셋뿐이다.
// 몹의 자식 쿼드에 붙이고, EnemySpriteSet 에셋에서 그림을 읽는다.
//
// 플레이어와 달리 몹에는 "지금 움직이는 중" 플래그가 없다. EnemyAI는 배회·추격·넉백·
// 에어본을 각자 다른 방식으로 밀기 때문에, 어떤 플래그를 봐도 한 군데는 어긋난다.
// 그래서 부모가 실제로 움직인 거리를 직접 재서 판단한다 — 무엇이 밀었든 결과는 같다.
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class EnemySpriteAnimator : MonoBehaviour
{
    [Tooltip("스폰할 때 이 중 하나를 무작위로 고른다. 종류를 늘리려면 에셋만 더 넣으면 된다")]
    public EnemySpriteSet[] sets;
    [Tooltip("넣어 두면 무작위 추첨 없이 이것만 쓴다. 특정 종류를 찍어서 내보낼 때")]
    public EnemySpriteSet forcedSet;

    [Tooltip("부모 원점에서 발밑까지의 거리. 캡슐(높이 2, center 0)이면 -1")]
    public float pivotYOffset = -1f;
    [Tooltip("초당 이동 거리가 이보다 작으면 멈춘 것으로 보고 idle을 쓴다")]
    public float moveThreshold = 0.35f;
    [Tooltip("타격 판정 뒤에도 이만큼 공격 자세를 유지한다. 선딜과 합쳐 모션 전체 길이가 된다")]
    public float attackFollowThrough = 0.25f;
    [Tooltip("이 값보다 좌우 성분이 작으면(거의 위아래로만 이동) 보던 방향을 유지한다")]
    public float flipDeadzone = 0.15f;

    public EnemySpriteSet Set { get { return set; } }

    EnemySpriteSet set;
    EnemyAI ai;
    // 특수 패턴이 있는 정예만 잡힌다. 일반 잡몹은 null이라 특수 분기를 통째로 건너뛴다
    IElitePatternVisual pattern;
    // 보스 패턴들. 셋의 motions에서 이름이 같은 모션을 그린다
    IBossMotion[] bossMotions;
    float chargeStartTime = -999f;
    Renderer rend;
    MaterialPropertyBlock mpb;
    Vector3 lastParentPos;
    float speed;        // 측정된 이동 속도 (월드 단위/초)
    float timer;
    int index;
    int facing = 1;     // 1 = 원본, -1 = 좌우 반전. 멈춰도 마지막 방향을 유지한다
    // 그릴 때만 한 번 더 뒤집는 보정. facing을 직접 건드리면 매 프레임 부호가 뒤집혀 깜빡인다
    int renderFlip = 1;
    // 마지막에 그린 프레임. 일시정지 중 크기를 바꿨을 때 같은 자세로 다시 그리는 데 쓴다
    EnemySpriteSet.Frame lastFrame;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        if (transform.parent != null)
        {
            ai = transform.parent.GetComponent<EnemyAI>();
            pattern = transform.parent.GetComponent<IElitePatternVisual>();
            bossMotions = transform.parent.GetComponents<IBossMotion>();
        }
        chargeStartTime = -999f;

        set = PickSet();
        lastParentPos = ParentPos();
        speed = 0f;
        timer = 0f;
        index = 0;
        if (set != null) ApplyIdle();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
    }

    // 일시정지 중에는 LateUpdate가 아예 돌지 않아 크기를 만져도 화면이 그대로다.
    // 크기를 눈으로 맞추려면 몹이 멈춰 있어야 하는데(안 그러면 맞아 죽거나 자세가 계속 바뀐다)
    // 그 둘이 정면으로 부딪친다. 그래서 멈춘 동안에도 "마지막에 그린 그 자세 그대로"
    // 다시 한 번 그려 준다. 애니메이션은 진행시키지 않는다 — 크기만 따라온다
    void EditorTick()
    {
        if (this == null) return;
        if (!Application.isPlaying) return;                   // 에디트 모드는 ExecuteAlways가 맡는다
        if (!UnityEditor.EditorApplication.isPaused) return;   // 돌아가는 중이면 LateUpdate가 맡는다
        if (lastFrame == null) return;

        Vector3 before = transform.localScale;
        Apply(lastFrame);
        // 멈춰 있을 때는 게임 뷰가 알아서 다시 그리지 않는다. 바뀐 경우에만 갱신을 요청한다
        if ((transform.localScale - before).sqrMagnitude > 0.0000001f)
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif

    // 스폰마다 종류를 다시 뽑는다. 풀링으로 재사용되어도 OnEnable에서 다시 불리므로
    // 같은 오브젝트가 계속 같은 몹으로 나오지 않는다
    EnemySpriteSet PickSet()
    {
        if (forcedSet != null) return forcedSet;
        if (sets == null || sets.Length == 0) return null;
        // 에디트 모드에서까지 매번 바뀌면 프리팹을 볼 수가 없다. 미리보기는 첫 번째로 고정
        if (!Application.isPlaying) return sets[0];
        return sets[Random.Range(0, sets.Length)];
    }

    Vector3 ParentPos()
    {
        return transform.parent != null ? transform.parent.position : transform.position;
    }

    void LateUpdate()
    {
        if (set == null || rend == null) return;

        // 이동량 측정. Apply가 이 트랜스폼을 직접 옮기므로 반드시 부모를 봐야 한다
        Vector3 now = ParentPos();
        Vector3 delta = now - lastParentPos;
        delta.y = 0f;
        lastParentPos = now;

        renderFlip = 1; // 공격 분기에서만 다시 켠다
        float dt = Application.isPlaying ? Time.deltaTime : 0f;
        float measured = dt > 0.0001f ? delta.magnitude / dt : 0f;
        speed = Mathf.Lerp(speed, measured, 0.35f); // 프레임마다 튀는 값을 눌러 준다

        // 기절이 가장 위다. 돌진이 벽에 박힌 뒤 3~5초를 뻗어 있는 구간이라,
        // 그 사이 넉백이 들어오든 공격 타이머가 남아 있든 기절 자세가 이겨야 한다
        if (ai != null && ai.IsStunned && set.stun != null && set.stun.texture != null)
        {
            speed = 0f;
            timer = 0f;
            index = 0;
            Apply(set.stun); // 보던 방향은 그대로 둔다. 뻗은 놈이 몸을 돌리면 이상하다
            return;
        }

        // 보스 패턴 모션. 재생 중인 패턴이 있으면 그 모션이 이긴다
        if (bossMotions != null && set.motions != null)
        {
            for (int i = 0; i < bossMotions.Length; i++)
            {
                IBossMotion m = bossMotions[i];
                if (m == null || !m.MotionActive) continue;
                EnemySpriteSet.Motion motion = FindMotion(m.MotionName);
                if (motion == null) continue;
                timer = 0f;
                index = 0;
                Apply(MotionFrame(motion, m.MotionProgress));
                return;
            }
        }

        // 정예 특수 패턴. 넉백(피격)보다 위에 두는 이유는 패턴 중에 밀려도 패턴은 멈추지 않기 때문이다
        if (pattern != null && pattern.PatternActive && set.chargeFrames != null && set.chargeFrames.Length > 0)
        {
            int last = set.chargeFrames.Length - 1;
            float progress = pattern.PatternProgress;
            int ci;
            if (progress < 0f)
            {
                // 길이를 모르는 패턴(돌진). 고정 속도로 1회 재생한 뒤 마지막 장에서 멈춘다.
                // 순환시키면 몸을 세우는 첫 장이 돌진 도중에 다시 튀어나온다
                if (chargeStartTime < 0f) chargeStartTime = Time.time;
                float cfps = set.chargeFramesPerSecond > 0f ? set.chargeFramesPerSecond : 10f;
                ci = Mathf.Min(Mathf.FloorToInt((Time.time - chargeStartTime) * cfps), last);
            }
            else
            {
                // 길이가 고정인 패턴(넉백 시전, 자폭 도화선). 패턴 시간 전체에 펼친다.
                // 그래야 길이를 바꿔도 그림이 중간에 잘리거나 남지 않는다.
                // easing이 1보다 크면 뒤로 갈수록 간격이 짧아진다 — 도화선이 타들어가는 가속
                float e = set.chargeFrameEasing;
                float shaped = (e > 0f && e != 1f) ? Mathf.Pow(progress, e) : progress;
                ci = Mathf.Clamp(Mathf.FloorToInt(shaped * set.chargeFrames.Length), 0, last);
            }

            // 돌진(길이 모름)만 달려가는 쪽을 본다. 나머지(넉백·잡기·사격·자폭)는 전부 플레이어를 노리므로
            // 플레이어를 본다 — 원거리 정예가 물러나며 쏠 때 등을 돌리고 쏘지 않도록
            if (progress < 0f && delta.sqrMagnitude > 0.000001f) UpdateFacing(delta);
            else if (ai != null && ai.PlayerTransform != null) UpdateFacing(ai.PlayerTransform.position - now);

            speed = 0f;
            timer = 0f;
            index = 0;
            Apply(set.chargeFrames[ci]);
            return;
        }
        chargeStartTime = -999f; // 다음 패턴은 처음부터 다시 재생한다

        // 밀려나는 동안은 idle로 둔다. 이동량으로 걷는지 판단하기 때문에 그냥 두면
        // 제 발로 달린 적이 없는데도 걷는 자세가 나오고, 밀린 방향으로 몸까지 돌아버린다.
        // EnemyAI도 넉백 중에는 공격 진행을 멈추므로 공격 모션보다 위에 둔다
        if (ai != null && ai.IsKnockedBack)
        {
            // 켠 셋만 밀려나는 쪽을 본다. 슬라임의 궤적이나 고스트의 불꽃처럼 진행 방향이
            // 그림에 드러나는 몹은 맞기 직전 방향에 묶여 있으면 궤적이 앞쪽에 달려 어색하다.
            // 방향이 드러나지 않는 몹은 플레이어를 계속 보는 쪽이 자연스러워 그대로 둔다.
            // 미는 방향을 그대로 쓰므로 옆에서 밀렸을 때도 어긋나지 않는다
            if (set.faceKnockbackDirection) UpdateFacing(ai.KnockbackDirection);
            speed = 0f; // 밀린 속도가 남아 있으면 넉백이 끝난 직후 몇 프레임 동안 걷는 자세가 샌다
            timer = 0f;
            index = 0;
            ApplyIdle();
            return;
        }

        // 공격이 최우선. 휘두르는 도중에 걷는 자세로 덮이면 선딜이 읽히지 않는다
        if (ai != null && set.attackFrames != null && set.attackFrames.Length > 0)
        {
            float total = Mathf.Max(0.05f, ai.attackWindup + attackFollowThrough);
            float since = Time.time - ai.AttackMotionTime;
            if (since >= 0f && since <= total)
            {
                // 장수에 상관없이 모션 전체에 고르게 펼친다. 선딜을 바꿔도 따로 맞출 필요가 없다
                int k = Mathf.Clamp(Mathf.FloorToInt(since / total * set.attackFrames.Length), 0, set.attackFrames.Length - 1);
                if (ai.PlayerTransform != null) UpdateFacing(ai.PlayerTransform.position - now);
                if (set.attackArtFlipped) renderFlip = -1;
                timer = 0f;
                index = 0;
                Apply(set.attackFrames[k]);
                return;
            }
        }

        bool moving = speed > moveThreshold;
        if (moving) UpdateFacing(delta);

        if (!moving || set.moveFrames == null || set.moveFrames.Length == 0)
        {
            timer = 0f;
            index = 0;
            ApplyIdle();
            return;
        }

        float fps = set.moveFramesPerSecond;
        if (set.scaleFpsBySpeed && set.referenceSpeed > 0.01f)
            fps *= Mathf.Clamp(speed / set.referenceSpeed, 0.5f, 2.5f);

        float step = fps > 0f ? 1f / fps : 1f;
        timer += dt;
        while (timer >= step)
        {
            timer -= step;
            index = (index + 1) % set.moveFrames.Length;
        }
        Apply(set.moveFrames[index % set.moveFrames.Length]);
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
        if (Mathf.Abs(side) < flipDeadzone) return;

        facing = (set.artFacesScreenLeft ? side > 0f : side < 0f) ? -1 : 1;
    }

    EnemySpriteSet.Motion FindMotion(string motionName)
    {
        for (int i = 0; i < set.motions.Length; i++)
            if (set.motions[i] != null && set.motions[i].name == motionName && set.motions[i].frames != null && set.motions[i].frames.Length > 0)
                return set.motions[i];
        return null;
    }

    // 진행도 0~1은 hitFrame 앞쪽 장들에, 1~2는 hitFrame부터의 장들에 펼친다.
    // 그래서 hitFrame 이후 장은 타격 판정이 생기기 전에는 절대 나오지 않는다
    static EnemySpriteSet.Frame MotionFrame(EnemySpriteSet.Motion m, float p)
    {
        int n = m.frames.Length;
        int h = m.hitFrame;
        if (h <= 0 || h >= n)
            return m.frames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(p) * n), 0, n - 1)];
        if (p < 1f)
            return m.frames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, p) * h), 0, h - 1)];
        return m.frames[Mathf.Clamp(h + Mathf.FloorToInt((p - 1f) * (n - h)), h, n - 1)];
    }

    // 대기 자세. 여러 장이 있으면 시간에 맞춰 순환하고, 숨쉬기를 얹는다
    void ApplyIdle()
    {
        Apply(IdleFrame(), true);
    }

    EnemySpriteSet.Frame IdleFrame()
    {
        if (set.idleFrames == null || set.idleFrames.Length == 0) return set.idle;
        int n = set.idleFrames.Length;
        if (n == 1 || !Application.isPlaying) return set.idleFrames[0];

        // 왕복이면 1..N..2 (N장이 2N-2칸), 아니면 1..N
        int steps = set.idlePingPong ? 2 * n - 2 : n;
        int s = Mathf.FloorToInt(Time.time * Mathf.Max(0.01f, set.idleFramesPerSecond)) % steps;
        return set.idleFrames[s < n ? s : steps - s];
    }

    // 쿼드 크기와 위치를 그림 속 몹의 치수에서 매번 계산한다.
    // 이렇게 해 두면 sizeScale을 만져도 발 위치와 가로 정렬이 저절로 따라온다
    void Apply(EnemySpriteSet.Frame f, bool breathe = false)
    {
        if (f == null || f.texture == null || rend == null) return;
        lastFrame = f;

        float texW = f.texture.width;
        float texH = f.texture.height;
        float charPx = Mathf.Max(1f, f.charHeightPx);
        float quadH = set.characterHeight * Mathf.Max(0.01f, f.sizeScale) * texH / charPx;
        float quadW = quadH * texW / texH;

        // 숨쉬기: 세로로 늘면 가로는 그 절반만큼 줄어든다. 발 높이는 아래 localY가 다시 맞춘다
        if (breathe && set.breatheAmount > 0f && Application.isPlaying)
        {
            float b = set.breatheAmount * Mathf.Sin(Time.time * 2f * Mathf.PI / Mathf.Max(0.1f, set.breathePeriod));
            quadH *= 1f + b;
            quadW *= 1f - b * 0.5f;
        }

        float localY = quadH * 0.5f - (f.charBottomPx / texH) * quadH + pivotYOffset;
        float offsetX = -((f.footCenterPx - texW * 0.5f) / texW) * quadW;

        // 스케일 x를 음수로 두면 좌우가 뒤집힌다. 머티리얼이 양면(_Cull Off)이라 그대로 보인다.
        // 가로 보정(offsetX)에도 같은 부호를 곱해야 뒤집힌 그림의 발 위치가 따라온다.
        // 크기는 월드 단위로 계산했으므로 부모가 커져 있으면(보스 큐브 x6) 그만큼 나눠 준다
        // 방향 고정 셋은 보던 방향(facing)을 무시하고 항상 화면 왼쪽을 본다
        int face = set.alwaysFaceScreenLeft ? (set.artFacesScreenLeft ? 1 : -1) : facing;
        int flip = face * renderFlip;
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(quadW * flip / ps.x, quadH / ps.y, 1f / ps.z);

        // 가로 보정을 UV로 처리하면 wrap이 Clamp라 쿼드 밖으로 밀려난 부분이 잘린다.
        // 그래서 텍스처는 건드리지 않고 쿼드 자체를 화면 가로 방향으로 민다.
        // 로컬 좌표로 밀면 몹이 회전할 때 같이 돌아 어긋나므로 카메라의 가로축을 쓴다
        Vector3 basePos = ParentPos() + Vector3.up * localY;
        Camera cam = Camera.main;
        Vector3 screenRight = cam != null ? cam.transform.right : Vector3.right;
        transform.position = basePos + screenRight * (offsetX * flip);

        if (mpb == null) mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetTexture(BaseMapId, f.texture);
        mpb.SetTexture(MainTexId, f.texture);
        mpb.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
        rend.SetPropertyBlock(mpb);
    }
}
