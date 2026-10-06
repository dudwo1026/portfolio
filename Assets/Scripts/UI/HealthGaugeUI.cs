using UnityEngine;
using UnityEngine.UI;

public class HealthGaugeUI : MonoBehaviour
{
    public Health target;
    public Image fillImage;
    // 넣어 두면 대상이 꺼져 있는 동안 이 오브젝트를 숨긴다.
    // 보스는 옥상에 올라가야 AreaManager가 켜 주므로, 그 전에는 체력바가 보이면 안 된다.
    // 플레이어 체력바는 비워 두면 예전 그대로 동작한다
    public GameObject root;

    void Update()
    {
        if (target == null || fillImage == null) return;

        if (root != null)
        {
            bool on = target.gameObject.activeInHierarchy;
            if (root.activeSelf != on) root.SetActive(on);
            if (!on) return;
        }

        fillImage.fillAmount = target.CurrentHealth / target.maxHealth;
    }
}
