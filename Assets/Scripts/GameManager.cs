using UnityEngine;

public enum GameState { Idle, Countdown, Playing, Over }

// 全局状态机：管分数、倒计时、最高分（PlayerPrefs），驱动生成器与 UI
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Idle;
    public bool IsPlaying { get { return State == GameState.Playing; } }

    [Header("引用")]
    public MoleSpawner spawner;
    public UIController ui;

    [Header("参数")]
    public float gameDuration = 60f;
    public float countdownDuration = 3f;

    public int Score { get; private set; }
    public float TimeLeft { get; private set; }
    public int Best { get; private set; }

    float countdownLeft;

    void Awake()
    {
        Instance = this;
        Best = PlayerPrefs.GetInt("WhackAMoleBest", 0);
    }

    void Start()
    {
        if (ui != null) ui.ShowStart();
    }

    void Update()
    {
        if (State == GameState.Playing)
        {
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                if (ui != null) ui.SetTimer(0);
                EndGame();
            }
            else if (ui != null)
            {
                ui.SetTimer(Mathf.CeilToInt(TimeLeft));
            }
        }
        else if (State == GameState.Countdown)
        {
            countdownLeft -= Time.deltaTime;
            int n = Mathf.CeilToInt(countdownLeft);
            if (ui != null) ui.ShowCountdown(n > 0 ? n.ToString() : "GO!");
            if (countdownLeft <= -0.6f) BeginPlaying();
        }
    }

    // 开始 / 再来一局按钮共用
    public void StartGame()
    {
        if (State == GameState.Countdown || State == GameState.Playing) return;
        Score = 0;
        TimeLeft = gameDuration;
        countdownLeft = countdownDuration;
        if (spawner != null) spawner.ResetAll();
        if (ui != null)
        {
            ui.SetScore(0);
            ui.SetTimer(Mathf.CeilToInt(gameDuration));
            ui.ShowCountdownPanel();
            ui.ShowCountdown("3");
        }
        State = GameState.Countdown;
    }

    void BeginPlaying()
    {
        State = GameState.Playing;
        if (ui != null) ui.ShowHUD();
        if (spawner != null) spawner.StartSpawning();
    }

    void EndGame()
    {
        State = GameState.Over;
        if (spawner != null) spawner.StopSpawning();
        if (Score > Best)
        {
            Best = Score;
            PlayerPrefs.SetInt("WhackAMoleBest", Best);
        }
        if (ui != null) ui.ShowGameOver(Score, Best);
    }

    public void AddScore(int delta)
    {
        if (!IsPlaying) return;
        Score += delta;
        if (Score < 0) Score = 0;
        if (ui != null) ui.SetScore(Score);
    }

    // 炸弹命中：红闪 + 爆炸音
    public void OnBombHit()
    {
        if (ui != null) ui.FlashRed();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
    }
}
