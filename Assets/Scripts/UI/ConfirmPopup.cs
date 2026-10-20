using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 예/아니오 재확인 창. ←→ + Enter, ESC = 아니오. 뒤에 깔린 선택기는 열려 있는 동안 꺼 둔다
public class ConfirmPopup : MonoBehaviour
{
    ConfirmSelector behind;
    System.Action onYes, onNo;
    Font font;
    int openedFrame;

    public static void Show(string message, System.Action onYes, ConfirmSelector behind, System.Action onNo = null)
    {
        ConfirmPopup p = new GameObject("ConfirmPopup").AddComponent<ConfirmPopup>();
        p.onYes = onYes;
        p.onNo = onNo;
        p.behind = behind;
        if (behind != null) behind.enabled = false;
        p.Build(message);
    }

    void Build(string message)
    {
        openedFrame = Time.frameCount;
        foreach (Text t in FindObjectsByType<Text>(FindObjectsSortMode.None)) { font = t.font; if (font != null) break; }

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 460;
        CanvasScaler s = gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        Image shade = Box(root, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
        shade.raycastTarget = true; // 뒤의 카드가 마우스로 눌리지 않게
        Box(root, Vector2.zero, new Vector2(720f, 320f), new Color(0.06f, 0.06f, 0.08f, 0.95f));
        Label(root, message, 36, new Vector2(0f, 60f), 660f);

        GameObject row = new GameObject("Buttons", typeof(RectTransform));
        row.SetActive(false); // 선택기를 다 채운 뒤 켠다 (OnEnable에서 버튼을 읽는다)
        row.transform.SetParent(root, false);
        Button yes = MakeButton((RectTransform)row.transform, "예", new Vector2(-150f, -70f), () => Close(true));
        Button no = MakeButton((RectTransform)row.transform, "아니오", new Vector2(150f, -70f), () => Close(false));
        ConfirmSelector sel = row.AddComponent<ConfirmSelector>();
        sel.buttons = new[] { yes, no };
        sel.defaultIndex = 0;
        sel.selectedFrame = Resources.Load<Sprite>("UI/SelectFrame");
        row.SetActive(true);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.frameCount != openedFrame) Close(false);
    }

    void Close(bool accepted)
    {
        if (!accepted && behind != null) behind.enabled = true; // 예면 다음 화면으로 넘어가니 뒤 선택기는 그대로 둔다
        Destroy(gameObject);
        if (accepted && onYes != null) onYes();
        if (!accepted && onNo != null) onNo();
    }

    Button MakeButton(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Image img = Box(parent, pos, new Vector2(240f, 64f), new Color(0.13f, 0.13f, 0.16f, 1f));
        img.raycastTarget = true;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        Label(img.rectTransform, text, 30, Vector2.zero, 240f);
        return b;
    }

    Image Box(RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        Image img = new GameObject("Box", typeof(RectTransform)).AddComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Label(RectTransform parent, string text, int size, Vector2 pos, float width)
    {
        Text t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(width, size * 1.6f);
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }
}
