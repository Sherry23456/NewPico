using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 顶部 HUD：任务提示。uGUI 世界空间画布（跟随视角）。
/// 天气/时段按钮待参考项目的时间系统接入后恢复。
/// </summary>
public class FarmHUD : MonoBehaviour
{
    TMP_Text _quest;

    float _tick;

    void Start()
    {
        _quest = transform.Find("QuestText")?.GetComponent<TMP_Text>();
    }

    void Update()
    {
        _tick += Time.deltaTime;
        if (_tick < 0.35f) return;
        _tick = 0f;

        if (_quest != null) _quest.text = "任务: " + GetQuestText();
    }

    /// <summary>按地块状态生成当前任务提示。</summary>
    string GetQuestText()
    {
        var plots = Object.FindObjectsOfType<FarmPlot>();
        int wild = 0, tilled = 0, thirsty = 0, mature = 0;
        foreach (var p in plots)
        {
            if (p.CanTill()) wild++;
            else if (p.CanPlant()) tilled++;
            else if (p.CanWater()) thirsty++;
            else if (p.CanHarvest()) mature++;
        }
        if (mature > 0) return $"收获成熟的作物 ({mature})";
        if (thirsty > 0) return $"给口渴的地块浇水 ({thirsty})";
        if (tilled > 0) return "播种（先在公告板选种子）";
        if (wild > 0) return $"翻土开垦 ({wild})";
        return "等待作物生长…";
    }
}
