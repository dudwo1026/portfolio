using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 구획 자동 저장. 구획을 클리어하는 순간(열쇠를 다 주운 순간) 진행 상황과 플레이어 상태를 기억해 두고,
// 구획 안에서 죽었을 때 게임오버 화면의 "구획 재시도"로 씬을 다시 불러 그 시점으로 되돌린다.
// 플레이 세션 안에서만 유지된다 (게임을 끄면 사라진다)
public static class ZoneSave
{
    public static bool HasSave { get; private set; }
    public static bool Retrying { get; private set; } // 재시도로 씬을 다시 불러온 중 (가이드를 건너뛰는 등)

    public static readonly List<string> ClearedZones = new List<string>();
    public static readonly List<int> UsedElites = new List<int>(); // 미니보스로 나온 순서 = 웨이브에 섞이는 종류
    static int keys;
    static Vector3 playerPos;
    static float playerHealth;
    static int consumables;
    static int[] ammo;

    // 새 게임·처음부터 다시 시작할 때
    public static void Clear()
    {
        HasSave = false;
        Retrying = false;
        ClearedZones.Clear();
        UsedElites.Clear();
    }

    public static void Save(IEnumerable<string> clearedZones, IEnumerable<int> usedElites, int keyCount)
    {
        ClearedZones.Clear(); ClearedZones.AddRange(clearedZones);
        UsedElites.Clear(); UsedElites.AddRange(usedElites);
        keys = keyCount;
        PlayerCombat pc = Object.FindFirstObjectByType<PlayerCombat>();
        if (pc != null)
        {
            playerPos = pc.transform.position;
            Health h = pc.GetComponent<Health>();
            playerHealth = h != null ? h.CurrentHealth : 0f;
            PlayerConsumables c = pc.GetComponent<PlayerConsumables>();
            consumables = c != null ? c.count : 0;
            ammo = pc.GetAmmoState();
        }
        HasSave = true;
    }

    // 게임오버에서 "구획 재시도". 저장이 없으면(첫 구획) 처음 상태로 다시 시작한다
    public static void Retry()
    {
        Retrying = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += (s, m) => { if (Retrying) new GameObject("ZoneSaveRestore").AddComponent<Restorer>(); };
    }

    // 씬 오브젝트들의 Awake/Start(탄 채우기 등)가 끝난 다음 프레임에 덮어쓴다
    class Restorer : MonoBehaviour
    {
        int frames;
        void Update()
        {
            if (++frames < 2) return;
            Retrying = false;
            if (HasSave) Apply();
            Destroy(gameObject);
        }

        void Apply()
        {
            if (KeyItemSpawner.Instance != null) KeyItemSpawner.Instance.SetCollected(keys);
            PlayerCombat pc = FindFirstObjectByType<PlayerCombat>();
            if (pc == null) return;
            CharacterController cc = pc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pc.transform.position = playerPos;
            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = playerPos; rb.linearVelocity = Vector3.zero; }
            if (cc != null) cc.enabled = true;
            Health h = pc.GetComponent<Health>();
            if (h != null) h.SetCurrentHealth(playerHealth);
            PlayerConsumables c = pc.GetComponent<PlayerConsumables>();
            if (c != null) c.count = consumables;
            pc.SetAmmoState(ammo);
        }
    }
}
