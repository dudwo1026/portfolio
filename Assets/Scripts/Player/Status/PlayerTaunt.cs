using System.Collections.Generic;
using UnityEngine;

// 도발: every enemy on the player's own map chases them regardless of distance or line of sight.
// Rescanned periodically so enemies that spawn during the taunt join in too.
public class PlayerTaunt : MonoBehaviour
{
    public float duration = 30f;
    public float rescanInterval = 0.25f;

    public bool IsActive => timer > 0f;

    float timer;
    float rescanTimer;
    readonly List<EnemyAI> aggroed = new List<EnemyAI>();
    Transform[] mapCenters;

    void Awake()
    {
        List<Transform> maps = new List<Transform>();
        GameObject m1 = GameObject.Find("Map1");
        GameObject m2 = GameObject.Find("Map2");
        if (m1 != null) maps.Add(m1.transform);
        if (m2 != null) maps.Add(m2.transform);
        mapCenters = maps.ToArray();
    }

    public void Apply()
    {
        Apply(duration);
    }

    public void Apply(float seconds)
    {
        if (seconds <= 0f) return;
        if (seconds > timer) timer = seconds; // refresh, never shorten
        rescanTimer = 0f;
    }

    void Update()
    {
        if (timer <= 0f) return;

        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = rescanInterval;
            Rescan();
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = 0f;
        ReleaseAll();
    }

    void Rescan()
    {
        Transform myMap = NearestMap(transform.position);

        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI e in enemies)
        {
            if (e == null) continue;
            if (NearestMap(e.transform.position) != myMap) continue; // 다른 맵의 적은 제외

            if (!e.forcedAggro)
            {
                e.forcedAggro = true;
                e.ForceAttack();
            }
            if (!aggroed.Contains(e)) aggroed.Add(e);
        }
    }

    void ReleaseAll()
    {
        foreach (EnemyAI e in aggroed)
            if (e != null) e.forcedAggro = false;
        aggroed.Clear();
    }

    Transform NearestMap(Vector3 pos)
    {
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < mapCenters.Length; i++)
        {
            Vector3 d = mapCenters[i].position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = mapCenters[i]; }
        }
        return best;
    }

    void OnDisable()
    {
        if (timer > 0f) { timer = 0f; ReleaseAll(); }
    }
}
