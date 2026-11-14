using UnityEngine;

/// <summary>
/// 农学实验变量持有者：肥料库存（堆肥/化肥）与施用。
/// 堆肥 = 品质+1 倾向；化肥 = 成熟快一天但品质-1。
/// </summary>
public static class FarmExperiment
{
    public static int Compost { get; private set; }
    public static int Chem { get; private set; }
    public const int Cap = 5;

    public static bool AddCompost()
    {
        if (Compost >= Cap) return false;
        Compost++;
        return true;
    }

    public static bool BuyChem()
    {
        if (!FarmProgress.CanBuyChem) return false;
        if (Chem >= Cap) return false;
        if (FarmEconomy.Instance == null || !FarmEconomy.Instance.Spend(10)) return false;
        Chem++;
        return true;
    }

    /// <summary>对生长中地块施肥：优先堆肥，其次化肥。返回描述或 null。</summary>
    public static string ApplyTo(FarmPlot plot)
    {
        if (plot == null || !plot.CanFertilize()) return null;
        if (Compost > 0) { Compost--; plot.ApplyFertilizer("compost"); return "施了堆肥（品质+1 倾向）"; }
        if (Chem > 0) { Chem--; plot.ApplyFertilizer("chem"); return "施了化肥（催熟 1 天，品质-1）"; }
        return null;
    }
}
