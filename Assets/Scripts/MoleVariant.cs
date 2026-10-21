using UnityEngine;

// 弹出物变体配置：挂在冒头模型预制体根节点上，由 Mole 读取
public class MoleVariant : MonoBehaviour
{
    [Header("分值：普通鼠 1 / 金鼠 3 / 炸弹 -2")]
    public int score = 1;

    public bool isBomb = false;

    [Header("被打中时播放的音效")]
    public AudioClip hitSound;

    [Header("炸弹命中时的爆炸特效预制体")]
    public GameObject explosionPrefab;
}
