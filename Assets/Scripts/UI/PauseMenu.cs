using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ESC: 일시정지 + 화면을 어둡게 하고 메뉴(타이틀로 / 옵션 / 게임 종료)를 띄운다. 다시 ESC면 이어서 한다.
// 이 컴포넌트는 꺼지지 않는 오브젝트에 두고, panel만 켰다 껐다 한다
public class PauseMenu : MonoBehaviour
{
    public GameObject panel;
    public string titleSceneName = "TitleScene";

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        if (OptionsPanel.IsOpen || OptionsPanel.ClosedFrame == Time.frameCount) return; // ESC는 옵션 창이 먼저 쓴다

        if (panel.activeSelf) Resume();
        // 게임오버·포탈 확인창·열쇠 연출·결과창으로 이미 멈춰 있거나, 시간을 직접 다루는 연출(워프·보스 등장·처치 슬로) 중엔 열지 않는다
        else if (Time.timeScale > 0f && !Portal.Warping && !BossIntro.Playing && !HitStop.Active) Open();
    }

    void Open()
    {
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OpenOptions()
    {
        OptionsPanel.Open(panel.GetComponentInChildren<ConfirmSelector>());
    }

    public void ToTitle()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 빌드가 아니라 플레이를 멈춘다
#else
        Application.Quit();
#endif
    }
}
