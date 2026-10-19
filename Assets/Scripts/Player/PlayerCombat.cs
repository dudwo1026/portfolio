using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponSlot { Primary, Secondary, Melee }

public class PlayerCombat : MonoBehaviour
{
    public WeaponSlot currentSlot = WeaponSlot.Primary;

    public PrimaryWeapon primaryWeapon = PrimaryWeapon.Rifle; // 타이틀에서 고르면 그 값으로 덮인다

    public float primaryFireRate = 8f;
    // 0.25가 아니라 0.23인 이유: 발사 판정이 프레임 단위라 쿨다운이 프레임 중간에 풀려도
    // 다음 프레임에야 쏜다. 그 손해(60fps에서 최대 16ms)를 미리 깎아 실효 4발/초를 맞춘 값
    public float secondaryFireCooldown = 0.23f;
    public float meleeCooldown = 0.5f;
    // 실제 사거리 = meleeRange + meleeRadius. 1.96 + 0.8 = 2.76 (이전 2.3 에서 20% 증가)
    public float meleeRange = 1.96f;
    public float meleeRadius = 0.8f;

    public float bulletSpeed = 30f; // 20 -> 30 (1.5배). 주무기와 권총이 같이 쓴다
    public float muzzleLift = 0.25f; // 총구를 몸 중심보다 이만큼 올린다. 아래 Muzzle() 참고
    // 총알 사거리 = bulletSpeed * bulletLifeTime. 탄속을 30으로 올렸으므로
    // 사거리를 예전과 같은 60으로 유지하려면 수명은 2초여야 한다 (30 * 2 = 60)
    public float bulletLifeTime = 2f;
    public float bulletDamage = 20f;  // 소총
    public float pistolDamage = 40f;  // 권총
    public float meleeDamage = 50f;
    public float meleeKnockbackForce = 4f;
    public float meleeKnockbackDuration = 0.5f;
    public float meleeConeAngle = 120f;
    public float meleeWindup = 0.15f;   // 누르고 나서 판정이 나가기까지. 이 사이에 대시하면 취소된다
    float meleeHitAt = -1f;             // 판정 예약 시각 (-1 = 없음)
    Vector3 meleeDir;                   // 누른 순간의 방향. 선딜 중에 몸을 돌려도 그쪽으로 친다

    [Header("근접 연출")]
    public GameObject meleeHitVfx;            // CFXR4 Sword Hit PLAIN (Cross). 맞은 적마다 X자 섬광
    public float meleeHitVfxScale = 0.35f;
    // 휘두르는 궤적 (판정 부채꼴 그대로). 보도블록이 거의 흰색이라 옅은 색은 묻힌다 — 진한 주황
    public Color meleeArcColor = new Color(1f, 0.5f, 0.05f, 1f);
    public Color meleeHitTint = new Color(1f, 0.85f, 0.4f, 1f); // X자 섬광 색 (원본은 흰색이라 밝은 바닥·몹 위에서 안 보인다)
    public float meleeArcGlow = 3f;           // 궤적 빛 번짐 층의 밝기 배율. 1을 넘어야 블룸에 걸린다 (0이면 빛 층 없음)
    public GameObject meleeSparksVfx;         // Sparks explode white. 맞은 자리에서 튀는 불꽃 (섬광과 같이)
    public float meleeSparksScale = 0.8f;
    public float meleeHitVfxSpeed = 2f;       // 섬광·불꽃 재생 속도. 2면 재생 시간이 절반 (휘두르는 궤적보다 튀지 않게)
    public float meleeArcSweepTime = 0.09f;   // 쓸고 지나가는 시간
    public float meleeArcFadeTime = 0.15f;    // 다 쓸고 나서 사라지는 시간
    public float meleeHitShake = 0.12f;       // 하나라도 맞히면 화면이 짧게 떨린다 (유탄 0.35)
    public float meleeHitShakeTime = 0.1f;
    int meleeSwingDir = 1;                    // 휘두를 때마다 방향을 바꾼다
    public float gunshotAlertRadius = 30f; // 사격 소리가 닿는 범위
    public GameObject bulletImpactVfx;     // 착탄 이펙트: CFXR3 Fire Explosion B 프리팹
    public float bulletImpactVfxScale = 0.4f;

    [Header("유탄발사기 (주무기 장착, F키 또는 휠 클릭)")]
    public int grenadeLoaded = 1;   // 약실에 든 유탄 (1발씩만)
    public int grenadeReserve = 2;     // 남은 예비 유탄
    public int grenadeMaxReserve = 2;  // 보급 시 채워지는 예비 유탄 (장전 1발 + 이만큼)
    public float grenadeDamage = 200f;
    public float grenadeRange = 15f;
    public float grenadeRadius = 5f;
    public float grenadeCooldown = 1f;
    public GameObject grenadeExplosionVfx;   // CFXR2 WW Explosion 프리팹
    public float grenadeExplosionVfxScale = 3.33f; // 반경 3에서 2였던 걸 반경 5에 맞춰 같은 비율로

