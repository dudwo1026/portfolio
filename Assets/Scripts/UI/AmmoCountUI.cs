using UnityEngine;
using UnityEngine.UI;

public class AmmoCountUI : MonoBehaviour
{
    public PlayerCombat combat;
    public Text primaryBig;
    public Text primarySmall;
    public Text secondaryBig;
    public Text secondarySmall;

    void Update()
    {
        if (combat == null) return;

        if (primaryBig != null) primaryBig.text = combat.PrimaryAmmo.ToString();
        if (primarySmall != null)
        {
            int totalBullets = combat.PrimaryAmmo + combat.PrimaryReserveMags * combat.primaryMagSize;
            // 유탄은 소총탄과 같은 칸에: 장전된 발 / 예비
            primarySmall.text = totalBullets + "   G " + combat.GrenadeLoaded + "/" + combat.GrenadeReserve;
        }

        if (secondaryBig != null) secondaryBig.text = combat.SecondaryAmmo.ToString();
        if (secondarySmall != null) secondarySmall.text = (combat.SecondaryAmmo + combat.SecondaryReserveMags * combat.secondaryMagSize).ToString();
    }
}
