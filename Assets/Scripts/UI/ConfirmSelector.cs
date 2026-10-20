using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 버튼 목록을 키보드로 고른다. 가로: ←/A, →/D / 세로: ↑/W, ↓/S. Enter = 누르기.
// 고른 버튼은 무기슬롯처럼 조금 커진다. 비활성 버튼은 건너뛴다. 마우스 클릭도 그대로 된다.
// Space는 대시 키라서 쓰지 않는다 (확인하는 프레임에 일시정지가 풀리면 대시가 같이 나갈 수 있다)
public class ConfirmSelector : MonoBehaviour
{
    public Button[] buttons;          // 왼쪽(위)부터
    public bool vertical = false;
    public int defaultIndex = 1;      // 창이 뜰 때 고른 상태로 둘 버튼
    // 켜면 처음엔 아무것도 고르지 않은 상태로 뜨고, 방향키를 처음 누를 때 defaultIndex가 골라진다
    public bool startUnselected = false;
    public float selectedScale = 1.15f;
    public bool selectedOutline = true;     // 고른 버튼에 흰 테두리
    public Color outlineColor = Color.white;
    public float outlineWidth = 3f;
    // 넣으면 Outline 대신 이 테두리 그림(9-slice)을 고른 버튼 위에 얹는다. 그림 카드는 Outline이 안 보인다
    public Sprite selectedFrame;
    public float framePadding = 8f;         // 버튼 바깥으로 튀어나오는 정도
    public float frameScale = 1f;           // 테두리 굵기 배율 (큰 카드는 2쯤)
    public bool pressPulse = false;         // 누르면 버튼 잔상이 퍼지며 사라진다 (UIPulse)
    // 버튼과 같은 순서. 고른 버튼 것만 켠다 (무기 설명처럼 고른 동안만 보일 것)
    public GameObject[] showWhenSelected;
    public bool pressFlash = false;         // 누르면 버튼이 한 번 하얗게 깜빡인다
    public float flashTime = 0.2f;

    int index; // -1 = 아직 아무것도 안 고름
    // 켜진 프레임에는 입력을 받지 않는다. 앞 화면에서 Enter로 이 창을 열었으면
    // 같은 프레임의 그 Enter가 여기서도 눌린 것으로 잡혀 첫 버튼이 바로 눌린다
    int enabledFrame = -1;

    void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true).rootCanvas;
        // 마우스 클릭도 Enter도 결국 onClick으로 오므로 여기 하나로 둘 다 잡힌다
        foreach (Button b in buttons)
        {
            Button button = b;
            if (pressPulse) button.onClick.AddListener(() => UIPulse.Play((RectTransform)button.transform, canvas));
            if (pressFlash) button.onClick.AddListener(() => StartCoroutine(Flash(button)));
        }
    }

    System.Collections.IEnumerator Flash(Button b)
    {
        GameObject go = new GameObject("Flash", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(b.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        for (float t = 0f; t < flashTime; t += Time.unscaledDeltaTime)
        {
            img.color = new Color(1f, 1f, 1f, 0.8f * (1f - t / flashTime));
            yield return null;
        }
        Destroy(go);
    }

    void OnEnable()
    {
        enabledFrame = Time.frameCount;
        if (startUnselected) index = -1;
        else SelectDefault();
        Apply();
    }

    void SelectDefault()
    {
        index = Mathf.Clamp(defaultIndex, 0, buttons.Length - 1);
        if (!buttons[index].interactable) Step(1);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || buttons.Length == 0) return;
        if (Time.frameCount == enabledFrame) return;

        bool prev = vertical ? (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                             : (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame);
        bool next = vertical ? (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                             : (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame);
        if (index < 0)
        {
            if (prev || next) { SelectDefault(); Apply(); } // 첫 입력은 고르기만 한다
            return;
        }
        if (prev) { Step(-1); Apply(); }
        if (next) { Step(1); Apply(); }
        if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && buttons[index].interactable)
            buttons[index].onClick.Invoke();
    }

    // 끝에서 멈춘다. 가는 길의 비활성 버튼은 건너뛰고, 그쪽에 고를 게 없으면 제자리
    void Step(int dir)
    {
        for (int i = index + dir; i >= 0 && i < buttons.Length; i += dir)
            if (buttons[i].interactable) { index = i; return; }
    }

    void Apply()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].transform.localScale = Vector3.one * (i == index ? selectedScale : 1f);
            if (showWhenSelected != null && i < showWhenSelected.Length && showWhenSelected[i] != null) showWhenSelected[i].SetActive(i == index);
            if (!selectedOutline) continue;
            if (selectedFrame != null) { Frame(buttons[i]).SetActive(i == index); continue; }
            if (buttons[i].targetGraphic == null) continue;
            // 버튼 바탕 그림에만 단다 (글자에 달면 글자가 두꺼워진다)
            Outline o = buttons[i].targetGraphic.GetComponent<Outline>();
            if (o == null)
            {
                if (i != index) continue;
                o = buttons[i].targetGraphic.gameObject.AddComponent<Outline>();
            }
            o.effectColor = outlineColor;
            o.effectDistance = new Vector2(outlineWidth, -outlineWidth);
            o.enabled = i == index;
        }
    }

    GameObject Frame(Button b)
    {
        Transform t = b.transform.Find("SelectFrame");
        if (t != null) return t.gameObject;
        GameObject go = new GameObject("SelectFrame", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(b.transform, false);
        rt.SetAsLastSibling(); // 글자보다 위
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-framePadding, -framePadding); rt.offsetMax = new Vector2(framePadding, framePadding);
        Image img = go.AddComponent<Image>();
        img.sprite = selectedFrame; img.type = Image.Type.Sliced; img.color = outlineColor; img.raycastTarget = false;
        img.pixelsPerUnitMultiplier = 1f / frameScale;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        return go;
    }
}
