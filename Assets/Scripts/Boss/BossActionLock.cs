using UnityEngine;

// 보스가 한 패턴에 묶여 있는 동안 다른 패턴이 끼어들지 못하게 하는 잠금.
// 분신 패턴이 '경로 확정'까지 이 잠금을 건다.
public class BossActionLock : MonoBehaviour
{
    float lockedUntil;

    public bool IsLocked { get { return Time.time < lockedUntil; } }

    public void LockFor(float seconds)
    {
        float until = Time.time + seconds;
        if (until > lockedUntil) lockedUntil = until;
    }
}
