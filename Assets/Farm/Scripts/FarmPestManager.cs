using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 守望稻田·虫情大搜查：田块与植株等比放大，害虫/益虫随机藏进植株间。
/// 规定时间内走进田里找到并拍掉全部害虫；拍到益虫误伤扣分弹知识卡。
/// 结算给守护报告（找到数/漏找/误伤/平均查找耗时/评级/历史对比）+ 金币经验 + JSON/CSV 记录。
/// Farm 场景自治，不依赖锤锤乐的 GameManager。
/// </summary>
public class FarmPestManager : MonoBehaviour
{
    public static FarmPestManager Instance { get; private set; }

    public enum Phase { Idle, Countdown, Searching, Report }
    public Phase State { get; private set; } = Phase.Idle;
    /// <summary>搜查进行中：PlotInteractor 据此抑制种田误触。</summary>
    public bool IsPlaying { get { return State == Phase.Searching; } }

    [Header("引用")]
    public FarmPlot[] plots;             // 虫子随机藏进这些田块
    public GameObject[] pestTemplates;   // 害虫：拍
    public GameObject[] goodTemplates;   // 益虫：护
    public FarmPestUI ui;

    [Header("搜查轮配置")]
    public float roundTime = 90f;
    public int pestCount = 14;
    public int goodCount = 5;
    [Header("藏匿高度（相对田面，虫会藏在植株间）")]
    public float bugMaxHeight = 0.8f;
    [Header("虫与虫最小间距（米）")]
    public float minBugSpacing = 1.0f;

    [Header("计分")]
    public int wrongHitPenalty = 5;
    public int protectBonus = 1;
    public int earlyClearBonus = 15;

    [Header("结算页停留秒数（倒计时完自动回待机）")]
    public float reportStay = 10f;

    // 轮内状态
    float _timeLeft;
    float _reportLeft;
    int _pestsLeft;
    readonly List<FarmPest> _active = new List<FarmPest>();
    readonly List<float> _findMs = new List<float>();
    float _roundStart;

    // 跨轮统计（会话内）
    int _points, _kills, _missed, _wrongHits, _protected;
    readonly Dictionary<string, int> _killCount = new Dictionary<string, int>();

    // 自适应（轮与轮之间调整，场景重载后还原配置值）
    CharacterController _cc;
    float _ccStepBackup;

    InputAction _startKey;   // PC 调试：P 开始

    void Awake()
    {
        Instance = this;
        _startKey = new InputAction("PestStart", InputActionType.Button, "<Keyboard>/p");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _startKey.Dispose();
    }

    void OnEnable() { _startKey.Enable(); }
    void OnDisable() { _startKey.Disable(); }

    void Start()
    {
        if (ui != null) ui.ShowStart();
    }

    void Update()
    {
        // PC 键鼠模式没有手柄，按 P 直接开局走流程（与 PlotInteractor 的键鼠后备风格一致）
        if (_startKey.WasPressedThisFrame() && (State == Phase.Idle || State == Phase.Report))
            StartSession();

        // 结算页倒计时：到 0 自动回待机
        if (State == Phase.Report)
        {
            _reportLeft -= Time.deltaTime;
            if (ui != null) ui.SetReportCountdown(Mathf.CeilToInt(Mathf.Max(0f, _reportLeft)));
            if (_reportLeft <= 0f)
            {
                State = Phase.Idle;
                if (ui != null) ui.ShowStart();
            }
            return;
        }

        if (State != Phase.Searching) return;
        _timeLeft -= Time.deltaTime;
        if (ui != null) ui.SetTime(Mathf.CeilToInt(Mathf.Max(0f, _timeLeft)));
        // 最后 5 秒中央大字倒计时
        if (ui != null) ui.SetCountdown(_timeLeft <= 5.5f ? Mathf.CeilToInt(Mathf.Max(1f, _timeLeft)).ToString() : "");
        if (_timeLeft <= 0f) EndRound("时间到");
    }

    // ---------- 开始 ----------
    public void StartSession()
    {
        if (State == Phase.Countdown || State == Phase.Searching) return;
        _points = 0; _kills = 0; _missed = 0; _wrongHits = 0; _protected = 0;
        _findMs.Clear(); _killCount.Clear();
        ClearBugs();
        StartCoroutine(SessionRoutine());
    }

    public void OnGongStruck()
    {
        if (State == Phase.Idle || State == Phase.Report) StartSession();
    }

