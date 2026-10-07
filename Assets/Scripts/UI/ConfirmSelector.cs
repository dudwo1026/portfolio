using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 예/아니오 창을 키보드로 고른다: ←/A = 왼쪽, →/D = 오른쪽, Enter = 누르기.
// 고른 버튼은 무기슬롯처럼 조금 커진다. 마우스 클릭도 그대로 된다.
// Space는 대시 키라서 쓰지 않는다 (확인하는 프레임에 일시정지가 풀리면 대시가 같이 나갈 수 있다)
public class ConfirmSelector : MonoBehaviour
{
    public Button[] buttons;          // 왼쪽부터 (YES, NO)
    public int defaultIndex = 1;      // 창이 뜰 때 고른 상태로 둘 버튼. 실수로 들어가지 않게 NO
    public float selectedScale = 1.15f;

    int index;

    void OnEnable()
    {
        index = defaultIndex;
        Apply();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || buttons.Length == 0) return;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { index = Mathf.Max(0, index - 1); Apply(); }
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { index = Mathf.Min(buttons.Length - 1, index + 1); Apply(); }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) buttons[index].onClick.Invoke();
    }

    void Apply()
    {
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].transform.localScale = Vector3.one * (i == index ? selectedScale : 1f);
    }
}