    [Header("레이저 (주무기 대안, 좌클릭 홀드)")]
    // 소총 순간 DPS(20 x 8발 = 160)의 1.2배. 탄은 소총과 같은 탄창을 같은 속도(초당 primaryFireRate발)로 쓴다
    public float laserDps = 192f;
    public float laserRange = 60f;      // 소총 사거리(탄속 45 x 수명 1.33)와 같게
    public float laserRadius = 0.3f;    // 판정 굵기 (반지름)
    // 보이는 굵기. 1단 laserMinWidth에서 시작해 최대 차지 laserWidth까지, 피해 배율(laserRampBySecond)과
    // 같은 비율로 굵어진다 (배율 50% → 80% → 100% = 굵기 0.225 → 0.44 → 0.585)
    public float laserMinWidth = 0.225f;
    public float laserWidth = 0.585f;
    public float laserWidthChangeTime = 0.15f; // 단계가 오를 때 이 시간에 걸쳐 굵어진다
    public Color laserColor = new Color(0.1f, 0.55f, 1f, 1f); // 이펙트가 없을 때의 단색 선
    public GameObject laserEffectPrefab; // Laser AOE Player. 비워 두면 단색 선(LineRenderer)으로 그린다
    // 최대 차지 때 이펙트 색. 프리팹은 1단 색(황금)으로 칠해 두고, 굵기와 같은 비율로 이 색 쪽으로 옮겨 간다.
    // 부위마다 밝기·투명도는 원래대로 두고 색상(H)만 옮긴다 (채도는 더 진해질 때만 따라간다)
    public Color laserChargedColor = new Color(1f, 0.5f, 0.31f); // 코랄
    // 최대 차지 때 밝기 배율. 빔 재질이 매우 밝아(HDR x4.75) 원래 밝기로는 붉은 기가 넘쳐 노랗게 보인다.
    // 밝기를 낮춰야 코랄이 진하게 남는다
    public float laserChargedBrightness = 0.5f;
    public float laserTopStageTint = 0.7f; // 2단 → 3단 색 변화를 이 비율만큼만 (0.7이면 3단 = 88% 지점, 1이면 laserChargedColor 그대로)
    public float laserShimmer = 0.08f;     // 보이는 굵기가 이 비율(±)만큼 일렁인다
    public float laserShimmerSpeed = 18f;  // 일렁임 빠르기
    // 착탄: 빔이 벽·장애물에 닿은 끝에서 계속 튀는 연출(프리팹의 Impact)과, 관통당한 적마다 튀는 불꽃
    public GameObject laserHitVfx;          // Sparks explode white. 피해가 들어갈 때마다(초당 8번) 맞은 적 자리에
    public float laserHitVfxScale = 0.6f;
    public int laserHitVfxMaxPerTick = 4;   // 한 번에 불꽃을 띄울 적 수 (줄줄이 관통할 때 화면이 덮이지 않게)
    GameObject laserFx;
    Transform laserFxImpact;
    ParticleSystem[] laserFxAll;
    Vector4[] laserFxBaseHsv;        // 부위별 원래 색 (h, s, v, a)
    float laserFxTint = -1f;         // 마지막으로 칠한 차지 비율. 바뀔 때만 다시 칠한다
    ParticleSystem[] laserFxBeams;   // 빔 = 늘림 배율이 음수인 파티클. 굵기·길이를 매 프레임 맞춘다
    float[] laserFxBeamSizes;        // 프리팹 원래 크기. 가장 굵은 빔이 laserWidth가 되도록 같은 비율로 줄인다
    float laserFxWidest;
    float laserShownWidth;           // 지금 보이는 굵기 (단계 사이를 부드럽게 잇는다)
    ParticleSystem.Particle[] laserFxBuf;
    public float laserPierceFalloff = 0.15f; // 적을 하나 뚫을 때마다 다음 적이 받는 피해가 이만큼(비율) 준다
    public float laserMinDamageRatio = 0.1f; // 그래도 이 아래로는 안 내려간다
    LineRenderer laserLine;
    float laserTick; // 다음 탄 소모·피해까지 남은 시간. 떼었다 눌러도 줄지 않아 연타로 DPS를 올릴 수 없다
    // 계속 쏜 시간(초)에 따른 피해 배율: 0~1초 50%, 1~2초 80%, 2초부터 100%.
    // 떼거나 장전·대시 등으로 끊기면 처음(50%)부터 다시
    public float[] laserRampBySecond = { 0.5f, 0.8f, 1f };
    bool laserFiring;
    bool waitFireRelease; // 일시정지가 풀린 뒤 좌클릭을 한 번 뗄 때까지 공격 막음
    float laserStartTime;
    Vector3 laserDir; // 쏘기 시작한 순간의 방향. 떼기 전까지 이 방향으로만 나간다

    public int GrenadeLoaded => grenadeLoaded;
    public int GrenadeReserve => grenadeReserve;
    public int GrenadeCount => grenadeLoaded + grenadeReserve;


    public int primaryMagSize = 30;
    public int laserMagSize = 60;      // 레이저 탄창 (초당 8발 소모 → 7.5초)
    public int laserMaxReserve = 300;  // 레이저 예비탄 → 장전 60 + 예비 = 전체 360
    public int supplyKitPrimaryRounds = 30; // 보급키트 하나가 채우는 주무기 예비탄 (소총·레이저 공통)

