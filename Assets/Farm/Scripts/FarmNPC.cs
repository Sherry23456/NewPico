using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// NPC 禾伯：靠近按扳机推进对话；Lv3 可委托浇水（清晨可见劳作巡逻）；
/// Lv5 全托管（自动种/浇/收，产出少一个）。
/// </summary>
public class FarmNPC : MonoBehaviour
{
    public static FarmNPC Instance { get; private set; }

    public float talkRange = 2.4f;
    InputAction _grip;
    Transform _player;
    TextMeshPro _bubble;
    TextMeshPro _hint;
    bool _lastGrip;
    int _dialogIndex;
    bool _delegatedLineArmed;

    public bool DelegatedWater { get; private set; }
    public bool FullAuto { get; private set; }

    static readonly string[] Tutorial =
    {
        "回乡啦？院里这几垄地还荒着。\n先拿扳机翻土，播下种子，记得浇水。",
        "老话说：水浇两壶，苗蹿得快，可长得傻。\n一壶刚好，两壶催熟还伤品质。",
        "石磨那儿能收堆肥，集市能买化肥。\n堆肥养地，化肥催苗——自己权衡。",
        "辣椒玉米是太阳面子货，白菜胡萝卜随和。\n遮阳棚给谁用，你说了算。",
        "收成别攒着，村口集市收购。\n走过去站定，钱就到账。",
    };

    void Awake()
    {
        Instance = this;
        _grip = new InputAction("NPCGrip", InputActionType.Button, "<XRController>{RightHand}/gripButton");
        _grip.AddBinding("<Keyboard>/r");
        _grip.AddBinding("<Keyboard>/e");
        _grip.AddBinding("<Mouse>/leftButton");
    }

    void OnDestroy() { if (Instance == this) Instance = null; _grip?.Dispose(); }

    void OnEnable() { _grip?.Enable(); }
    void OnDisable() { _grip?.Disable(); }

