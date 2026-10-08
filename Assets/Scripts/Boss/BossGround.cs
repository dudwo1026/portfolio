using UnityEngine;

// 보스 패턴의 워닝·소환물을 깔 '진짜 바닥' 높이.
// 아래로 훑어 맞은 면 중, 움직이는 것(플레이어·몹·보스 = Rigidbody)과 보스가 만든 것
// (토템·장애물·워닝·운석 등)을 빼고 가장 높은 면을 쓴다.
// 첫 번째로 맞은 면을 그대로 쓰면 토템이나 플레이어 머리 위에 워닝이 떠 버린다
public static class BossGround
{
    const float probeAbove = 3f; // 기준점보다 조금 위에서 쏜다. 기준점보다 높은 면은 아래에서 걸러낸다

    public static float Y(Vector3 point, float fallback)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * probeAbove, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.attachedRigidbody != null) continue;
            if (IsBossMade(c.transform)) continue;
            if (hits[i].point.y > point.y + 0.5f) continue; // 기준점보다 위에 걸린 것은 바닥이 아니다
            if (hits[i].point.y > best) best = hits[i].point.y;
        }
        return best > float.NegativeInfinity ? best : fallback;
    }

    // 보스가 만든 오브젝트는 이름이 Boss / Meteor 로 시작한다 (BossDefeat가 치우는 목록과 같은 규칙)
    static bool IsBossMade(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name.StartsWith("Boss") || t.name.StartsWith("Meteor")) return true;
        return false;
    }
}