    [Header("산탄총 (주무기 대안, 1클릭 1발)")]
    public int shotgunPellets = 12;
    public float shotgunSpread = 25f;       // 전방 이 각도(전체 폭) 안에서 탄마다 무작위 방향
    public float shotgunPelletDamage = 8f;  // 다 맞으면 한 발에 96
    // 0.25가 아니라 0.23인 이유는 권총과 같다: 프레임 단위 판정 손해를 미리 깎아 실효 0.25초
    public float shotgunCooldown = 0.23f;
    public int shotgunMagSize = 8;
    public int shotgunMaxReserve = 120;     // 장전 8 + 예비 = 전체 128
    public int shotgunSupplyKitRounds = 8;  // 키트 하나 = 탄창 하나. 30발이면 발당 96이라 소총 키트의 약 5배가 된다
    public int primaryMaxReserveMags = 11;  // 예비 330발 → 장전 30 + 예비 = 전체 360
    public int secondaryMagSize = 8;   // 권총: 예비탄 무제한, 탄창만 있다
    [System.NonSerialized] public bool infiniteAmmo; // 테스트용: 쏴도 탄창·유탄이 줄지 않는다 (KillGauge의 0 키)
    [System.NonSerialized] public bool debugPistolDamage; // 테스트용: 권총 피해 1000 (KillGauge의 ; 키)
    public float reloadTime = 1f; // 실제로 장전되면 이 시간 동안 원거리 사격 불가 (근접은 가능)
    public float secondaryReloadTime = 0.7f; // 권총은 더 빨리 장전한다
    float reloadEndTime;
    public bool IsReloading => Time.time < reloadEndTime; // 지금 든 무기를 장전하는 중 (탄은 시작할 때 이미 채워진다)



    float nextFireTime = 0f;
    float nextGrenadeTime = 0f;
    // 권총(반자동)의 선입력. 쿨다운 중에 누른 클릭을 딱 한 개만 기억한다.
    // bool 한 개라서 열 번을 눌러도 기억은 한 개 — 쿨다운이 풀릴 때 한 발만 나간다
    bool secondaryFireBuffered;
    int primaryAmmo;
    int primaryReserve;   // 예비 탄 (발 단위). 장전은 빈 만큼만 여기서 빼 온다
    int secondaryAmmo;

    // 원거리 무기를 마지막으로 발사한 시각. 공격 모션 재생용 (근접은 포함하지 않는다)
    public float LastRangedFireTime { get; private set; } = -999f;
    public float LastMeleeTime { get; private set; } = -999f;

    public int PrimaryAmmo => primaryAmmo;
    public int PrimaryReserve => primaryReserve;
    public int SecondaryAmmo => secondaryAmmo;
    public int PrimaryMagSize =>
        primaryWeapon == PrimaryWeapon.Laser ? laserMagSize :
        primaryWeapon == PrimaryWeapon.Shotgun ? shotgunMagSize : primaryMagSize;
    int PrimaryMaxReserve =>
        primaryWeapon == PrimaryWeapon.Laser ? laserMaxReserve :
        primaryWeapon == PrimaryWeapon.Shotgun ? shotgunMaxReserve : primaryMaxReserveMags * primaryMagSize;

    // 구획 세이브(ZoneSave)용. 주무기 종류는 GameSettings가 들고 있으므로 탄 수만 주고받는다
    public int[] GetAmmoState() { return new[] { primaryAmmo, primaryReserve, secondaryAmmo, grenadeLoaded, grenadeReserve }; }
    public void SetAmmoState(int[] s)
    {
        if (s == null || s.Length < 5) return;
        primaryAmmo = s[0]; primaryReserve = s[1]; secondaryAmmo = s[2]; grenadeLoaded = s[3]; grenadeReserve = s[4];
    }

    TopDownMove mover;
    PlayerBound bound;
    PlayerDash dash;

void Awake()
    {
        mover = GetComponent<TopDownMove>();
        if (GetComponent<Health>() != null && GetComponent<HitFlash>() == null) gameObject.AddComponent<HitFlash>(); // 맞으면 몹처럼 잠깐 하얗게
        bound = GetComponent<PlayerBound>();
        dash = GetComponent<PlayerDash>();
        if (GameSettings.primaryChosen) primaryWeapon = GameSettings.primary;
        primaryAmmo = PrimaryMagSize;
        primaryReserve = PrimaryMaxReserve;
        secondaryAmmo = secondaryMagSize;

        // 탄약처럼 유탄도 시작할 때 채운다. 씬에 남은 값이 0이면 첫 발이 안 나가버린다
        grenadeLoaded = 1;
        grenadeReserve = grenadeMaxReserve;
    }




