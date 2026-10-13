using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 화면 메뉴. 지금은 새 게임과 종료만 동작한다.
// 새 게임 → 난이도 선택 → 무기 선택 → 게임 시작. 고른 값은 GameSettings에 담아 게임 씬으로 넘긴다
public class TitleMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public GameObject menu;                   // 오른쪽 메뉴 (선택 화면 중에는 숨긴다)
    public GameObject difficultySelectPanel;  // 비워 두면 난이도 선택 없이 하드
    public GameObject weaponSelectPanel;      // 비워 두면 예전처럼 바로 시작

    public void NewGame()
    {
        if (menu != null) menu.SetActive(false);
        if (difficultySelectPanel != null) { difficultySelectPanel.SetActive(true); return; }
        GameSettings.difficulty = Difficulty.Hard;
        OpenWeaponSelect();
    }

    public void ChooseNormal() { ChooseDifficulty(Difficulty.Normal); }
    public void ChooseHard() { ChooseDifficulty(Difficulty.Hard); }

    void ChooseDifficulty(Difficulty d)
    {
        GameSettings.difficulty = d;
        if (difficultySelectPanel != null) difficultySelectPanel.SetActive(false);
        OpenWeaponSelect();
    }

    void OpenWeaponSelect()
    {
        if (weaponSelectPanel == null) { StartGame(); return; }
        weaponSelectPanel.SetActive(true);
    }

    public void StartRifle() { StartWith(PrimaryWeapon.Rifle); }
    public void StartLaser() { StartWith(PrimaryWeapon.Laser); }
    public void StartShotgun() { StartWith(PrimaryWeapon.Shotgun); }

    void StartWith(PrimaryWeapon weapon)
    {
        GameSettings.primary = weapon;
        GameSettings.primaryChosen = true;
        StartGame();
    }

    public void StartGame()
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
