using UnityEngine;

/// <summary>
/// 地块状态机：荒地→已翻土→生长中→成熟→（收获后回到已翻土）。
/// 阶段模型在场景内：Set_<cropId>/S0..S(n-1)，由 FarmSceneBuilder 预构建。
/// </summary>
public class FarmPlot : MonoBehaviour
{
    public enum PlotState { Wild, Tilled, Growing, Mature }
    public PlotState State = PlotState.Wild;

    public string cropId;
    public int stageIndex;
    public int waterCount;          // 0/1/2 壶
    public bool qualityPenalty;     // 浇 2 壶的徒长惩罚
    public string fertType = "";    // "" / "compost" / "chem"
    public bool fertApplied;
    public bool Shade;              // 遮阴（光变量）

    Renderer _base;
    Transform _ring;
    Transform _cloth;
    Color _soilDry = new Color(0.46f, 0.33f, 0.21f);
    Color _soilWet = new Color(0.27f, 0.18f, 0.11f);

    static readonly Color RingWild = new Color(1f, 1f, 1f, 0.9f);
    static readonly Color RingTilled = new Color(0.9f, 0.75f, 0.4f, 0.9f);
    static readonly Color RingThirsty = new Color(0.35f, 0.7f, 1f, 0.9f);
    static readonly Color RingWatered = new Color(0.4f, 0.55f, 1f, 0.5f);
    static readonly Color RingMature = new Color(0.35f, 1f, 0.45f, 0.95f);

    void Start()
    {
        _base = GetComponent<Renderer>();
        if (_base != null) _base.material = new Material(_base.material); // 独立实例，避免共享材质互相染色
        _ring = transform.Find("Ring");
        _cloth = transform.Find("ShadeCloth");
        if (_cloth != null) _cloth.gameObject.SetActive(Shade);
        ApplyState();
        ShowRing(false);
    }

    public bool CanTill() => State == PlotState.Wild;
    public bool CanPlant() => State == PlotState.Tilled;
    public bool CanWater() => State == PlotState.Growing && waterCount < 2;
    public bool CanHarvest() => State == PlotState.Mature;

    public void Till()
    {
        if (!CanTill()) return;
        State = PlotState.Tilled;
        ApplyState();
    }

    public void Plant(string id)
    {
        if (!CanPlant() || CropDB.Get(id) == null) return;
        cropId = id;
        stageIndex = 0;
        waterCount = 0;
        qualityPenalty = false;
        State = PlotState.Growing;
        ApplyState();
    }

    /// <summary>浇一壶。返回是否有效。</summary>
    public bool Water()
    {
        if (!CanWater()) return false;
        waterCount++;
        ApplyState();
        return true;
    }

    /// <summary>收获。返回 (数量, 品质星级)；失败返回 null。</summary>
    public int[] Harvest()
    {
        if (!CanHarvest()) return null;
        int count = Random.Range(1, 4);
        int quality = Random.Range(1, 4);
        if (qualityPenalty) quality = Mathf.Max(1, quality - 1);
        var def = CropDB.Get(cropId);
        if (def != null)
        {
            if (fertType == "compost") quality = Mathf.Min(3, quality + 1);
            if (fertType == "chem") quality = Mathf.Max(1, quality - 1);
            if (def.Pref == "sun") quality = Mathf.Clamp(quality + (Shade ? -1 : 1), 1, 3);
        }
        State = PlotState.Tilled;
        stageIndex = 0;
        waterCount = 0;
        qualityPenalty = false;
        fertType = "";
        fertApplied = false;
        ApplyState();
        return new[] { count, quality };
    }

    /// <summary>清晨结算：浇过水 +1 阶段；浇 2 壶或化肥催熟额外 +1 且品质 -1。</summary>
    public void OnMorning()
    {
        if (State != PlotState.Growing || waterCount <= 0) { waterCount = 0; ApplyState(); return; }
        stageIndex += 1;
        if (waterCount >= 2) { stageIndex += 1; qualityPenalty = true; }
        if (fertType == "chem") { stageIndex += 1; qualityPenalty = true; }
        var def = CropDB.Get(cropId);
        if (def != null && stageIndex >= def.Stages - 1)
        {
            stageIndex = def.Stages - 1;
            State = PlotState.Mature;
        }
        waterCount = 0;
        ApplyState();
    }

    public bool CanFertilize() => State == PlotState.Growing && !fertApplied;

    /// <summary>雨天自动补水一档（清晨由 FarmClock 调用）。</summary>
    public void RainBonus()
    {
        if (State == PlotState.Growing)
        {
            waterCount = Mathf.Min(2, waterCount + 1);
            ApplyState();
        }
    }

    public void ApplyFertilizer(string type)
    {
        fertType = type;
        fertApplied = true;
    }

    /// <summary>遮阳棚开合（Lv4 解锁，由交互器把关）。</summary>
    public void ToggleShade()
    {
        Shade = !Shade;
        if (_cloth != null) _cloth.gameObject.SetActive(Shade);
    }

    void ApplyState()
    {
        // 土壤颜色：浇水次数越多种得越深
        if (_base != null)
        {
            float w = Mathf.Clamp01(waterCount / 2f);
            _base.material.color = State == PlotState.Wild
                ? new Color(0.42f, 0.62f, 0.28f)
                : Color.Lerp(_soilDry, _soilWet, State == PlotState.Growing ? 0.35f + 0.65f * w : 0.15f);
        }
        // 作物阶段模型
        foreach (Transform child in transform)
            if (child.name.StartsWith("Set_"))
                child.gameObject.SetActive(false);
        if (State == PlotState.Growing || State == PlotState.Mature)
        {
            var set = transform.Find("Set_" + cropId);
            if (set != null)
            {
                set.gameObject.SetActive(true);
                for (int i = 0; i < set.childCount; i++)
                    set.GetChild(i).gameObject.SetActive(i == stageIndex);
            }
        }
        UpdateRingColor();
    }

    void UpdateRingColor()
    {
        if (_ring == null) return;
        var r = _ring.GetComponent<Renderer>();
        if (r == null) return;
        Color c;
        switch (State)
        {
            case PlotState.Wild: c = RingWild; break;
            case PlotState.Tilled: c = RingTilled; break;
            case PlotState.Growing: c = waterCount >= 2 ? RingWatered : (waterCount == 1 ? RingWatered : RingThirsty); break;
            default: c = RingMature; break;
        }
        r.material.color = c;
    }

    public void ShowRing(bool show)
    {
        if (_ring != null) _ring.gameObject.SetActive(show);
    }

    void Update()
    {
        // 成熟地块的环轻微呼吸提示
        if (_ring != null && _ring.gameObject.activeSelf && State == PlotState.Mature)
        {
            float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            _ring.localScale = new Vector3(s, 1f, s);
        }
    }
}