    IEnumerator SessionRoutine()
    {
        State = Phase.Countdown;
        if (ui != null) { ui.ShowHUD(); ui.SetCountdown("3"); }
        FarmAudio.Play("ui", transform.position, 0.7f);
        yield return new WaitForSeconds(1f);
        for (int n = 2; n >= 1; n--)
        {
            if (ui != null) ui.SetCountdown(n.ToString());
            yield return new WaitForSeconds(1f);
        }
        if (ui != null) ui.SetCountdown("");

        SpawnRound();
        State = Phase.Searching;
        _timeLeft = roundTime;
        _roundStart = Time.time;
        if (ui != null) { ui.SetTime(Mathf.CeilToInt(roundTime)); ui.SetRemain(_pestsLeft); }
    }

    void SpawnRound()
    {
        // 抬高上台阶高度：玩家能走进抬高的田块，在植株间行走
        _cc = FindObjectOfType<CharacterController>();
        if (_cc != null)
        {
            _ccStepBackup = _cc.stepOffset;
            _cc.stepOffset = 0.7f;   // 田面高 0.45，允许直接踏上去
        }

        int spawned = 0;
        for (int i = 0; i < pestCount; i++)
            if (SpawnBug(false)) spawned++;
        for (int i = 0; i < goodCount; i++) SpawnBug(true);
        _pestsLeft = spawned;
    }

    bool SpawnBug(bool good)
    {
        if (plots == null || plots.Length == 0) return false;
        var pool = good ? goodTemplates : pestTemplates;
        if (pool == null || pool.Length == 0) return false;
        var template = pool[Random.Range(0, pool.Length)];
        if (template == null) return false;

        Vector3 pos;
        if (!TryFindHideSpot(out pos)) return false;

        var inst = PrefabPool.Get(template, null);
        var pest = inst != null ? inst.GetComponent<FarmPest>() : null;
        if (pest == null)
        {
            if (inst != null) PrefabPool.Release(inst);
            return false;
        }
        pest.onRetired = OnPestRetired;
        pest.Place(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        _active.Add(pest);
        return true;
    }

    /// <summary>随机田块 + 随机植株间位置（带最小间距检查）。</summary>
    bool TryFindHideSpot(out Vector3 pos)
    {
        pos = Vector3.zero;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var plot = plots[Random.Range(0, plots.Length)];
            if (plot == null) continue;
            var col = plot.GetComponent<Collider>();
            if (col == null) continue;
            var b = col.bounds;   // 只有田块 Box 自身，不含植株
            float plotTop = b.max.y;
            pos = new Vector3(
                Random.Range(b.min.x + 0.25f, b.max.x - 0.25f),
                plotTop + Mathf.Pow(Random.value, 1.5f) * bugMaxHeight,
                Random.Range(b.min.z + 0.2f, b.max.z - 0.2f));

            bool ok = true;
            foreach (var p in _active)
            {
                if (p != null && (p.transform.position - pos).sqrMagnitude < minBugSpacing * minBugSpacing)
                {
                    ok = false;
                    break;
                }
            }
            if (ok) return true;
        }
        return false;
    }

    // ---------- 结算 ----------
    void OnPestRetired(FarmPest pest, PestExit exit, float seconds)
    {
        _active.Remove(pest);
        var v = pest.Variant;
        if (v == null) return;
        Vector3 pos = pest.transform.position + Vector3.up * 0.3f;

        if (exit == PestExit.Swatted)
        {
            if (v.isBeneficial)
            {
                _wrongHits++;
                _points -= wrongHitPenalty;
                FarmAudio.Play("ui", pos, 0.9f);
                FarmEconomy.FloatText(pos, $"误伤{v.displayName}！-{wrongHitPenalty}");
                if (ui != null) ui.ShowCard(v);
            }
            else
            {
                _kills++;
                _pestsLeft--;
                _killCount.TryGetValue(v.displayName, out int n);
                _killCount[v.displayName] = n + 1;
                _findMs.Add((Time.time - _roundStart) * 1000f);
                _points += v.score > 0 ? v.score : 1;
                FarmAudio.Play("coin", pos, 0.8f);
                FarmEconomy.FloatText(pos, $"+{v.score} 拍中{v.displayName}");
                if (ui != null) ui.SetRemain(_pestsLeft);
                if (_pestsLeft <= 0 && State == Phase.Searching) EndRound("提前全部找到！");
            }
        }
        if (ui != null) ui.SetPoints(_points);
    }

