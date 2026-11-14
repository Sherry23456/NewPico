using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>中文 TMP 字体：优先用户自制的 SimHei SDF 资产，否则运行时从 SimHei.ttf 生成动态资产。全 UI 共用。</summary>
public static class FarmUIFont
{
    static TMP_FontAsset _cjk;
    public static bool Initialized => _cjk != null;

    /// <summary>编辑器阶段由 Builder 预载用户的 SDF 资产。</summary>
    public static void SetFont(TMP_FontAsset asset)
    {
        if (asset != null) _cjk = asset;
    }

    public static TMP_FontAsset CJK
    {
        get
        {
            if (_cjk == null)
            {
#if UNITY_EDITOR
                // 场景统一用楷体（simkai），找不到再退回黑体
                var sdf = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/simkai SDF.asset");
                if (sdf == null) sdf = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/simkai SDF.asset");
                if (sdf != null) { _cjk = sdf; return _cjk; }
                var hei = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/SimHei.ttf");
                if (hei == null) hei = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
                if (hei != null)
                {
                    _cjk = TMP_FontAsset.CreateFontAsset(hei, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                        AtlasPopulationMode.Dynamic);
                    _cjk.name = "SimHei_Dynamic";
                }
#endif
            }
            return _cjk;
        }
    }

    /// <summary>给 TMP 文本挂中文字体（空引用安全）。</summary>
    public static void Apply(TMP_Text tm)
    {
        if (tm != null && CJK != null) tm.font = CJK;
    }
}
