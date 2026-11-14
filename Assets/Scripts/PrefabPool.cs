using System.Collections.Generic;
using UnityEngine;

// 简易预制体对象池：按 prefab 复用实例，替代高频 Instantiate/Destroy。
// PICO 真机上频繁实例化/销毁（地鼠模型、爆炸特效）造成的 GC 压力
// 是"越玩越卡"的主因，统一走本池后全程零销毁。
public static class PrefabPool
{
    static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();

    static Transform poolRoot;
    static Transform PoolRoot
    {
        get
        {
            if (poolRoot == null)
            {
                GameObject go = new GameObject("~PrefabPool");
                Object.DontDestroyOnLoad(go);
                poolRoot = go.transform;
            }
            return poolRoot;
        }
    }

    // 取出一个实例（挂到 parent 下）；池空时才真正 Instantiate
    public static GameObject Get(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;
        Stack<GameObject> stack;
        if (!pools.TryGetValue(prefab, out stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }

        GameObject go = null;
        while (stack.Count > 0 && go == null)
        {
            go = stack.Pop(); // 弹到 null 说明实例已被外部销毁（场景卸载等），继续找
        }

        if (go == null)
        {
            go = Object.Instantiate(prefab);
            PooledInstance created = go.AddComponent<PooledInstance>();
            created.prefab = prefab;
            created.originalScale = go.transform.localScale;
            if (go.GetComponentInChildren<ParticleSystem>(true) != null)
                go.AddComponent<PooledEffect>(); // 一次性粒子特效：播完自动归还
        }

        PooledInstance pi = go.GetComponent<PooledInstance>();
        go.transform.SetParent(parent != null ? parent : PoolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = pi.originalScale; // 还原被压扁动画改掉的缩放
        go.SetActive(true);
        return go;
    }

    // 归还实例：藏到池根下并停用，零销毁
    public static void Release(GameObject instance)
    {
        if (instance == null) return;
        PooledInstance pi = instance.GetComponent<PooledInstance>();
        if (pi == null || pi.prefab == null)
        {
            Object.Destroy(instance); // 不是池里出来的，退回销毁
            return;
        }

        Stack<GameObject> stack;
        if (!pools.TryGetValue(pi.prefab, out stack))
        {
            stack = new Stack<GameObject>();
            pools[pi.prefab] = stack;
        }

        instance.SetActive(false);
        instance.transform.SetParent(PoolRoot, false);
        stack.Push(instance);
    }
}

// 记录池实例与预制体的对应关系及原始缩放（压扁动画等会改 scale，取出时还原）
public class PooledInstance : MonoBehaviour
{
    [HideInInspector] public GameObject prefab;
    [HideInInspector] public Vector3 originalScale;
}
