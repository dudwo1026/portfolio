using UnityEngine;

// 2D 일러스트를 3D 씬의 캐릭터로 세우기 위한 판때기.
// 카메라가 고정된 각도로 내려다보기 때문에 방식이 둘로 갈린다.
//
//  Upright    : 스프라이트가 월드에서 똑바로 서고, 카메라 쪽으로 좌우(Y축)로만 돈다.
//               발이 바닥에 제대로 붙는다. 대신 카메라가 내려다보는 만큼 세로로 눌려 보여서
//               그만큼 키를 늘려 주는 보정이 필요하다.
//  FaceCamera : 카메라를 정면으로 마주 본다. 그림이 왜곡 없이 그려진 그대로 보인다.
//               대신 판이 눕는 셈이라 바닥을 파고들 수 있다.
[ExecuteAlways]
public class SpriteBillboard : MonoBehaviour
{
    public enum Mode { Upright, FaceCamera }

    public Mode mode = Mode.Upright;
    [Tooltip("비워 두면 Camera.main을 쓴다")]
    public Camera targetCamera;

    void LateUpdate()
    {
        Apply();
    }

    void Apply()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        // 유니티 기본 Quad는 앞면이 -Z 쪽을 향한다.
        // 즉 이 오브젝트의 forward를 '카메라에서 멀어지는 방향'으로 맞추면 정면으로 보인다
        if (mode == Mode.FaceCamera)
        {
            transform.rotation = cam.transform.rotation;
            return;
        }

        Vector3 flat = cam.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.000001f) return;
        transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

#if UNITY_EDITOR
    // 에디트 모드에서도 Game 뷰로 바로 확인할 수 있게 에디터 루프에 붙는다
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
        if (this == null) return;
        if (Application.isPlaying) return;
        Apply();
    }
#endif
}