    void Start()
    {
        var origin = GameObject.Find("XR Origin (XR Rig)");
        if (origin != null) _player = origin.transform;

        var bubbleGo = new GameObject("NPCBubble");
        _bubble = bubbleGo.AddComponent<TextMeshPro>();
        FarmUIFont.Apply(_bubble);
        _bubble.fontSize = 0.28f;
        _bubble.alignment = TextAlignmentOptions.Center;
        _bubble.color = new Color(1f, 0.98f, 0.9f);
        _bubble.rectTransform.sizeDelta = new Vector2(1.8f, 1.0f);
        bubbleGo.transform.SetParent(transform, false);
        bubbleGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
        bubbleGo.gameObject.SetActive(false);

        var hintGo = new GameObject("NPCHint");
        _hint = hintGo.AddComponent<TextMeshPro>();
        FarmUIFont.Apply(_hint);
        _hint.fontSize = 0.22f;
        _hint.alignment = TextAlignmentOptions.Center;
        _hint.color = new Color(1f, 1f, 1f, 0.85f);
        _hint.text = "[扳机 / E] 交谈";
        _hint.rectTransform.sizeDelta = new Vector2(1.1f, 0.3f);
        hintGo.transform.SetParent(transform, false);
        hintGo.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        hintGo.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_player == null) return;
        float d = Vector3.Distance(new Vector3(_player.position.x, 0, _player.position.z),
                                   new Vector3(transform.position.x, 0, transform.position.z));
        var look = new Vector3(_player.position.x, transform.position.y, _player.position.z);
        if (d < 12f && look != transform.position)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), Time.deltaTime * 2f);

        bool grip = _grip.IsPressed();
        if (grip && !_lastGrip && d < talkRange) Talk();
        _lastGrip = grip;

        // 交谈提示：范围内且气泡未开时显示
        if (_hint != null)
        {
            bool showHint = d <= talkRange && (_bubble == null || !_bubble.gameObject.activeSelf);
            _hint.gameObject.SetActive(showHint);
        }

        if (_bubble != null && _bubble.gameObject.activeSelf && Camera.main != null)
        {
            _bubble.transform.LookAt(Camera.main.transform);
            _bubble.transform.Rotate(0f, 180f, 0f);
        }
        if (_hint != null && _hint.gameObject.activeSelf && Camera.main != null)
        {
            _hint.transform.LookAt(Camera.main.transform);
            _hint.transform.Rotate(0f, 180f, 0f);
        }
    }

    void Talk()
    {
        var lines = new System.Collections.Generic.List<string>(Tutorial);
        int lv = FarmProgress.Level;
        bool offerDelegate = lv >= 3;
        bool offerFullAuto = lv >= 5;
        if (offerFullAuto) lines.Add(FullAuto
            ? "行，院子交给我，你去歇着。\n再按一次扳机，就还给你。"
            : "干脆连种带收都交给我吧，\n工钱从产出里扣一成。再按一次扳机接活。");
        else if (offerDelegate) lines.Add(DelegatedWater
            ? "浇水的事我记着呢，每天一壶。\n再按一次扳机，就不麻烦我啦。"
            : "要是忙，把浇水托付给我。\n老把式不收冤枉钱。再按一次扳机接活。");
        if (lv >= 2 && !offerFullAuto) lines.Add("集上有化肥卖，十金一袋。\n使多了伤地，悠着点。");

        _dialogIndex %= lines.Count;
        string text = lines[_dialogIndex];
        bool isOffer = offerFullAuto ? _dialogIndex == lines.Count - 1 && lines.Count > Tutorial.Length
                                     : offerDelegate && _dialogIndex == lines.Count - 1 && lines.Count > Tutorial.Length;
        _dialogIndex++;
        if (_dialogIndex >= lines.Count) _dialogIndex = 0;

        if (isOffer)
        {
            if (offerFullAuto) FullAuto = !FullAuto;
            else DelegatedWater = !DelegatedWater;
        }

        if (_bubble != null)
        {
            _bubble.text = text;
            _bubble.gameObject.SetActive(true);
            CancelInvoke(nameof(HideBubble));
            Invoke(nameof(HideBubble), 5f);
        }
        FarmAudio.Play("ui", transform.position, 0.6f);
    }

    void HideBubble() { if (_bubble != null) _bubble.gameObject.SetActive(false); }

    /// <summary>清晨：委托浇水 → 巡逻浇地；全托管 → 种/浇/收一条龙。</summary>
    public void OnNewDay()
    {
        if (!DelegatedWater && !FullAuto) return;
        StartCoroutine(MorningPatrol());
    }

    IEnumerator MorningPatrol()
    {
        var plots = Object.FindObjectsOfType<FarmPlot>();
        foreach (var p in plots)
        {
            var target = p.transform.position + new Vector3(0.9f, 0f, 0.9f);
            float t = 0f;
            var from = transform.position;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.1f;
                transform.position = Vector3.Lerp(from, target, t);
                yield return null;
            }
            if (FullAuto && p.CanHarvest())
            {
                var res = p.Harvest();
                if (res != null)
                {
                    var def = CropDB.Get(p.cropId);
                    string name = def != null ? def.DisplayName : p.cropId;
                    FarmEconomy.AddHarvest(name, Mathf.Max(1, res[0] - 1), res[1]);
                    FarmAudio.Play("harvest", p.transform.position);
                }
            }
            if (FullAuto && p.CanPlant())
            {
                p.Plant(PlotInteractor.SelectedSeedId);
                FarmAudio.Play("plant", p.transform.position);
            }
            if ((DelegatedWater || FullAuto) && p.CanWater())
            {
                p.Water();
                FarmAudio.Play("water", p.transform.position, 0.7f);
            }
            yield return new WaitForSeconds(0.25f);
        }
        var home = new Vector3(0.8f, transform.position.y, -5.6f);
        float t2 = 0f;
        var from2 = transform.position;
        while (t2 < 1f)
        {
            t2 += Time.deltaTime / 1.4f;
            transform.position = Vector3.Lerp(from2, home, t2);
            yield return null;
        }
    }
}
