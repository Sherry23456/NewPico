using System.Collections.Generic;
using UnityEngine;

/// <summary>作物静态定义表（显示名/生长早晨数/基础价）。阶段模型由场景内 Set_<id>/S0..S*n 提供。</summary>
public static class CropDB
{
    public class Def
    {
        public string Id;
        public string DisplayName;
        public int Stages;      // 视觉阶段数（含萌芽），生长早晨数 = Stages-1
        public int BasePrice;
        public string Pref;     // 偏好提示（M3 实验系统用）：sun/water/none
        public Def(string id, string name, int stages, int price, string pref)
        { Id = id; DisplayName = name; Stages = stages; BasePrice = price; Pref = pref; }
    }

    public static readonly Def[] All =
    {
        new Def("cabbage", "白菜", 2, 8,  "none"),
        new Def("carrot",  "胡萝卜", 2, 10, "none"),
        new Def("pepper",  "辣椒", 3, 14, "sun"),
        new Def("corn",    "玉米", 3, 16, "sun"),
    };

    public static Def Get(string id)
    {
        foreach (var d in All) if (d.Id == id) return d;
        return null;
    }
}
