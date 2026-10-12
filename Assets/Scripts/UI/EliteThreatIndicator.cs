using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 화면 밖에 있는 정예몹의 방향을 화면 가장자리 화살표로 알려준다.
//
// 왜 필요한가: 이 게임의 쿼터뷰 카메라(yaw -135 / pitch 40 / distance 13)는 앞(12시)으로
// 43유닛을 보여주지만 뒤(6시)로는 6.5유닛밖에 못 본다. 앞뒤 시야가 6.6:1이다.
// 정예 인지범위는 15라서, 6시에서 오는 정예는 8유닛 넘게 보이지 않는 채로 달려온다.
// 카메라 각도로는 해결이 안 된다 - pitch를 40에서 70까지 눕혀도 뒤쪽 시야는 6.55에서 6.78로
// 거의 그대로고 앞쪽만 43에서 11로 줄어든다. 그래서 정보로 메운다.
//
// 타입은 일부러 구분하지 않는다. 색으로 어떤 정예인지까지 알려주면 대처법이 미리 정해져서
// 체감 난이도가 급락한다. 정예가 저쪽에 있다는 것까지만 알려주는 게 이 표시기의 역할이다.
public class EliteThreatIndicator : MonoBehaviour
{
    public Camera viewCamera;
    public Transform player;

    [Header("표시 조건")]
    // 켜면 정예 대신 맵에 나와 있는 열쇠(KeyItemSpawner)를 가리킨다. 같은 컴포넌트를 하나 더 붙여 파란색으로 쓴다
    public bool trackKeys = false;
    // 넣어 두면 정예·열쇠 대신 이 포탈들 중 열린 것을 가리킨다
    public Portal[] portals;
    bool TrackPortals { get { return portals != null && portals.Length > 0; } }
    public float showRadius = 22f;       // 이 안의 정예만 표시. 인지범위(15)보다 넉넉히 잡아 미리 경고한다
    public float refreshInterval = 0.25f; // 정예 목록 재탐색 주기 (위치는 매 프레임 갱신된다)
    public float aimHeight = 1f;          // 몹의 발밑이 아니라 몸통을 기준으로 방향을 잡는다

    [Header("모양")]
    // 화면 가장자리에서 이만큼 안쪽에 찍는다 (캔버스 단위). 화살표(가장 클 때 약 80)가 HUD에 겹치지 않게 변마다 따로 둔다
    public float leftMargin = 265f;   // 왼쪽 무기슬롯(왼끝에서 215)
    public float rightMargin = 120f;
    public float topMargin = 190f;    // 좌상단 초상화·회복약(위끝에서 ~140), 우상단 게이지·열쇠 개수(~110)
    public float bottomMargin = 90f;  // 아래쪽 HUD는 없다 (초상화는 좌상단으로, 미니맵은 제거)
    public float arrowSize = 46f;
    public Color arrowColor = new Color(1f, 0.24f, 0.18f, 0.92f);
    public Color outlineColor = new Color(0f, 0f, 0f, 0.7f);
    public float outlineScale = 1.32f;

    [Header("거리 표현")]
    // 거리는 알려주되 정체는 알려주지 않는다. 가까울수록 크게 그린다
    public bool scaleByDistance = true;
    public float nearDistance = 8f;
    public float nearScale = 1.3f;
    public float farScale = 0.78f;

    class Arrow
    {
        public RectTransform root;
        public Image outline;
        public Image fill;
    }

    RectTransform container;
    Sprite arrowSprite;
    readonly List<Arrow> pool = new List<Arrow>();
    readonly List<Transform> targets = new List<Transform>();
    float refreshTimer;

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (player == null)
        {
            TopDownMove mover = FindFirstObjectByType<TopDownMove>();
            if (mover != null) player = mover.transform;
        }

