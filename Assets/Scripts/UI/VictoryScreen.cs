using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 보스 처치 결과창. 처치 연출(사망 모션)이 끝나면 BossDefeat가 부른다. 씬에 미리 둘 것 없이 스스로 캔버스를 만든다.
// 순서: 화면이 어두워짐 → 가운데 띠가 위아래로 열림 → 금빛 "VICTORY"가 크게 내려앉으며 뒤로 빛이 번짐 →
// 기록이 한 줄씩 옆에서 밀려 들어오고 숫자는 0부터 올라감 → 마지막에 버튼.
// 결과창이 뜨면 시간이 멈추므로 전부 unscaled 시간으로 돈다
public class VictoryScreen : MonoBehaviour
{
    static readonly Color Gold = new Color(1f, 0.82f, 0.32f);
    static readonly Color Dim = new Color(0.72f, 0.72f, 0.78f);

    public static string titleSceneName = "TitleScene";

    float clearTime;
    Font font;
    Sprite frame, glowSprite;
    RectTransform root;

    // delay: 사망 모션이 끝날 때까지(게임 시간) 기다렸다가 띄운다
    public static void Show(float delay, float clearTime)
    {
        VictoryScreen v = new GameObject("VictoryScreen").AddComponent<VictoryScreen>();
        v.clearTime = clearTime;
        v.frame = Resources.Load<Sprite>("UI/SelectFrame"); // 타이틀 메뉴와 같은 고른 테두리
        v.StartCoroutine(v.Run(delay));
    }

    IEnumerator Run(float delay)
    {
        yield return new WaitForSeconds(delay);
        Time.timeScale = 0f;
        LockPlayer();
        Build();

        // 1) 어두워진다
        Image shade = Panel("Shade", root, Vector2.zero, new Color(0f, 0f, 0f, 0f), true);
        yield return Tween(0.6f, k => shade.color = new Color(0f, 0f, 0f, 0.72f * k));

        // 2) 가운데 띠가 위아래로 열린다
        Image band = Panel("Band", root, new Vector2(0f, 0f), new Color(0.05f, 0.04f, 0.09f, 0.92f), false);
        band.rectTransform.sizeDelta = new Vector2(2400f, 0f);
        Image lineTop = Panel("LineTop", band.rectTransform, Vector2.zero, Gold, false);
        Image lineBottom = Panel("LineBottom", band.rectTransform, Vector2.zero, Gold, false);
        yield return Tween(0.35f, k =>
        {
            float h = 760f * Ease(k);
            band.rectTransform.sizeDelta = new Vector2(2400f, h);
            lineTop.rectTransform.sizeDelta = lineBottom.rectTransform.sizeDelta = new Vector2(1500f * Ease(k), 3f);
            lineTop.rectTransform.anchoredPosition = new Vector2(0f, h * 0.5f);
            lineBottom.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.5f);
        });

