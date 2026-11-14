using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 最小经济：收获入库存、进出货区自动卖出、公告板显示天数/金币/库存/种子。
/// </summary>
public class FarmEconomy : MonoBehaviour
{
    public static FarmEconomy Instance { get; private set; }

    public int coins = 20;
    readonly Dictionary<string, int> _inventory = new Dictionary<string, int>();
    TextMeshPro _board;
    float _boardTimer;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var boardGo = GameObject.Find("FarmBoard");
        if (boardGo != null) _board = boardGo.GetComponentInChildren<TextMeshPro>();
        RefreshBoard();
    }

    public static void AddHarvest(string cropName, int count, int quality)
    {
        if (Instance == null) return;
        var key = quality >= 3 ? cropName + "(★★★)" : quality == 2 ? cropName + "(★★)" : cropName;
        Instance._inventory.TryGetValue(key, out var n);
        Instance._inventory[key] = n + count;
        Instance.RefreshBoard();
    }

    /// <summary>直接发金币（守望稻田奖励等）：飘字 + 公告板刷新。</summary>
    public static void AddCoins(int amount)
    {
        if (Instance == null || amount == 0) return;
        Instance.coins = Mathf.Max(0, Instance.coins + amount);
        if (amount > 0) FarmAudio.Play("coin", Instance.transform.position, 0.8f);
        FloatText(Instance.transform.position + Vector3.up * 1.2f, $"{(amount > 0 ? "+" : "")}{amount} 金币");
        Instance.RefreshBoard();
    }

    /// <summary>出货区触发：全部卖出。</summary>
    public void SellAll()
    {
        if (_inventory.Count == 0)
        {
            FloatText(transform.position + Vector3.up * 1.6f, "没有可出售的作物");
            return;
        }
        int total = 0;
        foreach (var kv in _inventory)
        {
            int unit = PriceOf(kv.Key);
            total += unit * kv.Value;
        }
        coins += total;
        _inventory.Clear();
        FarmAudio.Play("coin", transform.position);
        FloatText(transform.position + Vector3.up * 1.6f, $"+{total} 金币");
        RefreshBoard();
    }

    static int PriceOf(string key)
    {
        int star = 1;
        if (key.Contains("★★★")) star = 3;
        else if (key.Contains("★★")) star = 2;
        string bare = key.Replace("(★★★)", "").Replace("(★★)", "");
        var names = new Dictionary<string, int> { { "白菜", 8 }, { "胡萝卜", 10 }, { "辣椒", 14 }, { "玉米", 16 } };
        return names.TryGetValue(bare, out var p) ? p * star : 5 * star;
    }

    /// <summary>花钱（买化肥等）。成功扣款返回 true。</summary>
    public bool Spend(int amount)
    {
        if (coins < amount) return false;
        coins -= amount;
        RefreshBoard();
        return true;
    }

    /// <summary>玩家走进出货区 → 自动卖出。</summary>
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CharacterController>() != null) SellAll();
    }

    void Update()
    {
        _boardTimer += Time.deltaTime;
        if (_boardTimer > 0.5f)
        {
            _boardTimer = 0f;
            RefreshBoard();
            // 公告板始终面向玩家（billboard + 180°：TMP 可读面为 -z 侧，经验证）
            if (_board != null && Camera.main != null)
            {
                var b = _board.transform;
                var camP = Camera.main.transform.position;
                b.LookAt(new Vector3(camP.x, b.position.y, camP.z));
                b.Rotate(0f, 180f, 0f);
            }
        }
    }

    void RefreshBoard()
    {
        if (_board == null) return;
        string inv = _inventory.Count == 0 ? "空" : "有存货";
        _board.text = $"金币 {coins}    Lv{FarmProgress.Level}（经验 {FarmProgress.XP}）\n持有: {inv}    守望稻田：走进田里拍害虫，别误伤益虫";
    }

    public static void FloatText(Vector3 pos, string msg)
    {
        if (Instance == null) return;
        var go = new GameObject("FloatText");
        go.transform.position = pos;
        var tm = go.AddComponent<TextMeshPro>();
        FarmUIFont.Apply(tm);
        tm.text = msg;
        tm.fontSize = 0.5f;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = new Color(1f, 0.95f, 0.6f);
        go.AddComponent<FarmFloater>();
    }
}

/// <summary>漂浮文字：上浮 2 秒后销毁。</summary>
public class FarmFloater : MonoBehaviour
{
    float _t;
    TextMeshPro _tm;
    void Start() { _tm = GetComponent<TextMeshPro>(); }
    void Update()
    {
        _t += Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 0.5f;
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0f, 180f, 0f);
        }
        if (_tm != null) _tm.color = new Color(1f, 0.95f, 0.6f, Mathf.Clamp01(2f - _t));
        if (_t > 2f) Destroy(gameObject);
    }
}
