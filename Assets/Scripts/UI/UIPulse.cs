using UnityEngine;
using UnityEngine.UI;

// 버튼을 누르면 그 버튼 모양의 흰 실루엣이 커지면서 옅어지다 사라진다 (펄스 효과).
// 실루엣은 캔버스 맨 위에 따로 만든다. 누르자마자 메뉴가 숨겨져도 효과는 끝까지 보인다
public class UIPulse : MonoBehaviour
{
    float time, duration, endScale;
    Vector3 startScale;
    Image img;
    Color color;

    public static void Play(Graphic target, Canvas canvas, Color color, float duration = 0.45f, float endScale = 1.6f)
    {
        Image src = target as Image;
        // 캔버스는 부르는 쪽이 넘긴다. 메뉴가 방금 숨겨졌으면 target.canvas가 비어 있을 수 있다
        if (canvas == null) return;

        GameObject go = new GameObject("Pulse", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        RectTransform from = target.rectTransform;
        rt.SetParent(canvas.transform, false);
        rt.SetAsLastSibling();
        rt.sizeDelta = from.rect.size;
        rt.position = from.TransformPoint(from.rect.center);
        rt.rotation = from.rotation;
        rt.localScale = Vector3.Scale(from.lossyScale, new Vector3(1f / canvas.transform.lossyScale.x, 1f / canvas.transform.lossyScale.y, 1f));

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        if (src != null) { img.sprite = src.sprite; img.type = src.type; img.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier; }
        img.color = color;

        UIPulse p = go.AddComponent<UIPulse>();
        p.img = img; p.color = color; p.duration = duration; p.endScale = endScale; p.startScale = rt.localScale;
    }

    void Update()
    {
        time += Time.unscaledDeltaTime; // 일시정지 중인 창에서도 돈다
        float t = Mathf.Clamp01(time / duration);
        float e = 1f - (1f - t) * (1f - t); // 처음엔 빠르게 퍼지고 끝에서 느려진다
        transform.localScale = startScale * Mathf.Lerp(1f, endScale, e);
        img.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
        if (t >= 1f) Destroy(gameObject);
    }
}
