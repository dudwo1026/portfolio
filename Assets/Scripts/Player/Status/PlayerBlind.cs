using UnityEngine;
using UnityEngine.UI;

// 암흑: everything except the player's immediate surroundings goes dark.
// A radial-gradient overlay (clear in the middle, opaque outside) is parked on the player's
// screen position - far larger than the screen, so the dark part always covers the rest of it.
public class PlayerBlind : MonoBehaviour
{
    public float duration = 10f;
    public float overlaySize = 4000f;     // canvas units; must exceed the screen so the dark part covers it
    public float holeInnerRatio = 0.0333f; // fully clear out to this fraction of the overlay radius
    public float holeOuterRatio = 0.12f;   // fully dark from here out
    public float maxDarkness = 1f;         // pitch black outside the player's bubble
    public float fadeTime = 0.3f;

    public bool IsActive => timer > 0f;

    float timer;
    Image overlay;
    RectTransform overlayRt;
    RectTransform canvasRt;
    Camera cam;

    public void Apply(float seconds)
    {
        timer = Mathf.Max(timer, seconds > 0f ? seconds : duration);
        if (overlay == null) Build();
        if (overlay != null) overlay.gameObject.SetActive(true);
    }

    public void Clear()
    {
        timer = 0f;
        if (overlay != null) overlay.gameObject.SetActive(false);
    }

    void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) return;
        canvasRt = canvasGO.GetComponent<RectTransform>();

        GameObject go = new GameObject("BlindOverlay", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);
        overlayRt = go.GetComponent<RectTransform>();
        overlayRt.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRt.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRt.pivot = new Vector2(0.5f, 0.5f);
        overlayRt.sizeDelta = new Vector2(overlaySize, overlaySize);

        overlay = go.AddComponent<Image>();
        overlay.sprite = BuildRadialSprite();
        overlay.raycastTarget = false; // must not swallow clicks on the game-over buttons
        overlay.color = new Color(0f, 0f, 0f, 0f);

        // keep the game-over panel above the darkness
        Transform gameOver = canvasGO.transform.Find("GameOverPanel");
        if (gameOver != null) gameOver.SetAsLastSibling();
    }

    Sprite BuildRadialSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(holeInnerRatio, holeOuterRatio, r));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    void LateUpdate()
    {
        if (overlay == null) return;

        if (timer > 0f) timer -= Time.deltaTime;

        float target = timer > 0f ? maxDarkness : 0f;
        Color c = overlay.color;
        c.a = Mathf.MoveTowards(c.a, target, (fadeTime > 0f ? maxDarkness / fadeTime : 1f) * Time.deltaTime);
        overlay.color = c;

        if (c.a <= 0.001f && timer <= 0f)
        {
            overlay.gameObject.SetActive(false);
            return;
        }

        FollowPlayer();
    }

    void FollowPlayer()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || canvasRt == null) return;

        Vector3 screenPoint = cam.WorldToScreenPoint(transform.position);
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, null, out local))
            overlayRt.anchoredPosition = local;
    }
}
