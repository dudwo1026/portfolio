using UnityEngine;

// 포탈 열쇠. 구획(CombatZone)을 클리어하면 SpawnKeyAt으로 떨어진다. 다 모으면 옥상 포탈(requireKeys)이 열린다
public class KeyItemSpawner : MonoBehaviour
{
    public static KeyItemSpawner Instance;

    public int keysRequired = 6;
    public GameObject keyPrefab; // 열쇠 모델 (루트에 트리거 콜라이더). 비워 두면 노란 큐브
    public Color keyColor = new Color(1f, 0.8f, 0.1f, 1f);
    public float spinSpeed = 90f;
    public UnityEngine.UI.Text countLabel; // 우상단 게이지 아래 "열쇠 n/6"
    // 포탈 옆 열쇠 표시등. 왼쪽부터 순서대로 넣는다 — 모은 개수만큼 앞에서부터 켜진다
    public Renderer[] slotLights;
    public Material slotOff;
    public Material slotOn;
    // 켜 두면 표시등 큐브 대신 열쇠 모델을 세운다: 못 모은 칸은 slotOff 한 가지 색, 모은 칸은 원래 열쇠 색
    public bool slotKeyModels = true;
    public float slotKeyHeight = 1.7f; // 표시등 열쇠 높이(월드)
    public float slotKeySpinSpeed = 30f; // 모은 칸 열쇠가 천천히 돈다 (도/초)
    Transform[] slotKeys;
    GameObject[] slotGlows; // 모은 칸만 빛 번짐을 켠다
    Renderer[][] slotParts;
    Material[][] slotPartMats;

    public int Collected { get; private set; }
    int lit; // 포탈 옆 표시등·개수 글자에 보이는 수. 획득 연출에서 구체가 떨어진 뒤에야 따라 오른다

