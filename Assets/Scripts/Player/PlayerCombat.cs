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
    // 총알 사거리 = bulletSpeed * bulletLifeTime. 탄속을 30으로 올렸으므로
    // 사거리를 예전과 같은 60으로 유지하려면 수명은 2초여야 한다 (30 * 2 = 60)
    public float bulletLifeTime = 2f;
    public float bulletDamage = 20f;
    public float meleeDamage = 50f;
    public float meleeKnockbackForce = 4f;
    public float meleeKnockbackDuration = 0.5f;
    public float meleeConeAngle = 120f;
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
    public float laserWidth = 0.45f;    // 보이는 굵기 (밝은 바닥에서도 보이게)
    public Color laserColor = new Color(0.1f, 0.55f, 1f, 1f);
    public float laserPierceFalloff = 0.1f; // 적을 하나 뚫을 때마다 다음 적이 받는 피해가 이만큼(비율) 준다
    public float laserMinDamageRatio = 0.3f; // 그래도 이 아래로는 안 내려간다
    LineRenderer laserLine;
    float laserTick; // 다음 탄 소모·피해까지 남은 시간. 떼었다 눌러도 줄지 않아 연타로 DPS를 올릴 수 없다
    bool laserFiring;
    Vector3 laserDir; // 쏘기 시작한 순간의 방향. 떼기 전까지 이 방향으로만 나간다

    public int GrenadeLoaded => grenadeLoaded;
    public int GrenadeReserve => grenadeReserve;
    public int GrenadeCount => grenadeLoaded + grenadeReserve;


    public int primaryMagSize = 30;
    public int laserMagSize = 60;      // 레이저 탄창 (초당 8발 소모 → 7.5초)
    public int primaryMaxReserveMags = 8;
    public int secondaryMagSize = 10;
    public int secondaryMaxReserveMags = 6;
    public float reloadTime = 1f; // 실제로 장전되면 이 시간 동안 원거리 사격 불가 (근접은 가능)
    float reloadEndTime;



    float nextFireTime = 0f;
    float nextGrenadeTime = 0f;
    // 권총(반자동)의 선입력. 쿨다운 중에 누른 클릭을 딱 한 개만 기억한다.
    // bool 한 개라서 열 번을 눌러도 기억은 한 개 — 쿨다운이 풀릴 때 한 발만 나간다
    bool secondaryFireBuffered;
    int primaryAmmo;
    int primaryReserve;   // 예비 탄 (발 단위). 장전은 빈 만큼만 여기서 빼 온다
    int secondaryAmmo;
    int secondaryReserve;

    // 원거리 무기를 마지막으로 발사한 시각. 공격 모션 재생용 (근접은 포함하지 않는다)
    public float LastRangedFireTime { get; private set; } = -999f;
    public float LastMeleeTime { get; private set; } = -999f;

    public int PrimaryAmmo => primaryAmmo;
    public int PrimaryReserve => primaryReserve;
    public int SecondaryAmmo => secondaryAmmo;
    public int SecondaryReserve => secondaryReserve;
    public int PrimaryMagSize => primaryWeapon == PrimaryWeapon.Laser ? laserMagSize : primaryMagSize;
    // 예비탄은 아직 무기 구분 없이 소총 기준(탄창 수 x 30)으로 같이 쓴다
    int PrimaryMaxReserve => primaryMaxReserveMags * primaryMagSize;
    int SecondaryMaxReserve => secondaryMaxReserveMags * secondaryMagSize;

    TopDownMove mover;
    PlayerBound bound;
    PlayerDash dash;

