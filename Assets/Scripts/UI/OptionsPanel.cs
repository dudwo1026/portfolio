using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 옵션 창 (타이틀·일시정지 메뉴 공용). 화면 모드: 전체화면 / 창모드 3가지 크기. ↑↓ + Enter, ESC로 닫는다.
// 고른 설정은 Unity가 알아서 기억해 다음 실행에도 그대로 뜬다. 에디터에서는 해상도가 바뀌지 않는다
public class OptionsPanel : MonoBehaviour
{
    static readonly Vector2Int[] WindowSizes = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };

    public static bool IsOpen { get; private set; }
    public static int ClosedFrame { get; private set; } = -1; // 닫은 ESC가 같은 프레임에 일시정지 메뉴까지 닫지 않게

    ConfirmSelector hidden; // 뒤에 깔린 메뉴의 선택기. 열려 있는 동안 화살표·Enter를 가로채지 않게 꺼 둔다
    Font font;
    Text[] labels;
    int openedFrame;

    public static void Open(ConfirmSelector behind)
    {
        if (IsOpen) return;
        IsOpen = true;
        OptionsPanel p = new GameObject("OptionsPanel").AddComponent<OptionsPanel>();
        p.hidden = behind;
        if (behind != null) behind.enabled = false;
        p.Build();
    }

    void Build()
    {
        openedFrame = Time.frameCount;
        foreach (Text t in FindObjectsByType<Text>(FindObjectsSortMode.None)) { font = t.font; if (font != null) break; }

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 450;
        CanvasScaler s = gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        Image shade = Box(root, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
        shade.raycastTarget = true;
        Box(root, Vector2.zero, new Vector2(560f, 560f), new Color(0.06f, 0.06f, 0.08f, 0.95f));
        Label(root, "화면 설정", 44, new Vector2(0f, 220f));

        string[] names = { "전체화면", "창모드  1280 × 720", "창모드  1600 × 900", "창모드  1920 × 1080", "닫기" };
        Button[] buttons = new Button[names.Length];
        labels = new Text[names.Length];
        GameObject list = new GameObject("List", typeof(RectTransform));
        list.SetActive(false); // 선택기를 다 채운 뒤 켠다 (OnEnable에서 버튼을 읽는다)
        list.transform.SetParent(root, false);
        for (int i = 0; i < names.Length; i++)
        {
            float y = 120f - i * 82f - (i == names.Length - 1 ? 20f : 0f); // 닫기는 살짝 띄운다
            Image img = Box((RectTransform)list.transform, new Vector2(0f, y), new Vector2(420f, 64f), new Color(0.13f, 0.13f, 0.16f, 1f));
            img.raycastTarget = true;
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            int k = i;
            b.onClick.AddListener(() => Choose(k));
            buttons[i] = b;
            labels[i] = Label(img.rectTransform, names[i], 30, Vector2.zero);
        }
        ConfirmSelector sel = list.AddComponent<ConfirmSelector>();
        sel.buttons = buttons;
        sel.vertical = true;
        sel.defaultIndex = CurrentIndex();
        sel.selectedFrame = Resources.Load<Sprite>("UI/SelectFrame");
        list.SetActive(true);
        Mark();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.frameCount != openedFrame) Close();
    }

    void Choose(int i)
    {
        if (i == 0) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else if (i <= WindowSizes.Length) Screen.SetResolution(WindowSizes[i - 1].x, WindowSizes[i - 1].y, FullScreenMode.Windowed);
        else { Close(); return; }
        chosen = i;
        Mark();
    }

    int chosen = -1; // 방금 고른 것. Screen 값은 다음 프레임에야 바뀌어서 바로 읽으면 이전 설정이 나온다

    // 지금 화면 설정에 해당하는 줄 (창 크기가 목록에 없으면 전체화면 줄)
    int CurrentIndex()
    {
        if (chosen >= 0) return chosen;
        if (Screen.fullScreenMode != FullScreenMode.Windowed) return 0;
        for (int i = 0; i < WindowSizes.Length; i++)
            if (Screen.width == WindowSizes[i].x && Screen.height == WindowSizes[i].y) return i + 1;
        return 0;
    }

    void Mark()
    {
        int cur = CurrentIndex();
        for (int i = 0; i < labels.Length - 1; i++)
        {
            string t = labels[i].text.TrimStart('●', ' ');
            labels[i].text = i == cur ? "● " + t : t;
            labels[i].color = i == cur ? new Color(1f, 0.82f, 0.32f) : Color.white;
        }
    }

    void Close()
    {
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        if (hidden != null) hidden.enabled = true;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        IsOpen = false; // 씬이 바뀌며 지워져도 남지 않게
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

    Text Label(RectTransform parent, string text, int size, Vector2 pos)
    {
        Text t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(500f, size * 1.6f);
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