    void Update()
    {
        // 일시정지(가이드·메뉴·확인창·게임오버) 중에는 입력을 받지 않는다. 풀린 뒤에도 그 클릭을 쥔 채라면
        // 소총·레이저가 바로 쏴 버리므로, 좌클릭을 한 번 뗄 때까지 공격을 막는다
        if (Time.timeScale == 0f) { waitFireRelease = true; secondaryFireBuffered = false; return; }

        // 예약된 근접 판정. 선딜 중에 대시하면 그 공격은 나가지 않는다
        if (meleeHitAt >= 0f)
        {
            if (dash != null && dash.IsDashing) meleeHitAt = -1f;
            else if (Time.time >= meleeHitAt) { meleeHitAt = -1f; MeleeAttack(); }
        }
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            // 무기를 바꾸면 기억해 둔 클릭은 버린다 (바꾸자마자 저절로 한 발 나가지 않게)
            if (kb.digit1Key.wasPressedThisFrame) { currentSlot = WeaponSlot.Primary; secondaryFireBuffered = false; }
            if (kb.digit2Key.wasPressedThisFrame) { currentSlot = WeaponSlot.Secondary; secondaryFireBuffered = false; }
            if (kb.digit3Key.wasPressedThisFrame) { currentSlot = WeaponSlot.Melee; secondaryFireBuffered = false; }
            if (kb.rKey.wasPressedThisFrame) Reload();

            // 유탄: 휠 클릭을 좌클릭으로 보내는 마우스가 있어서 키보드에도 열어둔다
            if (kb.fKey.wasPressedThisFrame && Time.time >= nextGrenadeTime) FireGrenade();
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (waitFireRelease && !mouse.leftButton.isPressed) waitFireRelease = false;
        bool attackHeld = !waitFireRelease && mouse.leftButton.isPressed;
        bool attackClicked = !waitFireRelease && mouse.leftButton.wasPressedThisFrame;

        if (laserFiring) { } // 레이저를 쏘는 동안은 처음 방향을 유지한다 (이동은 된다)
        else if (mouse.rightButton.isPressed)
            AimAtMouse(mouse);
        else if (mover != null && mover.MoveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(mover.MoveDirection);

        laserTick = Mathf.Max(0f, laserTick - Time.deltaTime);
        bool laserMode = currentSlot == WeaponSlot.Primary && primaryWeapon == PrimaryWeapon.Laser;
        UpdateLaser(laserMode && attackHeld);

        bool firePressed;
        if (laserMode)
        {
            firePressed = false; // 레이저는 UpdateLaser가 맡는다
        }
        else if (currentSlot == WeaponSlot.Primary && primaryWeapon != PrimaryWeapon.Shotgun)
        {
            firePressed = attackHeld; // 소총은 꾹 누르면 연사
        }
        else if (currentSlot != WeaponSlot.Melee) // 권총, 산탄총: 1클릭 1발
        {
            // 권총은 반자동. 예전엔 쿨다운 중에 누른 클릭이 그냥 버려져서,
            // 실제 연사력이 쿨다운이 아니라 "내 클릭 주기의 배수"로 결정됐다
            // (초당 5번 누르면 오히려 초당 2발로 떨어졌음).
            // 이제 그 클릭을 하나만 기억해 뒀다가 쿨다운이 풀리는 즉시 쏜다.
            if (attackClicked) secondaryFireBuffered = true;
            firePressed = secondaryFireBuffered;
        }
        else
        {
            firePressed = attackClicked; // 근접은 예전 그대로
        }

        if (firePressed && Time.time >= nextFireTime)
        {
            secondaryFireBuffered = false; // 기억해 둔 클릭은 여기서 소모된다
            Fire();
        }

        // 유탄발사기는 주무기에 달려 있다: 주무기를 들고 휠 클릭
        if (mouse.middleButton.wasPressedThisFrame && Time.time >= nextGrenadeTime)
            FireGrenade();
    }

    // 레이저: 누르고 있는 동안 정면으로 한 줄기. 적은 관통하고 건물·장애물에서 끊긴다.
    // 탄은 소총과 같은 탄창을 1/primaryFireRate초마다 1발씩 쓰고, 그때마다 그 사이 분량의 피해를 준다
    void UpdateLaser(bool held)
    {
        bool canFire = held && primaryAmmo > 0 && Time.time >= reloadEndTime
                       && !(dash != null && dash.IsDashing) && !(bound != null && bound.IsActive);
        if (!canFire)
        {
            StopLaserVisual();
            return;
        }
        if (!laserFiring) { laserFiring = true; laserDir = transform.forward; laserStartTime = Time.time; laserShownWidth = 0f; } // 쏘기 시작 = 방향 고정

        Vector3 dir = laserDir;
        Vector3 origin = Muzzle(dir); // 총알과 같은 총구
        float length = LaserLength(origin, dir);

        // 차지 단계에 맞춰 굵어진다. 처음 프레임은 1단 굵기에서 바로 시작
        float targetWidth = LaserStageWidth(LaserRamp());
        laserShownWidth = laserShownWidth <= 0f ? targetWidth
            : Mathf.MoveTowards(laserShownWidth, targetWidth, laserWidth / Mathf.Max(0.01f, laserWidthChangeTime) * Time.deltaTime);

        if (laserEffectPrefab != null) PlaceLaserFx(origin, dir, length, laserShownWidth);
        else
        {
            if (laserLine == null) laserLine = BuildLaserLine();
            laserLine.enabled = true;
            laserLine.startWidth = laserShownWidth;
            laserLine.endWidth = laserShownWidth * 0.7f;
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, origin + dir * length);
        }
        LastRangedFireTime = Time.time; // 공격 모션 유지

        if (laserTick > 0f) return;
        laserTick += 1f / primaryFireRate;
        if (!infiniteAmmo) primaryAmmo--;
        DamageAlongLaser(origin, dir, length, laserDps / primaryFireRate * LaserRamp());
        EnemyAI.AlertGunshot(transform.position, gunshotAlertRadius);
    }

    float LaserRamp()
    {
        if (laserRampBySecond == null || laserRampBySecond.Length == 0) return 1f;
        int i = Mathf.Clamp(Mathf.FloorToInt(Time.time - laserStartTime), 0, laserRampBySecond.Length - 1);
        return laserRampBySecond[i];
    }

