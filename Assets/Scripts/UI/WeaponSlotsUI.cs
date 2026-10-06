using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotsUI : MonoBehaviour
{
    public PlayerCombat combat;
    public RectTransform primarySlot;
    public RectTransform secondarySlot;
    public RectTransform meleeSlot;

    public Vector2 normalSize = new Vector2(135f, 90f);
    public Vector2 selectedSize = new Vector2(185f, 126f);
    public Color normalColor = new Color(0.25f, 0.25f, 0.25f, 0.75f);
    public Color selectedColor = new Color(0.95f, 0.9f, 0.5f, 1f);
    public Color emptyColor = new Color(0.8f, 0.05f, 0.45f, 1f); // 장전된 탄창이 0일 때

    WeaponSlot lastSlot = (WeaponSlot)(-1);
    bool lastPrimaryEmpty;
    bool lastSecondaryEmpty;
    bool first = true;

    void Update()
    {
        if (combat == null) return;

        bool primaryEmpty = combat.PrimaryAmmo <= 0;   // 장전된 탄창이 빈 순간부터 (예비탄창 유무와 무관)
        bool secondaryEmpty = combat.SecondaryAmmo <= 0;

        if (!first && combat.currentSlot == lastSlot
            && primaryEmpty == lastPrimaryEmpty && secondaryEmpty == lastSecondaryEmpty) return;

        first = false;
        lastSlot = combat.currentSlot;
        lastPrimaryEmpty = primaryEmpty;
        lastSecondaryEmpty = secondaryEmpty;

        Apply(primarySlot, combat.currentSlot == WeaponSlot.Primary, primaryEmpty);
        Apply(secondarySlot, combat.currentSlot == WeaponSlot.Secondary, secondaryEmpty);
        Apply(meleeSlot, combat.currentSlot == WeaponSlot.Melee, false); // 근접은 탄약이 없다
    }

    void Apply(RectTransform slot, bool selected, bool empty)
    {
        if (slot == null) return;

        slot.sizeDelta = selected ? selectedSize : normalSize;

        Image img = slot.GetComponent<Image>();
        if (img == null) return;

        if (empty)
        {
            // 선택 여부는 크기로도 드러나므로, 비선택 슬롯은 같은 분홍을 어둡게만 해서 대비를 유지한다
            img.color = selected ? emptyColor : Dim(emptyColor);
            return;
        }
        img.color = selected ? selectedColor : normalColor;
    }

    Color Dim(Color c)
    {
        return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, normalColor.a);
    }
}