        arrowSprite = BuildArrowSprite();
        BuildContainer();
        RefreshEliteList();
    }

    void OnDestroy()
    {
        // 런타임에 만든 텍스처는 스스로 치운다
        if (arrowSprite != null)
        {
            if (arrowSprite.texture != null) Destroy(arrowSprite.texture);
            Destroy(arrowSprite);
        }
    }

    void BuildContainer()
    {
        GameObject go = new GameObject(TrackPortals ? "PortalArrows" : trackKeys ? "KeyArrows" : "EliteThreatArrows", typeof(RectTransform));
        container = (RectTransform)go.transform;
        container.SetParent(transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        container.SetAsFirstSibling(); // 기존 HUD(체력·슬롯·미니맵)가 위에 그려지도록
    }

    void LateUpdate()
    {
        if (container == null) return;

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null || player == null) { HideFrom(0); return; }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshEliteList();
        }

        Vector2 size = container.rect.size;
        Vector2 half = size * 0.5f;
        if (half.x - Mathf.Max(leftMargin, rightMargin) <= 1f || half.y - Mathf.Max(topMargin, bottomMargin) <= 1f) { HideFrom(0); return; }

        Vector3 playerFlat = player.position;
        playerFlat.y = 0f;

        int used = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            Transform t = targets[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue; // 주운 열쇠는 꺼진다
            if (!trackKeys && !TrackPortals)
            {
                EnemyAI e = t.GetComponent<EnemyAI>();
                if (e == null || !e.isActiveAndEnabled) continue;
                if (e.State != EnemyState.Attack) continue; // 플레이어를 쫓기 시작한 정예만 알린다
            }

            Vector3 world = t.position + Vector3.up * aimHeight;

            Vector3 flat = t.position;
            flat.y = 0f;
            float dist = Vector3.Distance(flat, playerFlat);
            if (dist > showRadius) continue;

            Vector2 edge;
            if (!OffScreenEdgePoint(world, size, out edge)) continue; // 화면에 보이면 화살표 없음

            Arrow a = GetArrow(used++);
            a.root.anchoredPosition = edge;
            a.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg - 90f);

            float s = scaleByDistance
                ? Mathf.Lerp(nearScale, farScale, Mathf.InverseLerp(nearDistance, showRadius, dist))
                : 1f;
            a.root.localScale = new Vector3(s, s, 1f);
        }

        HideFrom(used);
    }

    // 화면 밖이면 가장자리 위의 찍을 지점을 돌려준다. 화면 안이면 false
    bool OffScreenEdgePoint(Vector3 world, Vector2 size, out Vector2 point)
    {
        point = Vector2.zero;

        Vector3 vp = viewCamera.WorldToViewportPoint(world);
        bool behind = vp.z <= 0f;

        // 카메라 뒤쪽은 뷰포트 좌표가 뒤집혀 나온다. 중심 대칭으로 되돌려 방향만 살린다
        if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
        else if (vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) return false; // 화면 안

        Vector2 p = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);

        // 정확히 카메라 뒤(정중앙으로 접힌 경우)는 방향이 없다. 이 시점에서는 화면 아래가 맞다
        if (p.sqrMagnitude < 0.0001f) p = new Vector2(0f, -1f);

        // 방향을 유지한 채 가장자리 사각형에 정확히 얹는다 (밖이면 줄이고, 안이면 늘린다)
        float limitX = size.x * 0.5f - (p.x < 0f ? leftMargin : rightMargin);
        float limitY = size.y * 0.5f - (p.y < 0f ? bottomMargin : topMargin);
        if (limitX < 1f) limitX = 1f;
        if (limitY < 1f) limitY = 1f;
        float kx = Mathf.Abs(p.x) > 0.0001f ? limitX / Mathf.Abs(p.x) : float.PositiveInfinity;
        float ky = Mathf.Abs(p.y) > 0.0001f ? limitY / Mathf.Abs(p.y) : float.PositiveInfinity;
        point = p * Mathf.Min(kx, ky);
        return true;
    }

    void RefreshEliteList()
    {
        targets.Clear();
        if (TrackPortals)
        {
            foreach (Portal p in portals) if (p != null && p.IsOpen) targets.Add(p.transform);
            return;
        }
        if (trackKeys)
        {
            // 아직 안 나온 열쇠도 넣어 둔다. 꺼져 있는 동안은 LateUpdate에서 걸러진다
            if (KeyItemSpawner.Instance != null)
                foreach (Transform k in KeyItemSpawner.Instance.spawnPoints) targets.Add(k);
            return;
        }
        EnemyAI[] all = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].isElite) targets.Add(all[i].transform);
    }

    Arrow GetArrow(int index)
    {
        while (pool.Count <= index) pool.Add(CreateArrow());
        Arrow a = pool[index];
        if (!a.root.gameObject.activeSelf) a.root.gameObject.SetActive(true);
        a.fill.color = arrowColor;
        a.outline.color = outlineColor;
        return a;
    }

    void HideFrom(int index)
    {
        for (int i = index; i < pool.Count; i++)
            if (pool[i].root.gameObject.activeSelf) pool[i].root.gameObject.SetActive(false);
    }

    Arrow CreateArrow()
    {
        GameObject go = new GameObject("EliteArrow", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(container, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        Arrow a = new Arrow();
        a.root = rt;
        a.outline = NewImage("Outline", rt, outlineColor, arrowSize * outlineScale);
        a.fill = NewImage("Fill", rt, arrowColor, arrowSize);
        return a;
    }

    Image NewImage(string goName, RectTransform parent, Color color, float size)
    {
        GameObject go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.GetComponent<Image>();
        img.sprite = arrowSprite;
        img.color = color;
        img.raycastTarget = false; // 버튼 클릭을 가로채지 않게
        return img;
    }

    // 위를 향한 화살촉. 스프라이트 에셋을 따로 두지 않으려고 런타임에 그린다
    static Sprite BuildArrowSprite()
    {
        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[S * S];
        float notch = S * 0.3f;
        float cx = S * 0.5f;

        for (int y = 0; y < S; y++)
        {
            float t = y / (float)(S - 1);
            float halfWidth = (1f - t) * 0.46f * S;                          // 위로 갈수록 좁아진다
            float notchHalf = y < notch ? (1f - y / notch) * 0.26f * S : 0f;  // 밑동을 파 화살촉으로

            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Abs(x + 0.5f - cx);
                float a = Mathf.Clamp01(halfWidth - d);               // 바깥 경계 1px 부드럽게
                if (notchHalf > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - notchHalf));
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
    }
}
