using UnityEngine;
using UnityEngine.UI;

// 화면에 잠깐 떴다가 서서히 사라지는 안내 문구. Text 오브젝트에 붙여 두고 Show로 띄운다
[RequireComponent(typeof(Text))]
public class HudMessage : MonoBehaviour
{
    public float showTime = 2.5f;  // 떠 있는 전체 시간
    public float fadeTime = 0.5f;  // 그중 끝에서 서서히 사라지는 시간

    Text label;
    float timer;

    void Awake()
    {
        label = GetComponent<Text>();
        if (timer <= 0f) label.enabled = false;
    }

    // 떠 있는 중에 다시 부르면 새 문구로 바뀌고 시간도 처음부터
    public void Show(string message)
    {
        if (label == null) label = GetComponent<Text>();
        label.text = message;
        label.enabled = true;
        timer = showTime;
        SetAlpha(1f);
    }

    void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        SetAlpha(fadeTime > 0f ? Mathf.Clamp01(timer / fadeTime) : 1f); // 외곽선도 글자 알파를 따른다
        if (timer <= 0f) label.enabled = false;
    }

    void SetAlpha(float a)
    {
        Color c = label.color;
        c.a = a;
        label.color = c;
    }
}
