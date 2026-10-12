using UnityEngine;

// 플레이어를 따라다니는 카메라.
// [ExecuteAlways]라서 에디트 모드에서도 동작한다 — 플레이를 누르지 않고 Game 뷰를 보면서
// 슬라이더를 움직여 각도·거리를 맞출 수 있고, 에디트 모드이므로 그 값이 그대로 저장된다.
// (플레이 중에 맞춘 값은 정지하는 순간 사라진다)
[ExecuteAlways]
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("카메라 위치 · 각도")]
    [Tooltip("어느 방향에서 볼지. 맵을 수평으로 도는 각도")]
    [Range(-180f, 180f)] public float yaw = -135f;

    [Tooltip("내려다보는 각도. 0이면 수평, 90이면 완전한 탑뷰")]
    [Range(5f, 89f)] public float pitch = 56.31f;

    [Tooltip("플레이어까지의 직선 거리. 키울수록 화면에 넓게 담긴다")]
    [Range(2f, 60f)] public float distance = 14.42f;

    [Header("화면 중심 보정")]
    [Tooltip("플레이어를 화면 정중앙이 아닌 곳에 두고 싶을 때. 예: y를 올리면 플레이어가 화면 아래로 내려가 앞이 더 보인다")]
    public Vector3 lookOffset = Vector3.zero;

    [Header("화면 흔들림")]
    [Tooltip("초당 흔들리는 횟수 정도. 높을수록 잘게 떤다")]
    public float shakeFrequency = 22f;

    float shakeAmplitude;  // 이번 흔들림의 처음 세기 (월드 단위)
    float shakeDuration;
    float shakeTimeLeft;
    float shakeSeed;

    // 폭발 등에서 부른다. 세기는 화면 평면 위로 카메라가 밀리는 거리.
    // 이미 흔들리는 중이면 더 센 쪽을 남긴다 (유탄이 연달아 터져도 누적돼 폭주하지 않게)
    public static void Shake(float amplitude, float duration)
    {
        Camera cam = Camera.main;
        CameraFollow f = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (f == null || duration <= 0f) return;

        float remaining = f.shakeTimeLeft > 0f ? f.shakeAmplitude * (f.shakeTimeLeft / f.shakeDuration) : 0f;
        if (amplitude < remaining) return;
        f.shakeAmplitude = amplitude;
        f.shakeDuration = duration;
        f.shakeTimeLeft = duration;
        f.shakeSeed = Random.Range(0f, 100f);
    }

    void LateUpdate()
    {
        Apply();
    }

#if UNITY_EDITOR
    // [ExecuteAlways]의 LateUpdate는 에디터가 화면을 다시 그릴 때만 돈다.
    // 그것만 믿으면 슬라이더를 움직여도 카메라가 그대로인 경우가 생기므로,
    // 에디트 모드에서는 에디터 루프에 직접 붙어 매 틱 갱신한다
    void OnEnable()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.update += EditorTick;
    }

    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
    }

    void EditorTick()
    {
        if (this == null) return;   // 삭제된 뒤 남은 구독 방지
        if (Application.isPlaying) return;
        Apply();
    }
#endif

    void Apply()
    {
        if (target == null) return;

        Vector3 focus = target.position + lookOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focus - rot * Vector3.forward * distance + ShakeOffset(rot);
        transform.rotation = rot;
    }

    // 화면 가로·세로로만 민다 (앞뒤로 밀면 확대·축소처럼 보인다). 끝으로 갈수록 약해진다
    Vector3 ShakeOffset(Quaternion rot)
    {
        if (!Application.isPlaying || shakeTimeLeft <= 0f) return Vector3.zero;

        shakeTimeLeft -= Time.deltaTime;
        if (shakeTimeLeft <= 0f) return Vector3.zero;

        float k = shakeAmplitude * (shakeTimeLeft / shakeDuration);
        float t = Time.time * shakeFrequency;
        float x = (Mathf.PerlinNoise(shakeSeed, t) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(shakeSeed + 37f, t) - 0.5f) * 2f;
        return (rot * Vector3.right * x + rot * Vector3.up * y) * k;
    }
}
