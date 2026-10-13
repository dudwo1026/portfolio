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

    int index; // -1 = 아직 아무것도 안 고름
    // 켜진 프레임에는 입력을 받지 않는다. 앞 화면에서 Enter로 이 창을 열었으면
    // 같은 프레임의 그 Enter가 여기서도 눌린 것으로 잡혀 첫 버튼이 바로 눌린다
    int enabledFrame = -1;

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
            buttons[i].transform.localScale = Vector3.one * (i == index ? selectedScale : 1f);
    }
}
