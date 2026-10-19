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
    public Color emptyColor = new Color(0.9f, 0.08f, 0.08f, 1f);    // 장전된 탄창이 0일 때 (빨강)
    public Color reloadColor = new Color(0.8f, 0.05f, 0.45f, 1f);   // 장전하는 동안 (분홍)

    WeaponSlot lastSlot = (WeaponSlot)(-1);
    int lastState = -1;
    bool first = true;

    void Update()
    {
        if (combat == null) return;

        bool primaryEmpty = combat.PrimaryAmmo <= 0;   // 장전된 탄창이 빈 순간부터 (예비탄창 유무와 무관)
        bool secondaryEmpty = combat.SecondaryAmmo <= 0;
        bool reloading = combat.IsReloading;           // 장전은 지금 든 무기에만 걸린다
        int state = (primaryEmpty ? 1 : 0) | (secondaryEmpty ? 2 : 0) | (reloading ? 4 : 0);

        if (!first && combat.currentSlot == lastSlot && state == lastState) return;

        first = false;
        lastSlot = combat.currentSlot;
        lastState = state;

        bool p = combat.currentSlot == WeaponSlot.Primary, s = combat.currentSlot == WeaponSlot.Secondary;
        Apply(primarySlot, p, p && reloading ? reloadColor : primaryEmpty ? emptyColor : (Color?)null);
        Apply(secondarySlot, s, s && reloading ? reloadColor : secondaryEmpty ? emptyColor : (Color?)null);
        Apply(meleeSlot, combat.currentSlot == WeaponSlot.Melee, null); // 근접은 탄약이 없다
    }

    void Apply(RectTransform slot, bool selected, Color? warn)
    {
        if (slot == null) return;

        Vector2 size = selected ? selectedSize : normalSize;
        if (slot.sizeDelta != size)
        {
            slot.sizeDelta = size;
            // 슬롯들이 세로 정렬 상자(WeaponSlots) 안에 있으므로, 크기가 바뀌면 간격을 다시 맞춘다
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(slot.parent as RectTransform);
        }

        Image img = slot.GetComponent<Image>();
        if (img == null) return;

        if (warn.HasValue)
        {
            // 선택 여부는 크기로도 드러나므로, 비선택 슬롯은 같은 색을 어둡게만 해서 대비를 유지한다
            img.color = selected ? warn.Value : Dim(warn.Value);
            return;
        }
        img.color = selected ? selectedColor : normalColor;
    }

    Color Dim(Color c)
    {
        return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, normalColor.a);
    }
}
