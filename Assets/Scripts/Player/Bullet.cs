using UnityEngine;
using CartoonFX;

public class Bullet : MonoBehaviour
{
    public float lifeTime = 3f;
    public float damage = 10f;

    // 착탄 이펙트(CFXR3 Fire Explosion B). 총알은 런타임에 만들어져 인스펙터가 없으므로
    // PlayerCombat이 생성할 때 넣어 준다
    public GameObject impactVfx;
    public float vfxScale = 0.4f;
    public float impactTraceBack = 4f;   // 진입점을 되짚어 찾을 때 거슬러 올라가는 거리
    public float impactSurfaceOffset = 0.1f; // 표면에 딱 붙이면 반쯤 묻히므로 살짝 앞으로 뺀다


    void Start()
    {
        Destroy(gameObject, lifeTime);
    }


void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        Health h = other.GetComponent<Health>();
        bool targetSurvives = true;
        if (h != null)
        {
            h.TakeDamage(damage);
            targetSurvives = h.CurrentHealth > 0f; // 죽은 대상에 붙이면 같이 사라져 연출이 끊긴다
        }

        SpawnImpactVfx(other, targetSurvives);
        Destroy(gameObject);
    }

    // 총알이 맞은 표면 위의 지점.
    // 트리거 판정은 물리 스텝 단위라, OnTriggerEnter가 불릴 땐 총알이 이미 상대를 뚫고
    // 지나간 뒤다 (탄속 30이면 한 스텝에 0.6씩 순간이동한다. 마주 달려오는 몹이면 더 깊이 박힌다).
    // 그래서 총알 위치에 그대로 터뜨리면 폭발이 몹 뒤에서 난다.
    // 날아온 방향으로 거슬러 올라가 맞은 콜라이더의 진입면을 다시 찾는다.
    Vector3 ImpactPoint(Collider hitCollider)
    {
        Vector3 pos = transform.position;
        if (hitCollider == null) return pos;

        Rigidbody rb = GetComponent<Rigidbody>();
        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        if (vel.sqrMagnitude < 0.0001f) return pos;

        Vector3 dir = vel.normalized;
        Ray back = new Ray(pos - dir * impactTraceBack, dir);
        RaycastHit hit;
        // 그 콜라이더 하나만 상대로 쏜다. 중간에 낀 다른 물체에 영향받지 않는다
        if (hitCollider.Raycast(back, out hit, impactTraceBack * 2f))
            return hit.point - dir * impactSurfaceOffset;

        // 표면을 못 찾으면(모서리를 스친 경우 등) 콜라이더에서 가장 가까운 점으로 대체.
        // 단 ClosestPoint는 볼록 콜라이더에서만 쓸 수 있다. 지형처럼 비볼록 메쉬에 쓰면
        // 경고만 뱉고 실패하므로, 그런 경우는 총알 위치를 그대로 쓴다
        MeshCollider mesh = hitCollider as MeshCollider;
        if (mesh != null && !mesh.convex) return pos;

        return hitCollider.ClosestPoint(pos) - dir * impactSurfaceOffset;
    }

    // 실제로 뭔가에 맞았을 때만 터진다. 사거리 끝에서 수명으로 사라지는 총알은
    // Destroy(gameObject, lifeTime)로 조용히 없어지므로 여기를 거치지 않는다.
    // 프리팹의 CFXR_Effect가 clearBehavior=Destroy라 연출 후 알아서 사라진다
    void SpawnImpactVfx(Collider hitCollider, bool attachToTarget)
    {
        if (impactVfx == null) return;

        GameObject fx = Instantiate(impactVfx, ImpactPoint(hitCollider), Quaternion.identity);
        fx.transform.localScale = Vector3.one * Mathf.Max(0.01f, vfxScale);

        // 달려오는 몹을 맞히면, 월드에 고정된 연출은 1.5초 동안 그대로 있는데 몹은 계속 전진해서
        // 결국 폭발이 몹 뒤에 남는다. 맞은 대상에 붙여 같이 움직이게 한다.
        // 다만 그 일격에 죽은 대상에 붙이면 함께 파괴되어 연출이 끊기므로 그때는 월드에 남긴다.
        if (attachToTarget && hitCollider != null)
            fx.transform.SetParent(hitCollider.transform, true); // 월드 크기/위치 유지

        // 총알은 초당 여러 발이 박힌다. 프리팹에 딸린 포인트 라이트가 매 착탄마다 켜지면
        // 바닥이 계속 번쩍이고, 화면 진동까지 겹치면 조준이 어려워진다.
        // 원본 프리팹은 그대로 두고 이 인스턴스에서만 끈다 (유탄 폭발 연출은 영향 없음).
        CFXR_Effect effect = fx.GetComponent<CFXR_Effect>();
        if (effect != null)
        {
            effect.animatedLights = null;                                  // 라이트 애니메이션 중단
            if (effect.cameraShake != null) effect.cameraShake.enabled = false; // 화면 진동
        }

        // 애니메이션만 끄면 라이트가 초기 밝기로 켜진 채 남는다. 컴포넌트 자체를 끈다
        Light[] lights = fx.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
    }
}
