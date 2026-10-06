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
        transform.position = focus - rot * Vector3.forward * distance;
        transform.rotation = rot;
    }
}
