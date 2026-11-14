using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// 右手扳机（grip）对准地块交互：荒地翻土 / 已翻土播种 / 生长浇水 / 成熟收获。
/// 左手 A 键切换种子。策略案 §5 混合式交互的指向侧；物理化抓取在 M4 升级。
/// </summary>
public class PlotInteractor : MonoBehaviour
{
    public float range = 6f;
    InputAction _act;      // 右手 grip
    InputAction _seed;     // 左手 primary（切种子）
    InputAction _fert;     // 左手 secondary（施肥）
    Transform _hand;
    Transform _cam;
    FarmPlot _aim;
    TextMeshPro _seedLabel;
    int _seedIndex;
    bool _lastGrip;
    bool _lastSeed;
    bool _lastFert;
    Transform _mill, _stand, _pergola;

    public static string SelectedSeedId = "cabbage";

    void Awake()
    {
        _act = new InputAction("FarmAct", InputActionType.Button, "<XRController>{RightHand}/gripButton");
        _act.AddBinding("<Keyboard>/r");
        _act.AddBinding("<Mouse>/leftButton");
        _seed = new InputAction("FarmSeed", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        _seed.AddBinding("<Keyboard>/tab");
        _fert = new InputAction("FarmFert", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        _fert.AddBinding("<Keyboard>/f");
    }

    /// <summary>PC 模式下从鼠标位置发射线；VR 模式从右手柄。</summary>
    Ray AimRay()
    {
        if (FarmPC.PCMode && Camera.main != null)
            return Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (_hand != null) return new Ray(_hand.position, _hand.forward);
        return new Ray(transform.position + Vector3.up, transform.forward);
    }

    void OnEnable() { _act.Enable(); _seed.Enable(); _fert.Enable(); }
    void OnDisable() { _act.Disable(); _seed.Disable(); _fert.Disable(); }
    void OnDestroy() { _act.Dispose(); _seed.Dispose(); _fert.Dispose(); }

    void Start()
    {
        _hand = transform.Find("Camera Offset/Right Controller");
        _cam = GetComponentInChildren<Camera>() != null ? GetComponentInChildren<Camera>().transform : null;
        _mill = GameObject.Find("CN_Mill") != null ? GameObject.Find("CN_Mill").transform : null;
        var stand = GameObject.Find("MarketStand");
        if (stand != null) _stand = stand.transform;
        var perg = GameObject.Find("Pergola");
        if (perg != null) _pergola = perg.transform;

        var seedGo = new GameObject("SeedLabel");
        _seedLabel = seedGo.AddComponent<TextMeshPro>();
        FarmUIFont.Apply(_seedLabel);
        _seedLabel.fontSize = 0.35f;
        _seedLabel.alignment = TextAlignmentOptions.Center;
        _seedLabel.rectTransform.sizeDelta = new Vector2(1.2f, 0.35f);
        seedGo.transform.SetParent(transform.Find("Camera Offset/Left Controller"), false);
        seedGo.transform.localPosition = new Vector3(0f, 0.12f, 0.06f);
        seedGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        UpdateSeedLabel();
    }

    void Update()
    {
        bool seedNow = _seed.IsPressed();
        if (seedNow && !_lastSeed)
        {
            _seedIndex = (_seedIndex + 1) % CropDB.All.Length;
            SelectedSeedId = CropDB.All[_seedIndex].Id;
            UpdateSeedLabel();
            FarmAudio.Play("ui", _hand != null ? _hand.position : transform.position, 0.5f);
        }
        _lastSeed = seedNow;

        // 左手 B 键：对生长中地块施肥（优先堆肥）
        bool fertNow = _fert.IsPressed();
        if (fertNow && !_lastFert && _aim != null && _aim.CanFertilize())
        {
            string res = FarmExperiment.ApplyTo(_aim);
            FarmEconomy.FloatText(_aim.transform.position + Vector3.up * 0.7f,
                res ?? "没有肥料：石磨收堆肥 / 集市买化肥");
            if (res != null) FarmAudio.Play("plant", _aim.transform.position, 0.5f);
        }
        _lastFert = fertNow;

        // 瞄准地块
        FarmPlot aim = null;
        {
            var ray = AimRay();
            if (Physics.Raycast(ray, out var hit, range + 6f))
                aim = hit.collider.GetComponentInParent<FarmPlot>();
        }
        if (aim != _aim)
        {
            if (_aim != null) _aim.ShowRing(false);
            _aim = aim;
            if (_aim != null) _aim.ShowRing(true);
        }

        // 扳机动作（按下沿触发）：瞄准地块 → 地块动作；空手 → 站点交互
        // 虫情进行中：右手专注拍虫，抑制种田/站点误触
        bool grip = _act.IsPressed();
        if (grip && !_lastGrip)
        {
            if (FarmPestManager.Instance == null || !FarmPestManager.Instance.IsPlaying)
            {
                if (_aim != null) DoAction(_aim);
                else StationAction();
            }
        }
        _lastGrip = grip;
    }

    /// <summary>空手扳机：靠近石磨/集市/遮阳棚时的站点交互。</summary>
    void StationAction()
    {
        if (_hand == null) return;
        Vector3 p = transform.position;   // 玩家脚底

        if (_mill != null && Vector3.Distance(p, _mill.position) < 2.8f)
        {
            bool ok = FarmExperiment.AddCompost();
            FarmEconomy.FloatText(_mill.position + Vector3.up * 1.2f,
                ok ? "+1 堆肥" : "堆肥已经带满 5 份了");
            FarmAudio.Play("plant", _mill.position, 0.6f);
            return;
        }
        if (_stand != null && Vector3.Distance(p, _stand.position) < 2.8f)
        {
            if (!FarmProgress.CanBuyChem)
            {
                FarmEconomy.FloatText(_stand.position + Vector3.up * 1.4f, "化肥要 Lv2 才能买");
                return;
            }
            bool ok = FarmExperiment.BuyChem();
            FarmEconomy.FloatText(_stand.position + Vector3.up * 1.4f,
                ok ? "-10 金币  +1 化肥" : "买不了：金币不足或已带满 5 袋");
            FarmAudio.Play("coin", _stand.position, 0.6f);
            return;
        }
        if (_pergola != null && Vector3.Distance(p, _pergola.position) < 3.2f)
        {
            if (!FarmProgress.CanUseShade)
            {
                FarmEconomy.FloatText(_pergola.position + Vector3.up * 1.6f, "遮阳棚要 Lv4 才能搭");
                return;
            }
            // 开/关最近地块的遮阴
            FarmPlot best = null; float bd = 4.5f;
            foreach (var plot in Object.FindObjectsOfType<FarmPlot>())
            {
                float d = Vector3.Distance(plot.transform.position, _pergola.position);
                if (d < bd) { bd = d; best = plot; }
            }
            if (best != null)
            {
                best.ToggleShade();
                FarmEconomy.FloatText(best.transform.position + Vector3.up * 1.2f,
                    best.Shade ? "遮阳棚拉上了" : "遮阳棚拉开了");
                FarmAudio.Play("ui", best.transform.position);
            }
        }
    }

    void DoAction(FarmPlot plot)
    {
        var pos = plot.transform.position + Vector3.up * 0.4f;
        if (plot.CanTill())
        {
            plot.Till();
            FarmAudio.Play("dig", pos);
            Haptic(0.7f);
        }
        else if (plot.CanPlant())
        {
            plot.Plant(SelectedSeedId);
            FarmAudio.Play("plant", pos);
            Haptic(0.4f);
        }
        else if (plot.CanWater())
        {
            plot.Water();
            FarmAudio.Play("water", pos, 0.8f);
            Haptic(0.3f);
        }
        else if (plot.CanHarvest())
        {
            var res = plot.Harvest();
            if (res != null)
            {
                var def = CropDB.Get(plot.cropId);
                string cropName = def != null ? def.DisplayName : plot.cropId;
                FarmEconomy.AddHarvest(cropName, res[0], res[1]);
                FarmAudio.Play("harvest", pos);
                Haptic(0.9f);
                FarmEconomy.FloatText(pos + Vector3.up * 0.5f,
                    $"+{res[0]} {cropName} {"★".PadLeft(res[1], '★')}");
            }
        }
    }

    void Haptic(float amp)
    {
        try
        {
            var dev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (dev.isValid) dev.SendHapticImpulse(0, Mathf.Clamp01(amp), 0.08f);
        }
        catch { /* 设备不可用时静默 */ }
    }

    void UpdateSeedLabel()
    {
        var d = CropDB.Get(SelectedSeedId);
        if (_seedLabel != null && d != null) _seedLabel.text = $"种子: {d.DisplayName}";
    }
}