void Awake()
    {
        mover = GetComponent<TopDownMove>();
        bound = GetComponent<PlayerBound>();
        dash = GetComponent<PlayerDash>();
        if (GameSettings.primaryChosen) primaryWeapon = GameSettings.primary;
        primaryAmmo = PrimaryMagSize;
        primaryReserve = PrimaryMaxReserve;
        secondaryAmmo = secondaryMagSize;
        secondaryReserve = SecondaryMaxReserve;

        // 탄약처럼 유탄도 시작할 때 채운다. 씬에 남은 값이 0이면 첫 발이 안 나가버린다
        grenadeLoaded = 1;
        grenadeReserve = grenadeMaxReserve;
    }




    void Update()
    {
        if (Time.timeScale == 0f) return; // 일시정지(확인창·게임오버) 중에는 입력을 받지 않는다 (버튼 클릭이 사격으로 새지 않게)
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

        if (laserFiring) { } // 레이저를 쏘는 동안은 처음 방향을 유지한다 (이동은 된다)
        else if (mouse.rightButton.isPressed)
            AimAtMouse(mouse);
        else if (mover != null && mover.MoveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(mover.MoveDirection);

        laserTick = Mathf.Max(0f, laserTick - Time.deltaTime);
        bool laserMode = currentSlot == WeaponSlot.Primary && primaryWeapon == PrimaryWeapon.Laser;
        UpdateLaser(laserMode && mouse.leftButton.isPressed);

        bool firePressed;
        if (laserMode)
        {
            firePressed = false; // 레이저는 UpdateLaser가 맡는다
        }
        else if (currentSlot == WeaponSlot.Primary)
        {
            firePressed = mouse.leftButton.isPressed; // 주무기는 꾹 누르면 연사
        }
        else if (currentSlot == WeaponSlot.Secondary)
        {
            // 권총은 반자동. 예전엔 쿨다운 중에 누른 클릭이 그냥 버려져서,
            // 실제 연사력이 쿨다운이 아니라 "내 클릭 주기의 배수"로 결정됐다
            // (초당 5번 누르면 오히려 초당 2발로 떨어졌음).
            // 이제 그 클릭을 하나만 기억해 뒀다가 쿨다운이 풀리는 즉시 쏜다.
            if (mouse.leftButton.wasPressedThisFrame) secondaryFireBuffered = true;
            firePressed = secondaryFireBuffered;
        }
        else
        {
            firePressed = mouse.leftButton.wasPressedThisFrame; // 근접은 예전 그대로
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
            laserFiring = false;
            if (laserLine != null) laserLine.enabled = false;
            return;
        }
        if (!laserFiring) { laserFiring = true; laserDir = transform.forward; } // 쏘기 시작 = 방향 고정

        Vector3 dir = laserDir;
        Vector3 origin = transform.position + dir * 0.8f; // 총알과 같은 총구
        float length = LaserLength(origin, dir);

        if (laserLine == null) laserLine = BuildLaserLine();
        laserLine.enabled = true;
        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, origin + dir * length);
        LastRangedFireTime = Time.time; // 공격 모션 유지

        if (laserTick > 0f) return;
        laserTick += 1f / primaryFireRate;
        primaryAmmo--;
        DamageAlongLaser(origin, dir, length, laserDps / primaryFireRate);
        EnemyAI.AlertGunshot(transform.position, gunshotAlertRadius);
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
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<EnemyAI>() == null) continue;
            Health h = hits[i].collider.GetComponentInParent<Health>();
            if (h == null || !struck.Add(h)) continue;
            // 첫 적 100%, 둘째 90%, ... 여덟째부터 30%
            float ratio = Mathf.Max(laserMinDamageRatio, 1f - laserPierceFalloff * (struck.Count - 1));
            h.TakeDamage(damage * ratio);
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
            MeleeAttack();
            LastMeleeTime = Time.time; // 근접 모션이 이 시각을 보고 자세를 바꾼다
            if (isBound) bound.Release(); // meleeing breaks the bind
            nextFireTime = Time.time + meleeCooldown;
            return;
        }

        if (Time.time < reloadEndTime) return; // 장전 중

        if (currentSlot == WeaponSlot.Primary)
        {
            if (primaryAmmo <= 0) return;
            primaryAmmo--;
        }
        else
        {
            if (secondaryAmmo <= 0) return;
            secondaryAmmo--;
        }

        SpawnBullet();
        LastRangedFireTime = Time.time; // 공격 모션이 이 시각을 보고 자세를 바꾼다
        EnemyAI.AlertGunshot(transform.position, gunshotAlertRadius); // 총성에 어그로가 끌린다 (근접은 제외)
        float rate = currentSlot == WeaponSlot.Primary ? (1f / primaryFireRate) : secondaryFireCooldown;
        nextFireTime = Time.time + rate;
    }

    void FireGrenade()
    {
        if (currentSlot != WeaponSlot.Primary) return;      // 주무기 장착 중에만
        if (primaryWeapon == PrimaryWeapon.Laser) return;    // 유탄발사기는 돌격소총에만 달려 있다
        if (dash != null && dash.IsDashing) return;          // 대시 중에는 공격 불가
        if (bound != null && bound.IsActive) return;         // 구속 중에는 원거리 불가
        if (grenadeLoaded <= 0) return; // 비었으면 R로 재장전해야 한다
        if (Time.time < reloadEndTime) return; // 장전 중

        grenadeLoaded--;
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

    void SpawnBullet()
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bullet";
        bullet.transform.position = transform.position + transform.forward * 0.8f;
        bullet.transform.localScale = Vector3.one * 0.2f;
        bullet.GetComponent<Renderer>().material.color = Color.black;

        Collider col = bullet.GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody brb = bullet.AddComponent<Rigidbody>();
        brb.useGravity = false;
        brb.linearVelocity = transform.forward * bulletSpeed;

        Bullet b = bullet.AddComponent<Bullet>();
        b.lifeTime = bulletLifeTime;
        b.damage = bulletDamage;
        b.impactVfx = bulletImpactVfx;
        b.vfxScale = bulletImpactVfxScale;

    }

void MeleeAttack()
    {
        float reach = meleeRange + meleeRadius;
        Collider[] hits = Physics.OverlapSphere(transform.position, reach);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].gameObject == gameObject) continue;

            Vector3 dir = hits[i].transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Angle(transform.forward, dir) > meleeConeAngle * 0.5f) continue;

            // 보스 방어 토템은 근접으로만 부서진다 (Health가 없어 총알·유탄은 통하지 않는다)
            BossTotem totem = hits[i].GetComponent<BossTotem>();
            if (totem != null) { totem.TakeMeleeHit(); continue; }

            Health h = hits[i].GetComponent<Health>();
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
            int take = Mathf.Min(secondaryMagSize - secondaryAmmo, secondaryReserve);
            secondaryReserve -= take;
            secondaryAmmo += take;
            if (take > 0) reloadEndTime = Time.time + reloadTime; // 꽉 찬 상태에서 R은 아무 일도 없다
        }
    }


public void RefillAmmo()
    {
        primaryAmmo = PrimaryMagSize;
        primaryReserve = PrimaryMaxReserve;
        secondaryAmmo = secondaryMagSize;
        secondaryReserve = SecondaryMaxReserve;

        grenadeLoaded = 1;                     // 유탄도 가득 채운다 (장전 1 + 예비)
        grenadeReserve = grenadeMaxReserve;
    }

    // 보급키트: 주무기/보조무기 예비탄을 탄창 하나 분량씩 채운다 (유탄 제외)
    public void AddMagazine()
    {
        primaryReserve = Mathf.Min(primaryReserve + primaryMagSize, PrimaryMaxReserve);
        secondaryReserve = Mathf.Min(secondaryReserve + secondaryMagSize, SecondaryMaxReserve);
    }
}
