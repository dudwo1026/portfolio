using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 타이틀 화면 메뉴. 지금은 새 게임과 종료만 동작한다.
// 새 게임 → 난이도 선택 → 무기 선택 → 게임 시작. 고른 값은 GameSettings에 담아 게임 씬으로 넘긴다
public class TitleMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public GameObject menu;                   // 메뉴 (선택 화면 중에는 숨긴다)
    public GameObject difficultySelectPanel;  // 비워 두면 난이도 선택 없이 하드
    public GameObject weaponSelectPanel;      // 비워 두면 예전처럼 바로 시작
    public Graphic background;                // 새 게임을 누르면 이 배경을 검게 바꾼 뒤 난이도 선택을 띄운다
    public float fadeToBlackTime = 2f;
    [UnityEngine.Serialization.FormerlySerializedAs("weaponStartDelay")]
    public float flashDelay = 0.25f;          // 난이도/무기 카드가 한 번 깜빡이고 넘어갈 시간

    bool starting, choosing, launching;

    public void NewGame()
    {
        // 메뉴는 배경과 같이 어두워지다가, 난이도 선택이 뜰 때 숨긴다
        if (starting) return;
        starting = true;
        // 이 스크립트는 곧 숨길 메뉴에 붙어 있어 코루틴을 못 돌린다. 켜져 있는 배경에서 돌린다
        if (background != null) background.StartCoroutine(FadeThenChoose());
        else ShowDifficulty();
    }

    IEnumerator FadeThenChoose()
    {
        Color from = background.color;
        CanvasGroup menuGroup = null;
        if (menu != null)
        {
            menuGroup = menu.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = menu.AddComponent<CanvasGroup>();
        }
        for (float t = 0f; t < fadeToBlackTime; t += Time.unscaledDeltaTime)
        {
            background.color = Color.Lerp(from, Color.black, t / fadeToBlackTime);
            if (menuGroup != null) menuGroup.alpha = 1f - t / fadeToBlackTime;
            yield return null;
        }
        background.color = Color.black;
        ShowDifficulty();
    }

    void ShowDifficulty()
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
        if (choosing) return; // 깜빡이는 동안·확인창이 떠 있는 동안 또 눌러도 무시
        choosing = true;
        string name = d == Difficulty.Normal ? "노말" : "하드";
        AfterFlash(() => ConfirmPopup.Show(name + " 난이도로 진행할까요?", () =>
        {
            GameSettings.difficulty = d;
            if (difficultySelectPanel != null) difficultySelectPanel.SetActive(false);
            OpenWeaponSelect();
        }, Selector(difficultySelectPanel), () => choosing = false));
    }

    static ConfirmSelector Selector(GameObject panel)
    {
        return panel != null ? panel.GetComponent<ConfirmSelector>() : null;
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
        if (launching) return; // 깜빡이는 동안·확인창이 떠 있는 동안 또 눌러도 무시
        launching = true;
        string name = weapon == PrimaryWeapon.Laser ? "레이저로" : weapon == PrimaryWeapon.Shotgun ? "산탄총으로" : "돌격소총으로";
        AfterFlash(() => ConfirmPopup.Show(name + " 시작할까요?", () =>
        {
            GameSettings.primary = weapon;
            GameSettings.primaryChosen = true;
            StartGame();
        }, Selector(weaponSelectPanel), () => launching = false));
    }

    // 카드 깜빡임(ConfirmSelector.pressFlash)이 보이게 잠깐 기다렸다 넘어간다
    void AfterFlash(System.Action next)
    {
        if (background != null) background.StartCoroutine(After(flashDelay, next));
        else next();
    }

    IEnumerator After(float delay, System.Action next)
    {
        yield return new WaitForSecondsRealtime(delay);
        next();
    }

    public void StartGame()
    {
        ZoneSave.Clear(); // 새 게임은 구획 세이브 없이
        Time.timeScale = 1f; // 이전 판이 멈춘 상태로 끝났을 수 있다
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenOptions()
    {
        OptionsPanel.Open(menu != null ? menu.GetComponent<ConfirmSelector>() : null);
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
