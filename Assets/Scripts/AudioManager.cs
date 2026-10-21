using UnityEngine;

// 全局 2D 音效播放器：任何脚本一行调用
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效")]
    public AudioClip popClip;        // 地鼠冒头
    public AudioClip explosionClip;  // 炸弹爆炸（用 thud 降调即可）
    [Range(0.2f, 2f)] public float explosionPitch = 0.7f;

    AudioSource source;

    void Awake()
    {
        Instance = this;
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip);
    }

    public void PlayPop()
    {
        PlayOneShot(popClip);
    }

    public void PlayExplosion()
    {
        if (explosionClip == null) return;
        source.pitch = explosionPitch;
        source.PlayOneShot(explosionClip);
        source.pitch = 1f;
    }
}
