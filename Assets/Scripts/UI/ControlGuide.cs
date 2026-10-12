using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 타이틀에서 새 게임으로 들어오면 일시정지한 채 조작 가이드를 몇 장 보여 준다.
// Enter / 좌클릭 = 다음 장, ESC = 건너뛰기. Canvas에 붙이면 UI는 스스로 만든다.
// 입력은 LateUpdate에서 받는다 — 같은 프레임의 Update(일시정지 메뉴의 ESC, 사격 클릭)가
// 먼저 돌고 난 뒤에 풀어야 닫는 키가 다른 동작으로 새지 않는다
public class ControlGuide : MonoBehaviour
{
    [TextArea(3, 8)]
    public string[] pages =
    {
        // 줄마다 "이름|키". 이름은 오른쪽, 키는 왼쪽으로 맞춘 두 칸으로 나뉘어 줄이 맞는다
        "이동|W  A  S  D\n대시|Space",
        "조준|우클릭 (누르고 있기)\n공격|좌클릭\n유탄 발사|F\n재장전|R\n무기 교체|1  2  3",
        "상호작용|E (누르고 있기)\n소모품|Ctrl",
    };
    public bool onlyFromTitle = true; // 게임 씬을 바로 플레이할 때(테스트)는 띄우지 않는다

    GameObject panel;
    Text title;
    Text names;
    Text keys;
    int page;

    void Start()
    {
        if (onlyFromTitle && !GameSettings.primaryChosen) return;
        Build();
        Show(0);
        Time.timeScale = 0f;
    }

    void LateUpdate()
    {
        if (panel == null || !panel.activeSelf) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        bool next = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (!next) return;
        if (page + 1 < pages.Length) Show(page + 1);
        else Close();
    }

    void Show(int i)
    {
        page = i;
        title.text = "조작 가이드  (" + (i + 1) + " / " + pages.Length + ")";
        string n = "", k = "";
        foreach (string line in pages[i].Split('\n'))
        {
            string[] parts = line.Split('|');
            if (n.Length > 0) { n += "\n"; k += "\n"; }
            n += parts[0];
            k += parts.Length > 1 ? parts[1] : "";
        }
        names.text = n;
        keys.text = k;
    }

    void Close()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panel = new GameObject("ControlGuide", typeof(RectTransform), typeof(Image));
        RectTransform pr = (RectTransform)panel.transform;
        pr.SetParent(transform, false);
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        pr.SetAsLastSibling();

        RectTransform box = NewRect("Box", pr, new Vector2(900f, 480f), Vector2.zero);
        box.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        Outline ol = box.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.6f, 0.6f, 0.65f, 1f);
        ol.effectDistance = new Vector2(3f, -3f);

        title = NewText("Title", box, font, 38, new Vector2(860f, 60f), new Vector2(0f, 180f), TextAnchor.MiddleCenter);
        names = NewText("Names", box, font, 34, new Vector2(300f, 300f), new Vector2(-170f, 0f), TextAnchor.MiddleRight);
        keys = NewText("Keys", box, font, 34, new Vector2(420f, 300f), new Vector2(230f, 0f), TextAnchor.MiddleLeft);
        names.lineSpacing = keys.lineSpacing = 1.3f;
        names.color = new Color(0.75f, 0.85f, 1f, 1f); // 이름은 연한 하늘색, 키는 흰색
        Text hint = NewText("Hint", box, font, 24, new Vector2(860f, 40f), new Vector2(0f, -195f), TextAnchor.MiddleCenter);
        hint.text = "Enter / 좌클릭: 다음      ESC: 건너뛰기";
        hint.color = new Color(0.7f, 0.7f, 0.75f, 1f);
    }

    static RectTransform NewRect(string name, RectTransform parent, Vector2 size, Vector2 pos)
    {
        RectTransform r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent, false);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    static Text NewText(string name, RectTransform parent, Font font, int fontSize, Vector2 size, Vector2 pos, TextAnchor align)
    {
        Text t = NewRect(name, parent, size, pos).gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
