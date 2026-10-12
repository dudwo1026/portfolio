using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 홀드 진행도를 가진 것. 보급 장판들이 이걸 구현하면 캐스팅바가 저절로 따라온다
public interface IChargePad
{
    float Progress { get; }  // 0~1
    Color BarColor { get; }  // 캐스팅바 색. 보통 장판 자기 색을 그대로 돌려준다
    string Prompt { get; }   // 상호작용 가능 거리 안이면 안내 문구, 아니면 null
}

// 보급 장판 공통 캐스팅바. E를 누르고 있는 동안 플레이어 머리 위에 원형 게이지가 찬다.
//
// 장판마다 UI를 따로 두지 않는다. IChargePad를 구현한 것들 중 진행 중인 것 하나를 골라 그린다 —
// 두 장판이 겹칠 일이 없으므로 가장 많이 찬 것 하나만 보여주면 된다.
public class ChargeCastBarUI : MonoBehaviour
{
    public Camera viewCamera;
    public Transform player;
    public float headHeight = 2.4f;   // 플레이어 머리 위 이만큼에 띄운다
    public float size = 86f;          // 캔버스 단위 지름
    public float ringInnerRatio = 0.76f;

    [Header("색")]
    public Color trackColor = new Color(0f, 0f, 0f, 0.55f);        // 배경 링
    public Color fallbackFillColor = new Color(0.45f, 1f, 0.55f, 1f); // 장판이 색을 안 주면 쓰는 값
    public float fillAlpha = 0.95f;

    public int promptFontSize = 24;

    RectTransform container;
    RectTransform root;
    Text prompt; // 상호작용 거리 안에 들어가면 "[E] ..." 안내. 링 바로 위
    Image track;
    Image fill;
    Sprite ringSprite;

    readonly List<IChargePad> pads = new List<IChargePad>();

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (player == null)
        {
            TopDownMove mover = FindFirstObjectByType<TopDownMove>();
            if (mover != null) player = mover.transform;
        }

        ringSprite = BuildRingSprite(ringInnerRatio);
        Build();
        CollectPads();
    }

    void OnDestroy()
    {
        if (ringSprite != null)
        {
            if (ringSprite.texture != null) Destroy(ringSprite.texture);
            Destroy(ringSprite);
        }
    }

    // 인터페이스는 FindObjectsByType으로 못 찾는다. MonoBehaviour를 한 번만 훑어 모아 둔다
    void CollectPads()
    {
        pads.Clear();
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            IChargePad p = all[i] as IChargePad;
            if (p != null) pads.Add(p);
        }
    }

    void Build()
    {
        GameObject c = new GameObject("ChargeCastBar", typeof(RectTransform));
        container = (RectTransform)c.transform;
        container.SetParent(transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        container.SetAsFirstSibling(); // 기존 HUD가 위에 그려지도록

        GameObject r = new GameObject("Ring", typeof(RectTransform));
        root = (RectTransform)r.transform;
        root.SetParent(container, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);

        track = NewImage("Track", root, trackColor);
        fill = NewImage("Fill", root, fallbackFillColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top; // 12시에서 시작
        fill.fillClockwise = true;
        fill.fillAmount = 0f;

        root.gameObject.SetActive(false);

        GameObject t = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        RectTransform tr = (RectTransform)t.transform;
        tr.SetParent(container, false);
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(400f, 40f);
        prompt = t.GetComponent<Text>();
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.fontSize = promptFontSize;
        prompt.fontStyle = FontStyle.Bold;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = Color.white;
        prompt.raycastTarget = false;
        Outline o = t.GetComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(2f, -2f);
        t.SetActive(false);
    }

    Image NewImage(string goName, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.GetComponent<Image>();
        img.sprite = ringSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void LateUpdate()
    {
        if (root == null) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null || player == null) return;

        UpdatePrompt();

        IChargePad active = ActivePad();
        if (active == null)
        {
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }

        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
        fill.fillAmount = active.Progress;

        // 어느 장판을 쓰는 중인지 색으로 알 수 있게, 그 장판 색을 그대로 입는다
        Color c = active.BarColor;
        c.a = fillAlpha;
        fill.color = c;

        Place();
    }

    // 거리 안에 든 것 하나의 안내 문구를 링 바로 위에 띄운다
    void UpdatePrompt()
    {
        string text = null;
        for (int i = 0; i < pads.Count && text == null; i++) text = pads[i].Prompt;

        bool show = !string.IsNullOrEmpty(text);
        if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
        if (!show) return;

        prompt.text = text;
        Vector2 local;
        if (HeadLocal(out local)) ((RectTransform)prompt.transform).anchoredPosition = local + new Vector2(0f, size * 0.5f + 24f);
    }

    // 진행 중인 장판 하나. 두 장판이 겹칠 일이 없으므로 가장 많이 찬 것을 고른다
    IChargePad ActivePad()
    {
        IChargePad best = null;
        float bestP = 0.0001f;
        for (int i = 0; i < pads.Count; i++)
        {
            float p = pads[i].Progress;
            if (p > bestP) { bestP = p; best = pads[i]; }
        }
        return best;
    }

    // 플레이어 머리 위 월드 좌표를 캔버스 좌표로 옮긴다
    void Place()
    {
        Vector2 local;
        if (HeadLocal(out local)) root.anchoredPosition = local;
    }

    bool HeadLocal(out Vector2 local)
    {
        local = Vector2.zero;
        Vector3 screen = viewCamera.WorldToScreenPoint(player.position + Vector3.up * headHeight);
        if (screen.z <= 0f) return false; // 카메라 뒤면 그리지 않는다
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, null, out local);
    }

    // 가운데가 뚫린 링. 스프라이트 에셋을 두지 않으려고 런타임에 그린다
    static Sprite BuildRingSprite(float innerRatio)
    {
        const int S = 128;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[S * S];
        float c = S * 0.5f;
        float outer = c - 1f;
        float inner = outer * Mathf.Clamp01(innerRatio);

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 바깥·안쪽 경계를 1px씩 부드럽게
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
    }
}