    // 정면으로 가장 먼저 막는 건물·장애물까지의 거리. 적과 플레이어 자신은 막지 않는다
    float LaserLength(Vector3 origin, Vector3 dir)
    {
        float best = laserRange;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, laserRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return best;
    }

    void DamageAlongLaser(Vector3 origin, Vector3 dir, float length, float damage)
    {
        RaycastHit[] hits = Physics.SphereCastAll(origin, laserRadius, dir, length, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); // 가까운 적부터 뚫는다
        var struck = new System.Collections.Generic.HashSet<Health>(); // 콜라이더가 여럿인 적이 두 번 맞지 않게
        Color sparkTint = LaserStageColor();
        Camera cam = Camera.main;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<EnemyAI>() == null) continue;
            Health h = hits[i].collider.GetComponentInParent<Health>();
            if (h == null || !struck.Add(h)) continue;
            // 첫 적 100%, 둘째 85%, 셋째 70%, ... 일곱째부터 10%
            float ratio = Mathf.Max(laserMinDamageRatio, 1f - laserPierceFalloff * (struck.Count - 1));
            h.TakeDamage(damage * ratio);

            // 관통당한 자리에 불꽃. 빔이 몸을 지나는 점에서, 스프라이트에 묻히지 않게 카메라 쪽으로 당긴다
            if (struck.Count > laserHitVfxMaxPerTick) continue;
            Vector3 at = hits[i].distance > 0f ? origin + dir * hits[i].distance : hits[i].collider.bounds.center;
            if (cam != null) at -= cam.transform.forward * 1.5f;
            SpawnTintedOnce(laserHitVfx, at, laserHitVfxScale, sparkTint);
        }
    }

    // 지금 차지 단계의 빔 색 (황금 → laserChargedColor). 착탄 불꽃을 빔과 같은 색으로 칠할 때 쓴다
    Color LaserStageColor()
    {
        Color gold = new Color(1f, 0.8f, 0.3f, 1f);
        return Color.Lerp(gold, laserChargedColor, Mathf.Clamp01(laserFxTint));
    }

    void StopLaserVisual()
    {
        laserFiring = false;
        if (laserLine != null) laserLine.enabled = false;
        if (laserFx != null) Destroy(laserFx);
        laserFx = null;
    }

    // 기절 등으로 이 컴포넌트가 꺼지면 Update가 안 돌아 빔이 그 자리에 남는다
    void OnDisable()
    {
        StopLaserVisual();
        meleeHitAt = -1f; // 선딜 중에 기절 등으로 꺼지면 그 공격은 버린다
    }

    // 이펙트는 위로 뻗는 레이저라 위쪽을 dir로 눕힌다 (보스 레이저와 같은 방식).
    // 빔 길이 = 크기 x 늘림 배율이라, 굵기를 바꾸면 배율도 같이 다시 잰다
    void PlaceLaserFx(Vector3 origin, Vector3 dir, float length, float width)
    {
        if (laserFx == null)
        {
            laserFx = Instantiate(laserEffectPrefab);
            laserFx.name = "PlayerLaser";
            var beams = new System.Collections.Generic.List<ParticleSystem>();
            foreach (ParticleSystemRenderer r in laserFx.GetComponentsInChildren<ParticleSystemRenderer>())
                if (r.renderMode == ParticleSystemRenderMode.Stretch && r.lengthScale < 0f) beams.Add(r.GetComponent<ParticleSystem>());
            laserFxBeams = beams.ToArray();
            laserFxBeamSizes = new float[laserFxBeams.Length];
            laserFxWidest = 0f;
            for (int i = 0; i < laserFxBeams.Length; i++)
            {
                laserFxBeamSizes[i] = laserFxBeams[i].main.startSize.constantMax;
                laserFxWidest = Mathf.Max(laserFxWidest, laserFxBeamSizes[i]);
            }
            if (laserFxBuf == null) laserFxBuf = new ParticleSystem.Particle[64];

            laserFxAll = laserFx.GetComponentsInChildren<ParticleSystem>();
            laserFxBaseHsv = new Vector4[laserFxAll.Length];
            for (int i = 0; i < laserFxAll.Length; i++)
            {
                Color c = laserFxAll[i].main.startColor.color;
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                laserFxBaseHsv[i] = new Vector4(h, s, v, c.a);
            }
            laserFxTint = -1f;
            laserFxImpact = laserFx.transform.Find("Impact");
        }

        laserFx.transform.SetPositionAndRotation(origin, Quaternion.FromToRotation(Vector3.up, dir));

        // 빔 끝에 착탄 연출. 아무것도 안 맞고 사거리 끝까지 나갔으면 끈다
        if (laserFxImpact != null)
        {
            bool blocked = length < laserRange - 0.05f;
            if (laserFxImpact.gameObject.activeSelf != blocked) laserFxImpact.gameObject.SetActive(blocked);
            laserFxImpact.localPosition = Vector3.up * length; // 이펙트는 위쪽(+Y)이 빔 방향
        }
        TintLaserFx(width); // 색은 일렁임 전 굵기로 정한다. 일렁인 값으로 정하면 색까지 깜빡인다
        if (laserFxWidest <= 0f) return;

        // 보이는 굵기만 미세하게 일렁인다 (판정 굵기 laserRadius는 그대로)
        width *= 1f + laserShimmer * (Mathf.PerlinNoise(Time.time * laserShimmerSpeed, 0.37f) * 2f - 1f);

        for (int i = 0; i < laserFxBeams.Length; i++)
        {
            ParticleSystem ps = laserFxBeams[i];
            float size = laserFxBeamSizes[i] * width / laserFxWidest;
            ParticleSystem.MainModule m = ps.main;
            m.startSize = size;
            // 이미 나와 있는 빔 조각도 같이 굵힌다. 안 그러면 단계가 오를 때 0.4초 동안 굵기·길이가 어긋난다
            int n = ps.GetParticles(laserFxBuf);
            for (int j = 0; j < n; j++) laserFxBuf[j].startSize = size;
            ps.SetParticles(laserFxBuf, n);
            ps.GetComponent<ParticleSystemRenderer>().lengthScale = -length / size;
        }
    }

    // 피해 배율(1단 laserRampBySecond[0] ~ 1)을 보이는 굵기(laserMinWidth ~ laserWidth)로 옮긴다
    float LaserStageWidth(float ramp)
    {
        float r0 = laserRampBySecond != null && laserRampBySecond.Length > 0 ? laserRampBySecond[0] : 1f;
        return Mathf.Lerp(laserMinWidth, laserWidth, Mathf.InverseLerp(r0, 1f, ramp));
    }

    // 1단 굵기 = 원래 색, 최대 굵기 = laserChargedColor 쪽. 사이는 굵기 비율대로 (2단은 60% 지점).
    // 2단 → 3단 구간은 laserTopStageTint만큼만 옮겨 3단이 2단과 너무 동떨어지지 않게 한다 (0.7이면 3단 = 88% 지점)
    void TintLaserFx(float width)
    {
        bool hasRamp = laserRampBySecond != null && laserRampBySecond.Length > 0;
        float minWidth = laserMinWidth;
        float t = Mathf.InverseLerp(minWidth, laserWidth, width);
        if (hasRamp && laserRampBySecond.Length > 2)
        {
            float t2 = Mathf.InverseLerp(minWidth, laserWidth, LaserStageWidth(laserRampBySecond[1]));
            if (t > t2) t = t2 + (t - t2) * laserTopStageTint;
        }
        if (Mathf.Abs(t - laserFxTint) < 0.01f) return;
        laserFxTint = t;

        float ch, cs, cv;
        Color.RGBToHSV(laserChargedColor, out ch, out cs, out cv);
        for (int i = 0; i < laserFxAll.Length; i++)
        {
            Vector4 b = laserFxBaseHsv[i];
            if (b.y < 0.05f) continue; // 무채색 부위(빔 가운데 하얀 심)는 단계가 올라도 하얗게 둔다
            // 채도는 내리지 않는다. 코랄(채도 0.69)이 황금(0.95)보다 옅어서 그대로 섞으면
            // 발광 재질 위에서 하얗게 바래 오히려 연해 보였다
            Color c = Color.HSVToRGB(Mathf.Lerp(b.x, ch, t), Mathf.Lerp(b.y, Mathf.Max(b.y, cs), t), b.z * Mathf.Lerp(1f, laserChargedBrightness, t));
            c.a = b.w;
            ParticleSystem.MainModule m = laserFxAll[i].main;
            m.startColor = c;
        }
    }

    LineRenderer BuildLaserLine()
    {
        GameObject go = new GameObject("LaserBeam");
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = laserWidth;
        lr.endWidth = laserWidth * 0.7f;
        lr.material = new Material(Shader.Find("Sprites/Default")); // 조명 없이 색 그대로
        lr.startColor = laserColor;
        lr.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, laserColor.a * 0.6f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void AimAtMouse(Mouse mouse)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        float dist;
        if (ground.Raycast(ray, out dist))
        {
            Vector3 hit = ray.GetPoint(dist);
            Vector3 dir = hit - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }

void Fire()
    {
        // 대시 중에는 어떤 공격도 나가지 않는다. 회피 동작 중에 자세가 바뀌면 안 되고,
        // 쿨타임·탄약도 소모하지 않도록 판정 맨 앞에서 끊는다
        if (dash != null && dash.IsDashing) return;

        bool isBound = bound != null && bound.IsActive;
        if (isBound && currentSlot != WeaponSlot.Melee) return; // 구속: ranged attacks blocked

        if (currentSlot == WeaponSlot.Melee)
        {
            // 판정은 meleeWindup 뒤에 나간다. 방향은 누른 순간으로 고정
            meleeDir = transform.forward;
            if (meleeWindup > 0f) meleeHitAt = Time.time + meleeWindup;
            else MeleeAttack();
            LastMeleeTime = Time.time; // 근접 모션이 이 시각을 보고 자세를 바꾼다
            if (isBound) bound.Release(); // meleeing breaks the bind
            nextFireTime = Time.time + meleeCooldown;
            return;
        }

        if (Time.time < reloadEndTime) return; // 장전 중

        if (currentSlot == WeaponSlot.Primary)
        {
            if (primaryAmmo <= 0) return;
            if (!infiniteAmmo) primaryAmmo--;
        }
        else
        {
            if (secondaryAmmo <= 0) return;
            if (!infiniteAmmo) secondaryAmmo--;
        }

        float rate;
        if (currentSlot == WeaponSlot.Primary && primaryWeapon == PrimaryWeapon.Shotgun)
        {
            // 전방 shotgunSpread도 안에서 탄마다 무작위 방향
            for (int i = 0; i < shotgunPellets; i++)
            {
                float yaw = Random.Range(-shotgunSpread * 0.5f, shotgunSpread * 0.5f);
                SpawnBullet(shotgunPelletDamage, Quaternion.Euler(0f, yaw, 0f) * transform.forward);
            }
            rate = shotgunCooldown;
        }
        else if (currentSlot == WeaponSlot.Primary)
        {
            SpawnBullet(bulletDamage, transform.forward);
            rate = 1f / primaryFireRate;
        }
        else
        {
            SpawnBullet(debugPistolDamage ? 1000f : pistolDamage, transform.forward);
            rate = secondaryFireCooldown;
        }
        LastRangedFireTime = Time.time; // 공격 모션이 이 시각을 보고 자세를 바꾼다
        EnemyAI.AlertGunshot(transform.position, gunshotAlertRadius); // 총성에 어그로가 끌린다 (근접은 제외)
        nextFireTime = Time.time + rate;
    }

    void FireGrenade()
    {
        if (currentSlot != WeaponSlot.Primary) return;      // 주무기 장착 중에만
        if (primaryWeapon != PrimaryWeapon.Rifle) return;    // 유탄발사기는 돌격소총에만 달려 있다
        if (dash != null && dash.IsDashing) return;          // 대시 중에는 공격 불가
        if (bound != null && bound.IsActive) return;         // 구속 중에는 원거리 불가
        if (grenadeLoaded <= 0) return; // 비었으면 R로 재장전해야 한다
        if (Time.time < reloadEndTime) return; // 장전 중

        if (!infiniteAmmo) grenadeLoaded--;
        nextGrenadeTime = Time.time + grenadeCooldown;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Grenade";
        g.transform.position = transform.position + transform.forward * 0.8f;
        g.transform.localScale = Vector3.one * 0.35f;
        g.GetComponent<Renderer>().material.color = new Color(0.2f, 0.35f, 0.15f); // 군용 녹색
        g.GetComponent<Collider>().isTrigger = true;

        PlayerGrenade pg = g.AddComponent<PlayerGrenade>();
        pg.explosionVfx = grenadeExplosionVfx; // 폭발 연출은 Launch 전에 넘겨 둔다
        pg.vfxScale = grenadeExplosionVfxScale;
        pg.Launch(transform.forward, gameObject, grenadeDamage, grenadeRange, grenadeRadius);

        EnemyAI.AlertGunshot(transform.position, gunshotAlertRadius); // 유탄도 시끄럽다
    }

    // 총구: 몸 앞 0.8, 몸 중심(발 +1.0)보다 muzzleLift만큼 위.
    // 차도(0.05)와 보도(1.02) 사이 턱이 1.0 가까이 돼서, 몸 중심 높이로 쏘면 차도에서 보도 쪽으로
    // 쏜 총알·레이저가 턱 옆면에 박혔다. 탄 반지름(0.1)까지 넘기도록 조금 올린다.
    // 너무 올리면 보도에서 차도의 몹(키 2.6)을 넘겨 버린다
    Vector3 Muzzle(Vector3 dir)
    {
        return transform.position + dir * 0.8f + Vector3.up * muzzleLift;
    }

    void SpawnBullet(float damage, Vector3 dir)
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bullet";
        bullet.transform.position = Muzzle(transform.forward);
        bullet.transform.localScale = Vector3.one * 0.2f;
        bullet.GetComponent<Renderer>().material.color = Color.black;

        Collider col = bullet.GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody brb = bullet.AddComponent<Rigidbody>();
        brb.useGravity = false;
        brb.linearVelocity = dir * bulletSpeed;

        Bullet b = bullet.AddComponent<Bullet>();
        b.lifeTime = bulletLifeTime;
        b.damage = damage;
        b.impactVfx = bulletImpactVfx;
        b.vfxScale = bulletImpactVfxScale;

    }

