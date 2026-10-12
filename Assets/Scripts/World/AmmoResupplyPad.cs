using UnityEngine;
using UnityEngine.InputSystem;

public class AmmoResupplyPad : MonoBehaviour, IChargePad
{
    public float holdDuration = 3f;

    // 공통 캐스팅바(ChargeCastBarUI)가 읽어 간다
    public float Progress { get { return holdDuration > 0f ? Mathf.Clamp01(holdTimer / holdDuration) : 0f; } }

    // 캐스팅바 색은 장판 재질에서 가져온다. 나중에 장판 색을 바꾸면 바도 같이 따라간다
    public Color BarColor { get { return barColor; } }
    public string Prompt { get { return playerInside ? ChargeCastBarUI.HoldPrompt("탄약 보급") : null; } }

    bool playerInside;
    PlayerCombat playerCombat;
    float holdTimer;
    Color barColor = Color.white;

    void Awake()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null) barColor = r.sharedMaterial.color;
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerCombat pc = other.GetComponent<PlayerCombat>();
        if (pc != null)
        {
            playerInside = true;
            playerCombat = pc;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerCombat>() == playerCombat)
        {
            playerInside = false;
            playerCombat = null;
            holdTimer = 0f;
        }
    }

    void Update()
    {
        if (!playerInside || playerCombat == null) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.isPressed)
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.deltaTime;
        if (holdTimer >= holdDuration)
        {
            playerCombat.RefillAmmo();
            holdTimer = 0f;
        }
    }
}
