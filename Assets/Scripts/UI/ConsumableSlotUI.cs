using UnityEngine;
using UnityEngine.UI;

public class ConsumableSlotUI : MonoBehaviour
{
    public PlayerConsumables consumables;
    public Text countText;

    void Update()
    {
        if (consumables == null || countText == null) return;
        countText.text = "x" + consumables.count;
    }
}
