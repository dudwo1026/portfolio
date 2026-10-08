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
            int totalBullets = combat.PrimaryAmmo + combat.PrimaryReserve;
            // 유탄은 소총탄과 같은 칸에: 장전된 발 / 예비. 유탄은 소총에만 있다
            primarySmall.text = combat.primaryWeapon != PrimaryWeapon.Rifle
                ? totalBullets.ToString()
                : totalBullets + "   G " + combat.GrenadeLoaded + "/" + combat.GrenadeReserve;
        }

        if (secondaryBig != null) secondaryBig.text = combat.SecondaryAmmo.ToString();
        if (secondarySmall != null) secondarySmall.text = "∞"; // 권총은 예비탄 무제한
    }
}
