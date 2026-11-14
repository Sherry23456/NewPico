using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 守望稻田告示牌 UI：开始/HUD/结算三面板 + 科普知识卡弹窗。
/// 仿 Assets/Scripts/UIController.cs 的脏检查写文本（世界空间 Canvas 文本重建很贵）。
/// 按钮 binding 在 Start 时自动挂（Builder 只摆物件不接事件）。
/// </summary>
public class FarmPestUI : MonoBehaviour
{
    [Header("面板")]
    public GameObject startPanel;
    public GameObject hudPanel;
    public GameObject reportPanel;
    public GameObject cardPanel;

    [Header("HUD 文本")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI pointsText;
    public TextMeshProUGUI remainText;
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI hintText;

    [Header("结算 / 知识卡")]
    public TextMeshProUGUI reportText;
    public TextMeshProUGUI reportCountdownText;
    public TextMeshProUGUI cardTitle;
    public TextMeshProUGUI cardBody;

    [Header("头顶任务栏（Idle/Report 显示，搜查中隐藏；小按钮可手动收起/展开）")]
    public GameObject headPanel;
    public Button hudStartButton;
    public Button hudToggleButton;
    public TextMeshProUGUI hudToggleLabel;

    [Header("按钮（Start 时自动绑定点击）")]
    public Button startButton;
    public Button againButton;
    public Button exportButton;

    string _countdown = "\u0001";   // 脏检查哨兵
    int _points = int.MinValue;
    int _wave = int.MinValue;
    int _remain = int.MinValue;
    Coroutine _card;

    void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (againButton != null) againButton.onClick.AddListener(OnStartClicked);
        if (exportButton != null) exportButton.onClick.AddListener(OnExportClicked);
        if (hudStartButton != null) hudStartButton.onClick.AddListener(OnStartClicked);
        if (hudToggleButton != null) hudToggleButton.onClick.AddListener(OnToggleHeadPanel);
        if (hintText != null)
            hintText.text = "走进田里找虫子 · 拍害虫 · 别误伤益虫 · 敲锣或按 P 开始";
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (againButton != null) againButton.onClick.RemoveListener(OnStartClicked);
        if (exportButton != null) exportButton.onClick.RemoveListener(OnExportClicked);
        if (hudStartButton != null) hudStartButton.onClick.RemoveListener(OnStartClicked);
        if (hudToggleButton != null) hudToggleButton.onClick.RemoveListener(OnToggleHeadPanel);
    }

    void OnStartClicked()
    {
        if (FarmPestManager.Instance != null) FarmPestManager.Instance.StartSession();
    }

    void OnExportClicked()
    {
        if (FarmPestManager.Instance != null) FarmPestManager.Instance.ExportCsv();
    }

    /// <summary>头顶说明面板手动收起/展开（下方常驻小按钮）。单字"收/开"：simkai 图集保证有字形。</summary>
    void OnToggleHeadPanel()
    {
        if (headPanel == null) return;
        bool show = !headPanel.activeSelf;
        headPanel.SetActive(show);
        if (hudToggleLabel != null) hudToggleLabel.text = show ? "收" : "开";
    }

    // ---------- 面板切换 ----------
    public void ShowStart()
    {
        SetPanel(startPanel, true);
        SetPanel(hudPanel, false);
        SetPanel(reportPanel, false);
        SetPanel(cardPanel, false);
        SetPanel(headPanel, true);
    }

    public void ShowHUD()
    {
        SetPanel(startPanel, false);
        SetPanel(hudPanel, true);
        SetPanel(reportPanel, false);
        SetPanel(headPanel, false);   // 搜查中收起头顶说明，别挡视线
    }

    public void ShowReport(string text)
    {
        SetPanel(hudPanel, false);
        SetPanel(reportPanel, true);
        SetPanel(cardPanel, false);
        SetPanel(headPanel, true);   // 结算后头顶栏回来，方便直接再来一轮
        if (reportText != null) reportText.text = text;
    }

    void SetPanel(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    // ---------- 数值（全部脏检查） ----------
    public void SetTime(int sec)
    {
        if (_wave == sec) return;
        _wave = sec;
        if (waveText != null) waveText.text = $"时间 {sec}s";
    }

    public void SetPoints(int p)
    {
        if (_points == p) return;
        _points = p;
        if (pointsText != null) pointsText.text = "守护分 " + p;
    }

    public void SetRemain(int n)
    {
        if (_remain == n) return;
        _remain = n;
        if (remainText != null) remainText.text = "本波剩余 " + n;
    }

    public void SetCountdown(string s)
    {
        if (_countdown == s) return;
        _countdown = s;
        if (countdownText != null) countdownText.text = s;
    }

    int _reportCd = int.MinValue;

    /// <summary>结算页倒计时（到 0 自动回待机）。</summary>
    public void SetReportCountdown(int n)
    {
        if (_reportCd == n) return;
        _reportCd = n;
        if (reportCountdownText != null)
            reportCountdownText.text = n > 0 ? $"{n} 秒后回到待机 · 点「再来一轮」立即继续" : "";
    }

    // ---------- 知识卡 ----------
    public void ShowCard(FarmPestVariant v)
    {
        if (cardPanel == null || v == null) return;
        if (cardTitle != null) cardTitle.text = "小知识 · " + v.displayName;
        if (cardBody != null) cardBody.text = v.factText;
        cardPanel.SetActive(true);
        if (_card != null) StopCoroutine(_card);
        _card = StartCoroutine(HideCardLater());
    }

    IEnumerator HideCardLater()
    {
        yield return new WaitForSeconds(4.5f);
        if (cardPanel != null) cardPanel.SetActive(false);
    }
}
