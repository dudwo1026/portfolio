using UnityEngine;
using UnityEngine.UI;

// 버튼을 누르면 그 버튼(상자 + 글자)을 그대로 복사한 잔상이 커지면서 옅어지다 사라진다 (펄스 효과).
// 원본은 그대로 두고, 잔상은 캔버스 맨 위에 따로 만든다
public class UIPulse : MonoBehaviour
{
    float time, duration, endScale, startAlpha;
    Vector3 startScale;
    CanvasGroup group;

    public static void Play(RectTransform source, Canvas canvas, float duration = 0.45f, float endScale = 1.6f, float startAlpha = 0.5f)
    {
        // 캔버스는 부르는 쪽이 넘긴다. 메뉴가 방금 숨겨졌으면 source에서 캔버스를 못 찾을 수 있다
        if (canvas == null) return;

        GameObject go = Instantiate(source.gameObject, canvas.transform, false);
        go.name = "Pulse";
        foreach (Selectable s in go.GetComponentsInChildren<Selectable>()) DestroyImmediate(s); // 잔상은 눌리면 안 된다
        // 버튼 색 전환(눌림/고름)이 반쯤 먹은 상태로 복사되면 잔상이 흐리게 나온다. 기본 색으로 되돌린다
        foreach (Graphic gr in go.GetComponentsInChildren<Graphic>()) gr.canvasRenderer.SetColor(Color.white);
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        RectTransform rt = (RectTransform)go.transform;
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = source.rect.size;
        rt.position = source.TransformPoint(source.rect.center);
        rt.rotation = source.rotation;
        Vector3 cs = canvas.transform.lossyScale;
        rt.localScale = new Vector3(source.lossyScale.x / cs.x, source.lossyScale.y / cs.y, 1f);

        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.interactable = false;

        UIPulse p = go.AddComponent<UIPulse>();
        p.group = g; p.duration = duration; p.endScale = endScale; p.startScale = rt.localScale; p.startAlpha = startAlpha;
    }

    void Update()
    {
        time += Time.unscaledDeltaTime; // 일시정지 중인 창에서도 돈다
        float t = Mathf.Clamp01(time / duration);
        float e = 1f - (1f - t) * (1f - t); // 처음엔 빠르게 퍼지고 끝에서 느려진다
        transform.localScale = startScale * Mathf.Lerp(1f, endScale, e);
        group.alpha = startAlpha * (1f - t); // 처음부터 반투명
        if (t >= 1f) Destroy(gameObject);
    }
}
