using UnityEngine;

// 결계로 잠기는 전투 구획 하나. 범위는 이 오브젝트 위치를 중심으로 가로(x) size.x, 세로(z) size.y
public class CombatZone : MonoBehaviour
{
    public Vector2 size = new Vector2(80f, 50f);
    public int keyReward = 1; // 클리어하면 떨어지는 열쇠 수
    public Vector3 portalPosition; // 이 구획으로 들어가는 포탈 자리 (월드)

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.2f, size.y));
        Gizmos.DrawWireSphere(portalPosition, 1.5f);
    }
}
