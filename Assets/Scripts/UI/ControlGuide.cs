using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 타이틀에서 새 게임으로 들어오면 일시정지한 채 조작 가이드를 몇 장 보여 준다.
// Space = 다음 장, ESC = 건너뛰기. Canvas에 붙이면 UI는 스스로 만든다.
// 입력은 LateUpdate에서 받는다 — 같은 프레임의 Update(일시정지 메뉴의 ESC, 사격 클릭)가
// 먼저 돌고 난 뒤에 풀어야 닫는 키가 다른 동작으로 새지 않는다
public class ControlGuide : MonoBehaviour
{
    [TextArea(3, 8)]
    public string[] pages =
    {
        // 줄마다 "이름|내용". 내용의 [키]는 키캡으로, [LMB]/[RMB]는 마우스 그림으로, 나머지는 글자로 그린다
        "이동|[W][A][S][D]\n대시|[Space]",
        "조준|[RMB] 누르고 있기\n공격|[LMB]\n유탄 발사|[F]\n재장전|[R]\n무기 교체|[1][2][3]",
        "상호작용|[E] 누르고 있기\n소모품|[Ctrl]",
    };
    // 조작 가이드 뒤에 이어지는 진행 가이드 한 장. 줄마다 그림(있으면) + 오른쪽 설명
    [TextArea(2, 4)]
    public string[] storyPages =
    {
        "몬스터를 쓰러뜨리면 우상단의\n<color=#FFC83C>혼돈 게이지</color>가 누적됩니다.",
        "게이지가 누적될 때마다\n맵 어딘가에 <color=#FFC83C>열쇠</color>가 등장합니다.",
        "<color=#FFC83C>혼돈 게이지 최대치</color> + <color=#FFC83C>모든 열쇠 수집</color>\n시 옥상으로 향하는 문이 열립니다.",
        "옥상에서 당신을 기다리고 있는 <color=#FF5A4A>보스</color>를 마주하고 쓰러뜨리세요.",
    };
    public Sprite[] storyImages; // storyPages와 같은 순서. 비어 있으면 글만

    public bool onlyFromTitle = true; // 게임 씬을 바로 플레이할 때(테스트)는 띄우지 않는다
    // 뜬 직후 이 시간(실시간) 동안은 입력을 무시한다. 무기 선택 화면에서 연타한 Enter·클릭이
    // 씬이 넘어오자마자 처리돼 가이드를 넘겨 버리는 것을 막는다
    public float inputDelay = 0.5f;
    float openedAt;

    const float RowHeight = 74f;
    const float KeyHeight = 60f;
    const float Gap = 10f;

    static readonly Vector2 ControlBoxSize = new Vector2(940f, 560f);
    static readonly Vector2 StoryBoxSize = new Vector2(1700f, 680f); // 사진 세 장을 가로로 크게 놓는다

    GameObject panel;
    RectTransform box, body;
    Text title;
    Font font;
    Sprite keycap, mouseLeft, mouseRight, arrowHead;
    readonly List<Texture2D> textures = new List<Texture2D>();
    int page;

    void Start()
    {
        if (onlyFromTitle && !GameSettings.primaryChosen) return;
        if (ZoneSave.Retrying) return; // 구획 재시도로 다시 불러온 경우는 이미 본 가이드다
        Build();
        Show(0);
        Time.timeScale = 0f;
        openedAt = Time.unscaledTime;
    }

    void OnDestroy()
    {
        foreach (Texture2D t in textures) if (t != null) Destroy(t);
    }

    void LateUpdate()
    {
        if (panel == null || !panel.activeSelf) return;
        if (Time.unscaledTime < openedAt + inputDelay) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        // 대시도 Space지만 일시정지 중엔 입력을 안 받고, 여기서 닫는 건 그 Update 뒤라 새지 않는다
        if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;
        if (page < pages.Length) Show(page + 1); // 마지막 조작 장 다음은 진행 가이드
        else Close();
    }

    // 0..pages-1 = 조작 가이드, 마지막 한 장 = 진행 가이드
    void Show(int i)
    {
        page = i;
        for (int c = body.childCount - 1; c >= 0; c--) Destroy(body.GetChild(c).gameObject);

        if (i >= pages.Length) { ShowStory(); return; }
        box.sizeDelta = ControlBoxSize;
        title.text = "조작 가이드  (" + (i + 1) + " / " + pages.Length + ")";

        string[] lines = pages[i].Split('\n');
        float y = (lines.Length - 1) * RowHeight * 0.5f;
        foreach (string line in lines)
        {
            string[] parts = line.Split('|');
            Text name = NewText("Name", body, 34, new Vector2(360f, RowHeight), new Vector2(-240f, y), TextAnchor.MiddleRight);
            name.text = parts[0];
            name.color = new Color(0.75f, 0.85f, 1f, 1f);
            if (parts.Length > 1) BuildRow(parts[1], y);
            y -= RowHeight;
        }
    }

