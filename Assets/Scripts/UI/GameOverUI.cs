using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public Health playerHealth;
    public GameObject panel;

    bool shown;

    void Update()
    {
        if (shown || playerHealth == null) return;
        if (playerHealth.CurrentHealth <= 0f) Show();
    }

    void Show()
    {
        shown = true;
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
