using UnityEngine;

/// <summary>
/// 虫情变体数据：挂在每个虫子模板根节点上。
/// 害虫拍了得分，益虫拍了误伤（原打地鼠"炸弹机制"反转为"益虫保护"教育点）。
/// </summary>
public class FarmPestVariant : MonoBehaviour
{
    [Header("名称（飘字/结算/知识卡用）")]
    public string displayName = "蝗虫";

    [Header("true=益虫：不能拍，拍到扣分并弹知识卡")]
    public bool isBeneficial = false;

    [Header("拍中得分")]
    public int score = 1;

    [Header("科普知识卡文案")]
    [TextArea(2, 4)] public string factText = "";
}