void MeleeAttack()
    {
        float reach = meleeRange + meleeRadius;
        SpawnMeleeArc(reach);
        bool landed = false;

        Collider[] hits = Physics.OverlapSphere(transform.position, reach);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].gameObject == gameObject) continue;

            Vector3 dir = hits[i].transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Angle(meleeDir, dir) > meleeConeAngle * 0.5f) continue;

            // 보스 방어 토템은 근접으로만 부서진다 (Health가 없어 총알·유탄은 통하지 않는다)
            BossTotem totem = hits[i].GetComponent<BossTotem>();
            if (totem != null) { SpawnMeleeHit(hits[i]); landed = true; totem.TakeMeleeHit(); continue; }

            Health h = hits[i].GetComponent<Health>();
            if (h != null && h.CurrentHealth > 0f) { SpawnMeleeHit(hits[i]); landed = true; }
            if (h != null) h.TakeDamage(meleeDamage);

            EnemyAI ai = hits[i].GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.NotifyMeleeHit(); // lets patterns react to being meleed (e.g. the grab cast gets cancelled)
                ai.ApplyKnockback(dir, meleeKnockbackForce, meleeKnockbackDuration);
                continue;
            }

            Rigidbody hitRb = hits[i].attachedRigidbody;
            if (hitRb != null)
                hitRb.AddForce(dir.normalized * meleeKnockbackForce, ForceMode.Impulse);
        }

        if (landed) CameraFollow.Shake(meleeHitShake, meleeHitShakeTime);
    }

    // 판정 부채꼴을 바닥에 그대로 그린다. 안쪽 반지름은 몸통에 겹치지 않게 조금 띄운다
    void SpawnMeleeArc(float reach)
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        float feet = capsule != null ? transform.position.y - capsule.height * 0.5f * transform.lossyScale.y : transform.position.y - 1f;
        Vector3 ground = new Vector3(transform.position.x, feet + 0.15f, transform.position.z);
        MeleeSlashArc.Spawn(ground, meleeDir, 0.45f, reach, meleeConeAngle, meleeArcColor,
                            meleeArcSweepTime, meleeArcFadeTime, meleeSwingDir, transform, meleeArcGlow);
        meleeSwingDir = -meleeSwingDir;
    }

    // 맞은 자리에 X자 섬광. 몸통 가운데에서 카메라 쪽으로 당겨 터뜨린다 —
    // 몹 스프라이트는 깊이를 쓰므로 몸 한가운데에 두면 그림 뒤로 반쯤 묻힌다
    void SpawnMeleeHit(Collider target)
    {
        Vector3 pos = target.bounds.center;
        Camera cam = Camera.main;
        if (cam != null) pos -= cam.transform.forward * 1.5f;

        SpawnTintedOnce(meleeHitVfx, pos, meleeHitVfxScale, meleeHitTint);
        SpawnTintedOnce(meleeSparksVfx, pos, meleeSparksScale, meleeHitTint);
    }

    // 맞은 자리 연출 하나를 tint 색으로 칠해 한 번만 재생한다 (근접·레이저 착탄 공용)
    void SpawnTintedOnce(GameObject prefab, Vector3 pos, float scale, Color tint)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule m = ps.main;
            m.loop = false; // 불꽃 프리팹은 반복 재생으로 되어 있다
            m.simulationSpeed *= Mathf.Max(0.01f, meleeHitVfxSpeed);
            Color c = m.startColor.color;
            m.startColor = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a * tint.a);
        }
        Destroy(fx, 1.5f / Mathf.Max(0.01f, meleeHitVfxSpeed)); // 둘 다 스스로 지워지지 않는다
    }

