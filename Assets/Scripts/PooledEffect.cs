using UnityEngine;

// 一次性粒子特效的池化回收入口（爆炸等）：
// 取出时清空 stopAction 防止粒子系统自毁池实例，播完后自动归还对象池。
[DisallowMultipleComponent]
public class PooledEffect : MonoBehaviour
{
    ParticleSystem[] systems;
    float maxLifetime;
    float deadline;

    void Awake()
    {
        CacheSystems();
        // 粒子系统自带的 stopAction=Destroy 会销毁池实例，必须去掉
        foreach (ParticleSystem ps in systems)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
        // 兜底存活上限：主循环时长 + 最长粒子寿命 + 余量
        float longest = 0f;
        foreach (ParticleSystem ps in systems)
            longest = Mathf.Max(longest, ps.main.duration + ps.main.startLifetime.constantMax);
        maxLifetime = longest + 0.5f;
        deadline = Time.time + maxLifetime;
    }

    void OnEnable()
    {
        if (systems == null || systems.Length == 0) CacheSystems();
        foreach (ParticleSystem ps in systems)
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true); // playOnAwake 首次已自动播，这里保证复用时确定性重播
        }
        deadline = Time.time + maxLifetime;
    }

    void Update()
    {
        bool anyAlive = false;
        foreach (ParticleSystem ps in systems)
        {
            if (ps != null && ps.IsAlive(true)) { anyAlive = true; break; }
        }
        if (!anyAlive || Time.time > deadline)
            PrefabPool.Release(gameObject);
    }

    void CacheSystems()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) systems = new ParticleSystem[0];
    }
}
