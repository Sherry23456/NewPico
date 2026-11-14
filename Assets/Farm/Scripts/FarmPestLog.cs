using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 训练记录：每轮结算追加一条 JSON（persistentDataPath/farm_pest_log.json），
/// 一键导出 CSV（farm_pest_report.csv）——"数据可供教师/研究人员分析"的落点。
/// </summary>
public static class FarmPestLog
{
    [Serializable]
    public class Entry
    {
        public string date;
        public int kills;
        public int escapes;
        public int wrongHits;
        public int protectedGood;
        public int avgReactionMs;
        public int hitRate;
        public string grade;
        public int coins;
    }

    [Serializable]
    class EntryList { public System.Collections.Generic.List<Entry> sessions = new System.Collections.Generic.List<Entry>(); }

    public static string FilePath { get { return Application.persistentDataPath + "/farm_pest_log.json"; } }
    static string CsvPath { get { return Application.persistentDataPath + "/farm_pest_report.csv"; } }

    public static void Append(Entry e)
    {
        try
        {
            var list = Load();
            list.sessions.Add(e);
            File.WriteAllText(FilePath, JsonUtility.ToJson(list, true));
        }
        catch (Exception ex) { Debug.LogWarning("[守望稻田] 记录写入失败：" + ex.Message); }
    }

    public static Entry Last()
    {
        var list = Load();
        return list.sessions.Count > 0 ? list.sessions[list.sessions.Count - 1] : null;
    }

    static EntryList Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonUtility.FromJson<EntryList>(File.ReadAllText(FilePath));
                if (parsed != null) return parsed;
            }
        }
        catch { /* 文件损坏时当作空记录重新开始 */ }
        return new EntryList();
    }

    public static string ExportCsv()
    {
        var list = Load();
        if (list.sessions.Count == 0) return null;
        var sb = new System.Text.StringBuilder("时间,消灭,逃逸,误伤,保护益虫,平均反应ms,命中率%,评级,金币\n");
        foreach (var s in list.sessions)
            sb.Append($"{s.date},{s.kills},{s.escapes},{s.wrongHits},{s.protectedGood},{s.avgReactionMs},{s.hitRate},{s.grade},{s.coins}\n");
        try { File.WriteAllText(CsvPath, sb.ToString()); }
        catch (Exception ex) { Debug.LogWarning("[守望稻田] CSV 导出失败：" + ex.Message); return null; }
        Debug.Log("[守望稻田] CSV 已导出：" + CsvPath);
        return CsvPath;
    }
}