void OnDrawGizmosSelected()
    {
        float reach = meleeRange + meleeRadius;
        float half = meleeConeAngle * 0.5f;

        Gizmos.color = Color.red;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * transform.forward * reach;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * transform.forward * reach;
        Gizmos.DrawLine(transform.position, transform.position + left);
        Gizmos.DrawLine(transform.position, transform.position + right);

        Vector3 prev = transform.position + left;
        for (int i = 1; i <= 12; i++)
        {
            float a = -half + (meleeConeAngle * i / 12f);
            Vector3 p = transform.position + Quaternion.Euler(0f, a, 0f) * transform.forward * reach;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }


void Reload()
    {
        if (currentSlot == WeaponSlot.Primary)
        {
            // 유탄은 1발씩 장전한다. 소총 재장전과 같은 R키로 같이 처리
            bool loaded = false;
            if (grenadeLoaded <= 0 && grenadeReserve > 0)
            {
                grenadeReserve--;
                grenadeLoaded = 1;
                loaded = true;
            }

            // 남은 탄은 버리지 않는다. 빈 만큼만 예비에서 채운다
            int take = Mathf.Min(PrimaryMagSize - primaryAmmo, primaryReserve);
            primaryReserve -= take;
            primaryAmmo += take;
            if (loaded || take > 0) reloadEndTime = Time.time + reloadTime;
        }
        else if (currentSlot == WeaponSlot.Secondary)
        {
            int take = secondaryMagSize - secondaryAmmo; // 예비탄 무제한
            secondaryAmmo += take;
            if (take > 0) reloadEndTime = Time.time + secondaryReloadTime; // 꽉 찬 상태에서 R은 아무 일도 없다
        }
    }


// 주무기를 바꾸고 그 무기의 장전·예비탄으로 새로 채운다 (테스트 키 ])
    public void SetPrimaryWeapon(PrimaryWeapon weapon)
    {
        primaryWeapon = weapon;
        primaryAmmo = PrimaryMagSize;
        primaryReserve = PrimaryMaxReserve;
    }

public void RefillAmmo()
    {
        primaryAmmo = PrimaryMagSize;
        primaryReserve = PrimaryMaxReserve;
        secondaryAmmo = secondaryMagSize;

        grenadeLoaded = 1;                     // 유탄도 가득 채운다 (장전 1 + 예비)
        grenadeReserve = grenadeMaxReserve;
    }

    // 보급키트: 주무기 예비탄 supplyKitPrimaryRounds발 (유탄 제외, 권총은 예비탄 무제한이라 해당 없음).
    // 주무기는 무기와 상관없이 같은 발수다. 레이저 탄창(60)만큼 주면 소총의 약 2배 가치가 된다
    public void AddMagazine()
    {
        int kit = primaryWeapon == PrimaryWeapon.Shotgun ? shotgunSupplyKitRounds : supplyKitPrimaryRounds;
        primaryReserve = Mathf.Min(primaryReserve + kit, PrimaryMaxReserve);
    }
}