    void EndRound(string reason)
    {
        State = Phase.Report;
        if (_cc != null) _cc.stepOffset = _ccStepBackup;
        _reportLeft = reportStay;
        if (ui != null) ui.SetCountdown("");

        _missed = Mathf.Max(0, _pestsLeft);
        // 没被拍到的益虫记保护
        foreach (var p in _active)
        {
            if (p == null) continue;
            var v = p.Variant;
            if (v != null && v.isBeneficial) _protected++;
            p.Despawn();
        }
        _active.Clear();

        int seen = _kills + _missed;
        float hitRate = seen > 0 ? (float)_kills / seen : 0f;
        float avgMs = 0f;
        foreach (var r in _findMs) avgMs += r;
        avgMs = _findMs.Count > 0 ? avgMs / _findMs.Count : 0f;
        string grade = Grade(hitRate);

        bool earlyClear = _missed == 0;
        int coins = Mathf.Max(0, _kills * 2 - _wrongHits * wrongHitPenalty) + (earlyClear ? earlyClearBonus : 0);
        int xp = Mathf.Max(1, _kills / 3);

        // 自适应：找得快 → 下轮虫更多时间更短；漏太多 → 放缓
        if (earlyClear && _timeLeft >= 30f)
        {
            pestCount += 2; goodCount += 1;
            roundTime = Mathf.Max(50f, roundTime - 10f);
        }
        else if (_missed >= 4)
        {
            pestCount = Mathf.Max(8, pestCount - 2);
            roundTime = Mathf.Min(120f, roundTime + 10f);
        }

        string lastLine = "";
        var last = FarmPestLog.Last();
        if (last != null)
            lastLine = $"上次：找到 {last.kills} · 漏 {last.escapes} · {last.grade} 级";

        FarmPestLog.Append(new FarmPestLog.Entry
        {
            date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            kills = _kills,
            escapes = _missed,          // 字段沿用：语义 = 漏找的害虫
            wrongHits = _wrongHits,
            protectedGood = _protected,
            avgReactionMs = Mathf.RoundToInt(avgMs),
            hitRate = Mathf.RoundToInt(hitRate * 100),
            grade = grade,
            coins = coins
        });

        FarmEconomy.AddCoins(coins);
        FarmProgress.AddXP(xp);

        string detail = "";
        foreach (var kv in _killCount) detail += $"{kv.Key}×{kv.Value}  ";
        if (detail.Length == 0) detail = "（一只都没找到）";

        if (ui != null)
        {
            ui.ShowReport(
                $"{reason}\n" +
                $"找到：{_kills}（{detail}）    漏找：{_missed}\n" +
                $"误伤益虫：{_wrongHits}    保护益虫：{_protected}\n" +
                $"找到率：{Mathf.RoundToInt(hitRate * 100)}%    平均查找：{avgMs / 1000f:0.0} 秒/只\n" +
                $"守护评级：{grade}    得分：{_points}    奖励：{coins} 金币 +{xp} 经验" +
                (lastLine.Length > 0 ? $"\n{lastLine}" : ""));
            // 随机补一张知识卡，强化"认识虫子"的教育点
            var all = new List<FarmPestVariant>();
            if (pestTemplates != null)
                foreach (var t in pestTemplates)
                    if (t != null) all.Add(t.GetComponent<FarmPestVariant>());
            if (goodTemplates != null)
                foreach (var t in goodTemplates)
                    if (t != null) all.Add(t.GetComponent<FarmPestVariant>());
            all.RemoveAll(x => x == null);
            if (all.Count > 0) ui.ShowCard(all[Random.Range(0, all.Count)]);
        }

        Debug.Log($"[守望稻田] {reason} 结算 {grade} 级：找到{_kills} 漏{_missed} 误伤{_wrongHits}，记录已写入 {FarmPestLog.FilePath}");
    }

    string Grade(float hitRate)
    {
        if (hitRate >= 0.9f && _wrongHits == 0) return "S";
        if (hitRate >= 0.75f && _wrongHits <= 1) return "A";
        if (hitRate >= 0.55f) return "B";
        return "C";
    }

    void ClearBugs()
    {
        foreach (var p in _active)
            if (p != null) p.Despawn();
        _active.Clear();
    }

    /// <summary>导出全部训练记录为 CSV（答辩数据用）。</summary>
    public void ExportCsv()
    {
        string p = FarmPestLog.ExportCsv();
        FarmEconomy.FloatText(transform.position + Vector3.up * 0.5f,
            string.IsNullOrEmpty(p) ? "还没有训练记录" : "已导出 farm_pest_report.csv");
    }
}