    // 진행 가이드 한 장. 마지막 줄을 뺀 단계들은 윗줄에 가로로 늘어놓고 → 로 잇는다.
    // 칸마다 위에 그림(액자 FrameW x FrameH 안에 비율 유지), 아래에 설명.
    // 마지막 줄(보스)은 그 바로 아래 가운데 한 줄
    void ShowStory()
    {
        box.sizeDelta = StoryBoxSize;
        title.text = "게임 진행";
        const float spacing = 530f, frameW = 460f, frameH = 220f, picY = 110f, textY = -65f, finalY = -160f;
        int steps = storyPages.Length - 1;
        float firstX = -(steps - 1) * spacing * 0.5f;

        for (int s = 0; s < steps; s++)
        {
            float x = firstX + s * spacing;
            Image frame = NewImage("Frame", null);
            frame.color = new Color(0f, 0f, 0f, 0.35f);
            frame.rectTransform.sizeDelta = new Vector2(frameW, frameH);
            frame.rectTransform.anchoredPosition = new Vector2(x, picY);
            Outline o = frame.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.6f, 0.6f, 0.65f, 1f);
            o.effectDistance = new Vector2(2f, -2f);

            Sprite img = storyImages != null && s < storyImages.Length ? storyImages[s] : null;
            if (img != null)
            {
                float scale = Mathf.Min(frameW / img.rect.width, frameH / img.rect.height);
                Image pic = NewImage("Picture", img);
                pic.rectTransform.sizeDelta = new Vector2(img.rect.width * scale, img.rect.height * scale);
                pic.rectTransform.anchoredPosition = new Vector2(x, picY);
            }

            Text t = NewText("Story", body, 26, new Vector2(spacing - 10f, 80f), new Vector2(x, textY), TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow; // 줄이 넘쳐도 잘리지 않게
            t.text = storyPages[s];

            // 다음 칸으로: 액자 사이 틈 가운데에 →
            if (s + 1 < steps) Arrow(new Vector2(x + spacing * 0.5f - 25f, picY), new Vector2(x + spacing * 0.5f + 25f, picY));
        }

        Text fin = NewText("Final", body, 32, new Vector2(1500f, 60f), new Vector2(0f, finalY), TextAnchor.MiddleCenter);
        fin.text = storyPages[steps];
    }

    static readonly Color ArrowColor = new Color(0.75f, 0.85f, 1f, 1f);
    const float ArrowThick = 5f, HeadSize = 24f;

    // from → to. 촉 끝이 to에 닿는다
    void Arrow(Vector2 from, Vector2 to)
    {
        Vector2 dir = (to - from).normalized;
        Segment(from, to - dir * (HeadSize - 4f));
        Image head = NewImage("ArrowHead", arrowHead);
        head.color = ArrowColor;
        RectTransform r = head.rectTransform;
        r.sizeDelta = new Vector2(HeadSize, HeadSize);
        r.anchoredPosition = to - dir * HeadSize * 0.5f;
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // 가로·세로 선분. 끝을 두께 절반만큼 늘려 꺾이는 곳이 이어져 보이게 한다
    void Segment(Vector2 a, Vector2 b)
    {
        Image line = NewImage("ArrowLine", null);
        line.color = ArrowColor;
        line.rectTransform.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + ArrowThick, Mathf.Abs(b.y - a.y) + ArrowThick);
        line.rectTransform.anchoredPosition = (a + b) * 0.5f;
    }

    // "[W][A] 누르고 있기" → 키캡, 키캡, 글자. 이름 칸 오른쪽(-20)부터 왼쪽 정렬로 늘어놓는다
    void BuildRow(string content, float y)
    {
        float x = -20f;
        int pos = 0;
        while (pos < content.Length)
        {
            if (content[pos] == '[')
            {
                int end = content.IndexOf(']', pos);
                if (end < 0) end = content.Length;
                x += AddKey(content.Substring(pos + 1, end - pos - 1), x, y) + Gap;
                pos = end + 1;
            }
            else
            {
                int end = content.IndexOf('[', pos);
                if (end < 0) end = content.Length;
                string word = content.Substring(pos, end - pos).Trim();
                if (word.Length > 0)
                {
                    Text t = NewText("Label", body, 28, new Vector2(400f, RowHeight), Vector2.zero, TextAnchor.MiddleLeft);
                    t.text = word;
                    t.color = new Color(0.8f, 0.8f, 0.85f, 1f);
                    float w = t.preferredWidth;
                    Place(t.rectTransform, x, y, w, RowHeight);
                    x += w + Gap;
                }
                pos = end;
            }
        }
    }

    // 키 하나를 그리고 그 폭을 돌려준다
    float AddKey(string key, float x, float y)
    {
        if (key == "LMB" || key == "RMB")
        {
            Image m = NewImage("Mouse", key == "LMB" ? mouseLeft : mouseRight);
            Place(m.rectTransform, x, y, 46f, 69f);
            return 46f;
        }

        float w = key.Length <= 1 ? KeyHeight : key == "Space" ? 190f : 34f + 20f * key.Length;
        Image cap = NewImage("Key", keycap);
        cap.type = Image.Type.Sliced;
        Place(cap.rectTransform, x, y, w, KeyHeight);

        Text label = NewText("Label", cap.rectTransform, 30, new Vector2(w, KeyHeight - 6f), new Vector2(0f, 3f), TextAnchor.MiddleCenter);
        label.text = key;
        label.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        return w;
    }

