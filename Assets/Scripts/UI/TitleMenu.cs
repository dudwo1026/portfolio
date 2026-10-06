using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 화면 메뉴. 지금은 새 게임과 종료만 동작한다.
public class TitleMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    public void NewGame()
    {
        Time.timeScale = 1f; // 이전 판이 멈춘 상태로 끝났을 수 있다
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 빌드가 아니라 플레이를 멈춘다
#else
        Application.Quit();
#endif
    }
}
