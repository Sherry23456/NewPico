using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 世界空间 UI 封装：开始界面 / 倒计时 / HUD / 结算 / 红闪
public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("面板")]
    public GameObject startPanel;
    public GameObject countdownPanel;
    public GameObject hudPanel;
    public GameObject gameoverPanel;

    [Header("文本")]
    public Text scoreText;
    public Text timerText;
    public Text countdownText;
    public Text finalScoreText;
    public Text bestScoreText;

    [Header("特效")]
    public Image flashImage;

    [Header("按钮（Awake 时自动绑定点击）")]
    public Button startButton;
    public Button restartButton;

    [Header("操作提示文本")]
    public Text hintText;

    void Awake()
    {
        Instance = this;
        if (startButton != null) startButton.onClick.AddListener(OnGameButtonClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnGameButtonClicked);
#if UNITY_EDITOR
        if (hintText != null)
            hintText.text = "编辑器：G捡锤 · 空格+鼠标=移右手 · Q/E=降升手 · 快速甩鼠标=敲 · 左键=点按钮 · N=丢";
#else
        if (hintText != null)
            hintText.text = "手柄靠近锤子，按住侧握键拾取；右手B / 左手Y放下；食指扳机点击开始";
#endif
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnGameButtonClicked);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnGameButtonClicked);
    }

    void OnGameButtonClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }

    public void ShowStart()
    {
        SetActive(startPanel, true);
        SetActive(countdownPanel, false);
        SetActive(hudPanel, false);
        SetActive(gameoverPanel, false);
    }

    public void ShowCountdownPanel()
    {
        SetActive(startPanel, false);
        SetActive(countdownPanel, true);
        SetActive(hudPanel, false);
        SetActive(gameoverPanel, false);
    }

    public void ShowHUD()
    {
        SetActive(countdownPanel, false);
        SetActive(hudPanel, true);
    }

    public void ShowGameOver(int score, int best)
    {
        SetActive(hudPanel, false);
        SetActive(countdownPanel, false);
        SetActive(gameoverPanel, true);
        if (finalScoreText != null) finalScoreText.text = "本局得分：" + score;
        if (bestScoreText != null) bestScoreText.text = "最高纪录：" + best;
    }

    public void SetScore(int s)
    {
        if (scoreText != null) scoreText.text = "得分 " + s;
    }

    public void SetTimer(int t)
    {
        if (timerText != null) timerText.text = t + " 秒";
    }

    public void ShowCountdown(string s)
    {
        if (countdownText != null) countdownText.text = s;
    }

    public void FlashRed()
    {
        StopCoroutine("FlashRoutine");
        StartCoroutine("FlashRoutine");
    }

    IEnumerator FlashRoutine()
    {
        if (flashImage == null) yield break;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            Color c = flashImage.color;
            c.a = Mathf.Lerp(0.6f, 0f, Mathf.Clamp01(t / 0.5f));
            flashImage.color = c;
            yield return null;
        }
        Color end = flashImage.color;
        end.a = 0f;
        flashImage.color = end;
    }

    void SetActive(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }
}