        // 3) VICTORY: 크게 떠 있다가 내려앉고, 뒤에서 금빛이 번진다
        Image glow = Panel("Glow", root, new Vector2(0f, 205f), new Color(Gold.r, Gold.g, Gold.b, 0f), false);
        glow.sprite = glowSprite;
        glow.rectTransform.sizeDelta = new Vector2(1100f, 420f);
        Text title = Label("Title", root, "VICTORY", 150, Gold, new Vector2(0f, 205f), TextAnchor.MiddleCenter);
        Outline o = title.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.35f, 0.2f, 0f, 0.9f);
        o.effectDistance = new Vector2(4f, -4f);
        Text sub = Label("Sub", root, "보 스  처 치", 34, Dim, new Vector2(0f, 112f), TextAnchor.MiddleCenter);
        sub.color = new Color(Dim.r, Dim.g, Dim.b, 0f);
        yield return Tween(0.4f, k =>
        {
            float e = Ease(k);
            title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, e);
            title.color = new Color(Gold.r, Gold.g, Gold.b, k);
            glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f * e);
        });
        StartCoroutine(Pulse(glow));
        yield return Tween(0.3f, k => sub.color = new Color(Dim.r, Dim.g, Dim.b, k));

        // 4) 기록: 한 줄씩 왼쪽에서 밀려 들어온다. 숫자는 0부터 올라간다
        string[] names = { "난이도", "주무기", "클리어 시간", "처치한 적", "처치한 정예" };
        int kills = EnemyAI.Kills, elites = EnemyAI.EliteKills;
        string[] values = { DifficultyName(), WeaponName(), TimeText(clearTime), kills.ToString(), elites.ToString() };
        for (int i = 0; i < names.Length; i++)
        {
            float y = 30f - i * 50f;
            Text n = Label("Stat" + i, root, names[i], 30, Dim, new Vector2(-40f, y), TextAnchor.MiddleRight);
            Text v = Label("Value" + i, root, values[i], 34, Color.white, new Vector2(40f, y), TextAnchor.MiddleLeft);
            int row = i;
            StartCoroutine(Tween(0.3f, k =>
            {
                float e = Ease(k);
                n.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-140f, -40f, e), y);
                v.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(140f, 40f, e), y);
                n.color = new Color(Dim.r, Dim.g, Dim.b, k);
                v.color = new Color(1f, 1f, 1f, k);
            }));
            if (row == 2) StartCoroutine(Tween(0.8f, k => v.text = TimeText(clearTime * Ease(k))));
            if (row == 3) StartCoroutine(Tween(0.8f, k => v.text = Mathf.RoundToInt(kills * Ease(k)).ToString()));
            if (row == 4) StartCoroutine(Tween(0.8f, k => v.text = Mathf.RoundToInt(elites * Ease(k)).ToString()));
            yield return new WaitForSecondsRealtime(0.18f);
        }
        yield return new WaitForSecondsRealtime(0.7f);

        // 5) 버튼
        GameObject bar = new GameObject("Buttons", typeof(RectTransform));
        bar.SetActive(false); // 선택기를 다 채운 뒤 켠다 (OnEnable에서 버튼을 읽는다)
        RectTransform br = (RectTransform)bar.transform;
        br.SetParent(root, false);
        br.anchoredPosition = new Vector2(0f, -285f);
        Button toTitle = MakeButton(br, "타이틀로", new Vector2(-180f, 0f), ToTitle);
        Button quit = MakeButton(br, "게임 종료", new Vector2(180f, 0f), Quit);
        ConfirmSelector sel = bar.AddComponent<ConfirmSelector>();
        sel.buttons = new[] { toTitle, quit };
        sel.defaultIndex = 0;
        sel.selectedFrame = frame;
        sel.framePadding = 8f;
        sel.pressFlash = true;
        CanvasGroup cg = bar.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        bar.SetActive(true);
        yield return Tween(0.4f, k => cg.alpha = k);
    }

    void Build()
    {
        foreach (Text t in FindObjectsByType<Text>(FindObjectsSortMode.None)) { font = t.font; if (font != null) break; }
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        glowSprite = BuildGlow();

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 400;
        CanvasScaler s = gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
    }

    // 결과창 동안 쏘거나 움직이지 않게
    static void LockPlayer()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return;
        foreach (Behaviour b in new Behaviour[] { p.GetComponent<PlayerCombat>(), p.GetComponent<PlayerDash>(), p.GetComponent<PlayerConsumables>() })
            if (b != null) b.enabled = false;
        TopDownMove m = p.GetComponent<TopDownMove>();
        if (m != null) m.movementLocked = true;
    }

    Image Panel(string name, RectTransform parent, Vector2 pos, Color color, bool stretch)
    {
        Image img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        RectTransform rt = img.rectTransform;
        rt.SetParent(parent, false);
        if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        else rt.anchoredPosition = pos;
        img.color = color;
        img.raycastTarget = stretch; // 어두운 막이 뒤쪽 HUD 클릭을 막는다
        return img;
    }

    Text Label(string name, RectTransform parent, string text, int size, Color color, Vector2 pos, TextAnchor align)
    {
        Text t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        RectTransform rt = t.rectTransform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(align == TextAnchor.MiddleCenter ? 1400f : 420f, size * 1.6f);
        rt.pivot = new Vector2(align == TextAnchor.MiddleRight ? 1f : align == TextAnchor.MiddleLeft ? 0f : 0.5f, 0.5f);
        rt.anchoredPosition = pos;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = new Color(color.r, color.g, color.b, 0f);
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Image img = Panel(text, parent, pos, new Color(0.11f, 0.11f, 0.13f, 0.9f), false);
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = new Vector2(300f, 70f);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        Text t = Label("Label", img.rectTransform, text, 30, Color.white, Vector2.zero, TextAnchor.MiddleCenter);
        t.rectTransform.sizeDelta = new Vector2(300f, 70f);
        t.color = Color.white;
        return b;
    }

    void ToTitle()
    {
        Time.timeScale = 1f;
        ZoneSave.Clear(); // 깬 판의 구획 세이브는 지운다
        SceneManager.LoadScene(titleSceneName);
    }

    void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator Pulse(Image glow)
    {
        for (float t = 0f; glow != null; t += Time.unscaledDeltaTime)
        {
            glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.7f + 0.2f * Mathf.Sin(t * 2.2f));
            yield return null;
        }
    }

    static IEnumerator Tween(float time, System.Action<float> step)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { step(t / time); yield return null; }
        step(1f);
    }

    static float Ease(float k) { return 1f - (1f - k) * (1f - k) * (1f - k); }

    static string TimeText(float s)
    {
        int total = Mathf.FloorToInt(s);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    static string DifficultyName() { return GameSettings.difficulty == Difficulty.Normal ? "노말" : "하드"; }

    static string WeaponName()
    {
        switch (GameSettings.primary)
        {
            case PrimaryWeapon.Laser: return "레이저";
            case PrimaryWeapon.Shotgun: return "산탄총";
            default: return "돌격소총";
        }
    }

    // 가운데가 진하고 가장자리로 부드럽게 사라지는 타원 빛 (VICTORY 뒤)
    static Sprite BuildGlow()
    {
        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }
}
