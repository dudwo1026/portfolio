using UnityEngine;

// 포탈 열쇠. 구획(CombatZone)을 클리어하면 SpawnKeyAt으로 떨어진다. 다 모으면 옥상 포탈(requireKeys)이 열린다
public class KeyItemSpawner : MonoBehaviour
{
    public static KeyItemSpawner Instance;

    public int keysRequired = 7;
    public GameObject keyPrefab; // 열쇠 모델 (루트에 트리거 콜라이더). 비워 두면 노란 큐브
    public Color keyColor = new Color(1f, 0.8f, 0.1f, 1f);
    public float spinSpeed = 90f;
    public UnityEngine.UI.Text countLabel; // 우상단 게이지 아래 "열쇠 n/7"
    // 포탈 옆 열쇠 표시등. 왼쪽부터 순서대로 넣는다 — 모은 개수만큼 앞에서부터 켜진다
    public Renderer[] slotLights;
    public Material slotOff;
    public Material slotOn;

    public int Collected { get; private set; }
    public bool HasAllKeys { get { return Collected >= keysRequired; } }
    public readonly System.Collections.Generic.List<Transform> Keys = new System.Collections.Generic.List<Transform>(); // 떨어뜨린 열쇠 (주운 것은 꺼진 채 남는다)

    Material keyMat;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        keyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        keyMat.color = keyColor;
        RefreshLabel();
    }

    // 원하는 자리에 열쇠 하나를 세운다. 닿으면 줍는다
    public GameObject SpawnKeyAt(Vector3 pos)
    {
        GameObject go;
        if (keyPrefab != null) go = Instantiate(keyPrefab, pos, Quaternion.identity);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos;
            go.GetComponent<Collider>().isTrigger = true;
            go.GetComponent<Renderer>().sharedMaterial = keyMat;
        }
        go.name = "ZoneKey";
        go.AddComponent<KeyItem>();
        Keys.Add(go.transform);
        return go;
    }

    public void Collect(GameObject key)
    {
        key.SetActive(false);
        Collected++;
        RefreshLabel();
    }

    // 세이브 복구
    public void SetCollected(int n)
    {
        Collected = n;
        RefreshLabel();
    }

    // 테스트용 (KillGauge의 ' 키): 열쇠를 다 모은 것으로 처리한다
    public void DebugCollectAll()
    {
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
