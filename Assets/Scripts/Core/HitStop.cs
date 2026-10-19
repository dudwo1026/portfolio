using System.Collections;
using UnityEngine;

// 처치 연출: 잠깐 멈췄다가(히트스톱) 느린 화면에서 서서히 원래 속도로 돌아온다 (실제 시간 기준).
// 보스·미니보스가 부른다. 부른 쪽이 바로 지워져도 끝까지 돌도록 따로 만든 오브젝트에서 돈다
public class HitStop : MonoBehaviour
{
    static HitStop runner;
    float baseFixedDelta = -1f;
    float lastSet = -1f; // 이 연출이 마지막으로 넣은 timeScale. -1이면 돌고 있지 않다

    // 처치 연출이 도는 중인지 (그 사이 일시정지를 열면 느린 화면이 끊긴다)
    public static bool Active { get { return runner != null && runner.lastSet >= 0f; } }

    public static void Play(float stopTime, float startScale, float recoverTime)
    {
        if (Time.timeScale <= 0f) return; // 이미 멈춰 있으면(게임오버·확인창·다른 히트스톱) 건드리지 않는다
        if (runner == null) runner = new GameObject("HitStop").AddComponent<HitStop>();
        runner.StopAllCoroutines();
        runner.StartCoroutine(runner.Run(stopTime, startScale, recoverTime));
    }

    IEnumerator Run(float stopTime, float startScale, float recoverTime)
    {
        if (baseFixedDelta < 0f) baseFixedDelta = Time.fixedDeltaTime;
        float set = 0f;
        Time.timeScale = set;
        lastSet = set;
        yield return new WaitForSecondsRealtime(stopTime);
        for (float t = 0f; t < recoverTime; t += Time.unscaledDeltaTime)
        {
            // 그 사이 일시정지·게임오버·확인창이 시간을 잡았으면 손을 뗀다 (그쪽이 다시 1로 돌린다)
            if (Time.timeScale != set) { End(false); yield break; }
            float n = t / recoverTime;
            set = Mathf.Lerp(startScale, 1f, n * n); // 처음엔 천천히, 끝에서 빠르게 풀린다
            Time.timeScale = set;
            Time.fixedDeltaTime = baseFixedDelta * set; // 느린 화면에서 물리가 뚝뚝 끊기지 않게
            lastSet = set;
            yield return null;
        }
        End(Time.timeScale == set);
    }

    void End(bool restoreScale)
    {
        Time.fixedDeltaTime = baseFixedDelta;
        if (restoreScale) Time.timeScale = 1f;
        lastSet = -1f;
    }

    // 씬이 바뀌어 지워져도 느린 화면으로 남지 않게
    void OnDestroy()
    {
        if (lastSet < 0f) return;
        Time.fixedDeltaTime = baseFixedDelta;
        if (Time.timeScale == lastSet) Time.timeScale = 1f;
    }
}