    static void Place(RectTransform r, float left, float centerY, float w, float h)
    {
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(left, centerY);
    }

    void Close()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keycap = KeycapSprite();
        mouseLeft = MouseSprite(true);
        mouseRight = MouseSprite(false);
        arrowHead = ArrowHeadSprite();

        panel = new GameObject("ControlGuide", typeof(RectTransform), typeof(Image));
        RectTransform pr = (RectTransform)panel.transform;
        pr.SetParent(transform, false);
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        pr.SetAsLastSibling();

        box = NewRect("Box", pr, ControlBoxSize, Vector2.zero);
        box.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        Outline ol = box.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.6f, 0.6f, 0.65f, 1f);
        ol.effectDistance = new Vector2(3f, -3f);

        // 제목은 위, 안내는 아래 가장자리에 붙인다 — 진행 가이드에서 창 높이가 달라져도 따라간다
        title = NewText("Title", box, 38, new Vector2(860f, 60f), new Vector2(0f, -50f), TextAnchor.MiddleCenter);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        body = NewRect("Body", box, new Vector2(860f, 340f), new Vector2(0f, 5f));
        Text hint = NewText("Hint", box, 24, new Vector2(860f, 40f), new Vector2(0f, 40f), TextAnchor.MiddleCenter);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.text = "Space: 다음      ESC: 건너뛰기";
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

    Text NewText(string name, RectTransform parent, int fontSize, Vector2 size, Vector2 pos, TextAnchor align)
    {
        Text t = NewRect(name, parent, size, pos).gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    Image NewImage(string name, Sprite sprite)
    {
        Image img = NewRect(name, body, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // ── 그림은 에셋 없이 런타임에 그린다 ──────────────────────────────

    // 둥근 사각형의 부호 거리 (안쪽이 음수)
    static float RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float qx = Mathf.Abs(x - cx) - ((x1 - x0) * 0.5f - r);
        float qy = Mathf.Abs(y - cy) - ((y1 - y0) * 0.5f - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static Color Over(Color under, Color top, float a)
    {
        return Color.Lerp(under, new Color(top.r, top.g, top.b, Mathf.Max(under.a, top.a)), Mathf.Clamp01(a));
    }

    // 키캡: 어두운 받침 위에 밝은 윗면, 아래쪽이 두꺼워 눌러 볼 수 있는 키처럼 보인다. 9-slice로 폭을 늘린다
    Sprite KeycapSprite()
    {
        const int S = 64;
        Texture2D tex = NewTexture(S, S);
        Color baseC = new Color(0.32f, 0.33f, 0.37f, 1f);
        Color faceC = new Color(0.93f, 0.93f, 0.96f, 1f);
        Color[] px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                Color c = Color.clear;
                c = Over(c, baseC, 0.5f - RoundRect(fx, fy, 0f, 0f, S, S, 12f));
                c = Over(c, faceC, 0.5f - RoundRect(fx, fy, 3f, 9f, S - 3f, S - 3f, 10f));
                px[y * S + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 16f, 14f, 14f));
    }

    // 마우스: 흰 테두리 몸통, 위쪽 절반을 좌우 버튼으로 나누고 누르는 쪽만 금색
    Sprite MouseSprite(bool left)
    {
        const int W = 64, H = 96;
        Texture2D tex = NewTexture(W, H);
        Color edge = new Color(0.9f, 0.9f, 0.94f, 1f);
        Color fill = new Color(0.16f, 0.16f, 0.2f, 1f);
        Color hot = new Color(1f, 0.75f, 0.1f, 1f);
        const float split = 54f; // 버튼과 몸통의 경계 높이
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                Color c = Color.clear;
                c = Over(c, edge, 0.5f - RoundRect(fx, fy, 2f, 2f, W - 2f, H - 2f, 28f));
                float inner = RoundRect(fx, fy, 7f, 7f, W - 7f, H - 7f, 23f);
                bool onLine = fy > split - 2f && fy < split + 2f || (fy > split && Mathf.Abs(fx - W * 0.5f) < 2f);
                bool button = fy > split && (left ? fx < W * 0.5f : fx > W * 0.5f);
                if (!onLine) c = Over(c, button ? hot : fill, 0.5f - inner);
                px[y * W + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }

    // 화살촉: 오른쪽을 가리키는 삼각형. 돌려서 다른 방향에도 쓴다
    Sprite ArrowHeadSprite()
    {
        const int S = 32;
        Texture2D tex = NewTexture(S, S);
        Color[] px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = Mathf.Abs(y + 0.5f - S * 0.5f);
                // 왼쪽 변(x=2)과 두 빗변 (2,±15)→(30,0) 안쪽까지의 거리. 1px만 부드럽게
                float slant = ((30f - fx) * 15f / 28f - fy) * 28f / Mathf.Sqrt(28f * 28f + 15f * 15f);
                float a = Mathf.Clamp01(Mathf.Min(fx - 2f, slant) + 0.5f);
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    Texture2D NewTexture(int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        textures.Add(t);
        return t;
    }
}