    [Header("획득 연출")]
    public bool pickupCutscene = true;
    public float riseTime = 0.9f;      // 구체가 하늘로 솟는 시간
    public float riseHeight = 22f;
    public float fadeTime = 0.4f;
    public float fallTime = 0.55f;     // 포탈 앞에서 하늘의 구체가 열쇠 자리로 떨어지는 시간
    public float fallHeight = 16f;
    public float revealHold = 1.1f;    // 열쇠가 밝혀진 뒤 보여 주는 시간
    public float portalZoom = 0.8f;    // 포탈 앞을 비출 때 카메라 거리 배율
    public float orbSize = 2.2f;
    public float burstSize = 14f;      // 착지 순간 번지는 빛의 최대 지름
    bool cutsceneRunning;
    // 포탈은 마지막 열쇠가 연출로 밝혀지는 순간 열린다 (그래서 lit 기준)
    public bool HasAllKeys { get { return lit >= keysRequired; } }
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
        if (slotKeyModels && keyPrefab != null) BuildSlotKeys();
        RefreshLabel();
    }

    // 표시등 큐브 자리마다 열쇠를 세우고 큐브는 숨긴다. 큐브가 보던 방향(카메라 쪽)을 그대로 본다
    void BuildSlotKeys()
    {
        if (slotLights == null) return;
        slotKeys = new Transform[slotLights.Length];
        slotGlows = new GameObject[slotLights.Length];
        slotParts = new Renderer[slotLights.Length][];
        slotPartMats = new Material[slotLights.Length][];
        for (int i = 0; i < slotLights.Length; i++)
        {
            Renderer cube = slotLights[i];
            if (cube == null) continue;
            Bounds cb = cube.bounds;
            GameObject k = Instantiate(keyPrefab, cb.center, Quaternion.Euler(0f, cube.transform.eulerAngles.y, 0f));
            k.name = "SlotKey" + (i + 1);
            foreach (Collider c in k.GetComponentsInChildren<Collider>()) Destroy(c);
            Bounds kb = new Bounds(k.transform.position, Vector3.zero);
            Renderer[] parts = k.GetComponentsInChildren<MeshRenderer>(); // 빛 번짐(파티클)은 크기·색 바꾸기에서 뺀다
            Transform glow = k.transform.Find("Glow");
            if (glow != null) slotGlows[i] = glow.gameObject;
            foreach (Renderer r in parts) kb.Encapsulate(r.bounds);
            float s = slotKeyHeight / Mathf.Max(0.01f, kb.size.y);
            k.transform.localScale *= s;
            k.transform.position += Vector3.up * (cb.min.y - (k.transform.position.y + (kb.min.y - k.transform.position.y) * s)); // 바닥을 큐브 바닥에
            k.transform.SetParent(cube.transform.parent, true);
            cube.enabled = false;
            slotKeys[i] = k.transform;
            slotParts[i] = parts;
            slotPartMats[i] = System.Array.ConvertAll(parts, r => r.sharedMaterial);
        }
    }

    void Update()
    {
        if (slotKeys == null) return;
        for (int i = 0; i < slotKeys.Length && i < lit; i++)
            if (slotKeys[i] != null) slotKeys[i].Rotate(0f, slotKeySpinSpeed * Time.deltaTime, 0f, Space.World);
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
        Collected++; // 세이브·포탈 개방은 바로 센다. 보이는 쪽(lit)만 연출 끝에 맞춘다
        if (pickupCutscene && !cutsceneRunning) StartCoroutine(PickupCutscene(key.transform.position));
        else if (!cutsceneRunning) { lit = Collected; RefreshLabel(); }
    }

    // 열쇠 획득 연출. 세상을 멈춘 채(timeScale 0, 전부 unscaled 시간):
    // 열쇠가 빛의 구체로 바뀌어 하늘로 솟음 → 페이드아웃 → 포탈 앞 그 칸을 비추며 페이드인 →
    // 하늘에서 구체가 칸으로 떨어짐 → 번쩍하며 칸의 열쇠가 밝혀짐 → 잠깐 보여 주고 다시 플레이어로
    System.Collections.IEnumerator PickupCutscene(Vector3 from)
    {
        cutsceneRunning = true;
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;
        GameObject p = GameObject.Find("Player");
        Behaviour[] controls = p != null
            ? new Behaviour[] { p.GetComponent<PlayerCombat>(), p.GetComponent<PlayerDash>(), p.GetComponent<PlayerConsumables>() }
            : new Behaviour[0];
        bool[] wasOn = new bool[controls.Length];
        for (int i = 0; i < controls.Length; i++) if (controls[i] != null) { wasOn[i] = controls[i].enabled; controls[i].enabled = false; }

        // 1) 솟는다: 처음엔 천천히, 점점 빨라지며 화면 위로 빠져나간다
        LightOrb orb = LightOrb.Spawn(from, orbSize);
        for (float t = 0f; t < riseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / riseTime;
            orb.transform.position = from + Vector3.up * riseHeight * k * k;
            yield return null;
        }
        Destroy(orb.gameObject);

        // 2) 페이드아웃 → 카메라를 포탈 옆 그 칸으로
        UnityEngine.UI.Image fade = WarpBeam.ScreenOverlay(Color.black);
        yield return Fade(fade, 0f, 1f);
        int slot = Mathf.Clamp(Collected - 1, 0, slotLights != null ? slotLights.Length - 1 : 0);
        Vector3 target = SlotCenter(slot);
        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.cutFocus = target;
            follow.cutDistance = follow.distance * portalZoom;
            follow.cutWeight = 1f;
        }
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Fade(fade, 1f, 0f);

        // 3) 떨어진다: 가속하며 칸에 꽂힌다
        orb = LightOrb.Spawn(target + Vector3.up * fallHeight, orbSize);
        for (float t = 0f; t < fallTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fallTime;
            orb.transform.position = target + Vector3.up * fallHeight * (1f - k * k);
            yield return null;
        }
        Destroy(orb.gameObject);

        // 4) 번쩍: 화면이 하얗게 튀고, 빛이 크게 번지며 칸의 열쇠가 켜진다
        lit = Collected;
        RefreshLabel();
        if (slotGlows != null && slot < slotGlows.Length && slotGlows[slot] != null)
            foreach (ParticleSystem ps in slotGlows[slot].GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule m = ps.main;
                m.useUnscaledTime = true; // 멈춘 동안에도 빛 번짐이 보이게
                ps.Play();
            }
        LightOrb.Burst(target, orbSize, burstSize, 0.7f);
        UnityEngine.UI.Image flash = WarpBeam.ScreenOverlay(Color.white);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { flash.color = new Color(1f, 1f, 1f, 0.8f * (1f - t / 0.35f)); yield return null; }
        Destroy(flash.canvas.gameObject);
        yield return new WaitForSecondsRealtime(revealHold);

        // 5) 플레이어에게 돌아간다
        yield return Fade(fade, 0f, 1f);
        if (follow != null) follow.cutWeight = 0f;
        yield return new WaitForSecondsRealtime(0.1f);
        yield return Fade(fade, 1f, 0f);
        Destroy(fade.canvas.gameObject);

        for (int i = 0; i < controls.Length; i++) if (controls[i] != null) controls[i].enabled = wasOn[i];
        Time.timeScale = prevScale > 0f ? prevScale : 1f;
        cutsceneRunning = false;
        if (lit < Collected) { lit = Collected; RefreshLabel(); } // 연출 중에 하나 더 주운 경우
    }

    System.Collections.IEnumerator Fade(UnityEngine.UI.Image img, float a, float b)
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(a, b, t / fadeTime));
            yield return null;
        }
        img.color = new Color(img.color.r, img.color.g, img.color.b, b);
    }

    Vector3 SlotCenter(int i)
    {
        if (slotKeys != null && i < slotKeys.Length && slotKeys[i] != null)
        {
            Bounds b = new Bounds(slotKeys[i].position, Vector3.zero);
            foreach (Renderer r in slotParts[i]) b.Encapsulate(r.bounds);
            return b.center;
        }
        return slotLights != null && i < slotLights.Length && slotLights[i] != null ? slotLights[i].bounds.center : transform.position;
    }

    // 세이브 복구
    public void SetCollected(int n)
    {
        Collected = n;
        lit = n;
        RefreshLabel();
    }

    // 테스트용 (KillGauge의 ' 키): 열쇠를 다 모은 것으로 처리한다
    public void DebugCollectAll()
    {
        Collected = keysRequired;
        lit = Collected;
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (countLabel != null) countLabel.text = "열쇠 " + lit + "/" + keysRequired;
        if (slotLights == null) return;
        for (int i = 0; i < slotLights.Length; i++)
        {
            if (slotParts != null && slotParts[i] != null)
            {
                for (int j = 0; j < slotParts[i].Length; j++)
                    slotParts[i][j].sharedMaterial = i < lit ? slotPartMats[i][j] : slotOff;
                if (slotGlows[i] != null) slotGlows[i].SetActive(i < lit);
                continue;
            }
            if (slotLights[i] != null) slotLights[i].sharedMaterial = i < lit ? slotOn : slotOff;
        }
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
