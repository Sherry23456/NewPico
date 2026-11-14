using System.Collections.Generic;
using UnityEngine;

/// <summary>极简音频表：Builder 把 Assets/Farm/Audio 的 wav 挂进 clips，运行时按名字播放。</summary>
public class FarmAudio : MonoBehaviour
{
    public static FarmAudio Instance { get; private set; }

    public AudioClip[] clips;
    readonly Dictionary<string, AudioClip> _map = new Dictionary<string, AudioClip>();

    void Awake()
    {
        Instance = this;
        if (clips != null)
            foreach (var c in clips)
                if (c != null) _map[c.name] = c;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Play(string name, Vector3 pos, float vol = 1f)
    {
        if (Instance == null || !Instance._map.TryGetValue(name, out var clip) || clip == null) return;
        var src = new GameObject("SFX_" + name);
        src.transform.position = pos;
        var a = src.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = vol;
        a.spatialBlend = 1f;
        a.minDistance = 1f;
        a.maxDistance = 18f;
        a.Play();
        Object.Destroy(src, clip.length + 0.1f);
    }
}
