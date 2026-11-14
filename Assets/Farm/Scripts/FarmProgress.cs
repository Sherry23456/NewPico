using UnityEngine;

/// <summary>玩家进度：经验/等级/解锁。收获得经验 = 数量×品质。</summary>
public static class FarmProgress
{
    static readonly int[] Thresholds = { 0, 10, 25, 45, 70 };   // Lv1..Lv5
    public static int XP { get; private set; }
    public static int Level { get; private set; } = 1;

    public static void AddXP(int amount)
    {
        XP += amount;
        int lv = 1;
        for (int i = 0; i < Thresholds.Length; i++)
            if (XP >= Thresholds[i]) lv = i + 1;
        Level = lv;
    }

    public static bool CanBuyChem => Level >= 2;       // Lv2 化肥购买
    public static bool CanDelegateWater => Level >= 3; // Lv3 委托浇水
    public static bool CanUseShade => Level >= 4;      // Lv4 遮阳棚
    public static bool CanFullAuto => Level >= 5;      // Lv5 全托管
}
