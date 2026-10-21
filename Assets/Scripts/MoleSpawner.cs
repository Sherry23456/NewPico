using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 洞阵生成器：每隔一段时间随机挑一个空闲洞弹出地鼠，难度随时间加快
public class MoleSpawner : MonoBehaviour
{
    [Header("变体预制体")]
    public GameObject normalMole;
    public GameObject goldMole;
    public GameObject bomb;

    [Header("出现概率")]
    [Range(0f, 1f)] public float goldChance = 0.15f;
    [Range(0f, 1f)] public float bombChance = 0.15f;

    [Header("难度曲线：生成间隔从 startInterval 线性降到 endInterval")]
    public float startInterval = 1.2f;
    public float endInterval = 0.5f;
    public float gameDuration = 60f;

    [Header("冒头停留时间（秒）")]
    public float minStay = 0.8f;
    public float maxStay = 1.5f;

    Mole[] moles;
    Coroutine loop;

    void Awake()
    {
        Collect();
    }

    public void Collect()
    {
        moles = GetComponentsInChildren<Mole>(false);
    }

    public void StartSpawning()
    {
        StopLoop();
        loop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        StopLoop();
        if (moles == null) return;
        foreach (Mole m in moles) m.HideNow();
    }

    // 开新局前彻底清场
    public void ResetAll()
    {
        if (moles == null) return;
        foreach (Mole m in moles) m.ForceReset();
    }

    void StopLoop()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    IEnumerator SpawnLoop()
    {
        float elapsed = 0f;
        while (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            float interval = Mathf.Lerp(startInterval, endInterval, Mathf.Clamp01(elapsed / gameDuration));
            yield return new WaitForSeconds(interval);
            elapsed += interval;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) yield break;
            SpawnOne();
        }
    }

    void SpawnOne()
    {
        List<Mole> idle = new List<Mole>();
        foreach (Mole m in moles)
            if (m.IsIdle) idle.Add(m);
        if (idle.Count == 0) return;

        Mole mole = idle[Random.Range(0, idle.Count)];
        mole.Show(PickVariant(), Random.Range(minStay, maxStay), OnMoleRetired);
    }

    GameObject PickVariant()
    {
        float r = Random.value;
        if (r < bombChance) return bomb;
        if (r < bombChance + goldChance) return goldMole;
        return normalMole;
    }

    void OnMoleRetired(Mole mole)
    {
        // 生成节奏由协程控制，这里预留扩展位（比如连击断连判断）
    }
}
