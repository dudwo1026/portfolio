using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public Health playerHealth;
    public GameObject panel;
    // 구획 안에서 죽었을 때만 보인다. 마지막 구획 클리어 시점(없으면 처음)으로 되돌린다
    public GameObject retryZoneButton;

    bool shown;

    void Update()
    {
        if (shown || playerHealth == null) return;
        if (playerHealth.CurrentHealth <= 0f) Show();
    }

    void Show()
    {
        shown = true;
        if (retryZoneButton != null) retryZoneButton.SetActive(CombatZone.Current != null);
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnRestart()
    {
        Time.timeScale = 1f;
        ZoneSave.Clear(); // 처음부터
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnRetryZone()
    {
        Time.timeScale = 1f;
        ZoneSave.Retry();
    }

    public void OnQuit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
