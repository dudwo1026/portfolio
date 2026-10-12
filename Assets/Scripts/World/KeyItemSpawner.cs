using System.Collections.Generic;
using UnityEngine;

// 포탈 열쇠. 처치 게이지 20에서 1개, 이후 25마다 1개씩 (20, 45, ... 170 → 7개) 등장한다.
// 등장 위치는 spawnPoints 중 아직 안 쓴 곳에서 무작위. 다 모으면 포탈(requireKeys)이 열린다
public class KeyItemSpawner : MonoBehaviour
{
    public static KeyItemSpawner Instance;

    public Transform[] spawnPoints;   // 맵에 배치한 큐브들. 시작할 때 숨겼다가 차례가 오면 열쇠로 켠다
    public int keysRequired = 7;
    public float firstKeyAt = 20f;
    public float gaugeStep = 25f;
    public Color keyColor = new Color(1f, 0.8f, 0.1f, 1f);
    public float spinSpeed = 90f;
    public UnityEngine.UI.Text countLabel; // 우상단 게이지 아래 "열쇠 n/7"
    public HudMessage announce; // 화면 중상단 "n번째 열쇠가 생성되었습니다"
    // 포탈 옆 열쇠 표시등. 왼쪽부터 순서대로 넣는다 — 모은 개수만큼 앞에서부터 켜진다
    public Renderer[] slotLights;
    public Material slotOff;
    public Material slotOn;

    public int Collected { get; private set; }
    public bool HasAllKeys { get { return Collected >= keysRequired; } }

    readonly List<Transform> unused = new List<Transform>();
    int spawned;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = keyColor;
        foreach (Transform p in spawnPoints)
        {
            if (p == null) continue;
            Collider c = p.GetComponent<Collider>();
            if (c != null) c.isTrigger = true; // 몸으로 막지 않고, 닿으면 줍는다
            Renderer r = p.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            p.gameObject.AddComponent<KeyItem>();
            p.gameObject.SetActive(false);
            unused.Add(p);
        }
        RefreshLabel();
    }

    void Update()
    {
        float value = KillGauge.Instance != null ? KillGauge.Instance.Value : 0f;
        int due = value < firstKeyAt ? 0 : Mathf.Min(keysRequired, Mathf.FloorToInt((value - firstKeyAt) / gaugeStep) + 1);
        while (spawned < due && unused.Count > 0)
        {
            int i = Random.Range(0, unused.Count);
            unused[i].gameObject.SetActive(true);
            unused.RemoveAt(i);
            spawned++;
            // 한 번에 여러 개가 나오면 마지막 것만 보인다
            if (announce != null) announce.Show((spawned >= keysRequired ? "마지막" : spawned + "번째") + " 열쇠가 생성되었습니다");
        }
    }

    public void Collect(GameObject key)
    {
        key.SetActive(false);
        Collected++;
        RefreshLabel();
    }

    // 테스트용 (KillGauge의 ' 키): 남은 열쇠를 전부 치우고 다 모은 것으로 처리한다
    public void DebugCollectAll()
    {
        foreach (Transform p in spawnPoints) if (p != null) p.gameObject.SetActive(false);
        unused.Clear();
        spawned = keysRequired; // 이후 게이지가 올라도 더 나오지 않는다
        Collected = keysRequired;
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (countLabel != null) countLabel.text = "열쇠 " + Collected + "/" + keysRequired;
        if (slotLights == null) return;
        for (int i = 0; i < slotLights.Length; i++)
            if (slotLights[i] != null) slotLights[i].sharedMaterial = i < Collected ? slotOn : slotOff;
    }
}

public class KeyItem : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(0f, KeyItemSpawner.Instance.spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerCombat>() == null) return; // 플레이어만 줍는다
        KeyItemSpawner.Instance.Collect(gameObject);
    }
}
