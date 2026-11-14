using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UObject = UnityEngine.Object;

namespace FarmTools
{
    /// <summary>
    /// M3 守望稻田：向当前打开的 Farm 场景"增量"添加虫情小游戏（幂等：重建 PestGameRoot）。
    /// 红线：绝不改动 SampleScene —— 当前打开场景不是 Farm.unity 时直接中止。
    /// </summary>
    public static class FarmPestBuilder
    {
        const string RootName = "PestGameRoot";
        const string ModelDir = "Assets/Farm/Models";

        [MenuItem("Farm/M3 添加守望稻田小游戏")]
        public static void BuildMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.Replace('\\', '/').EndsWith("Farm.unity"))
            {
                Debug.LogError("[守望稻田] 请先打开 Assets/Scenes/Farm.unity 再运行 M3（本工具绝不改动 SampleScene）。");
                return;
            }
            var env = GameObject.Find("FarmEnvironment");
            var rig = GameObject.Find("XR Origin (XR Rig)");
            if (env == null || rig == null)
            {
                Debug.LogError("[守望稻田] 场景里缺 FarmEnvironment 或 XR Origin (XR Rig)，中止。");
                return;
            }
            BuildInto(env, rig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[守望稻田] M3 BUILD OK —— 已保存 " + scene.path);
        }

        /// <summary>供 M1 一键搭建调用（Farm 场景生成流程内，环境已就绪）。</summary>
        public static void BuildInto(GameObject env, GameObject rig)
        {
            // 0. 幂等：清掉旧的重建
            var old = env.transform.Find(RootName);
            if (old != null) UObject.DestroyImmediate(old.gameObject);

            // 中文字体先行（所有 TMP 都靠 FarmUIFont.Apply）：场景统一楷体 simkai
            // 注意：simkai SDF 的动态图集字符集是按作业文案烤的（1024²已满），新增字会缺字；
            // 新 UI 文案请复用已有字符，或用 TMP Font Asset Creator 重新生成全字符集的 SDF。
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/simkai SDF.asset");
            if (sdf == null) sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/simkai SDF.asset");
            if (sdf == null) sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SimHei SDF.asset");
            FarmUIFont.SetFont(sdf);

            EnsureFarmSystems(rig, env);

            // 出生点放到田间十字走道上（2×3 重排后原点会埋进 3 号田的菜里）
            rig.transform.position = new Vector3(2.5f, rig.transform.position.y, -2.5f);
            EnsureControllerModels(rig);
            EnsureOfficialLocomotion(rig);
            TuneControllerRays(rig);

            var root = new GameObject(RootName);
            root.transform.SetParent(env.transform, false);

            var woodM = SolidMat("M_PestWood", new Color(0.48f, 0.33f, 0.19f));
            var gongM = SolidMat("M_PestGong", new Color(0.85f, 0.66f, 0.22f));
            gongM.SetFloat("_Metallic", 0.55f);
            gongM.SetFloat("_Glossiness", 0.7f);
            var redM = SolidMat("M_PestRed", new Color(0.82f, 0.22f, 0.16f));

            // 1. 田间搜查场：田块植株等比放大重排 + 装饰植株（幂等）
            BuildSearchField(env);

            // 2. 虫情锣（敲响开局）+ 拍子架（放在田间走道口）
            BuildGong(root, woodM, gongM);
            BuildSwatter(root, woodM, redM);

            // 3. 虫子模板（GLB 低模优先，场景内停用备用，PrefabPool 取用）
            var pestT = new List<GameObject>();
            var goodT = new List<GameObject>();
            BuildTemplates(root, pestT, goodT);
            // 4. 告示牌 + 管理器 + 头顶任务栏
            var ui = BuildBoard(root);
            var mgrGo = new GameObject("PestManager");
            mgrGo.transform.SetParent(root.transform, false);
            var mgr = mgrGo.AddComponent<FarmPestManager>();
            mgr.plots = FindPlots(env);
            mgr.pestTemplates = pestT.ToArray();
            mgr.goodTemplates = goodT.ToArray();
            mgr.ui = ui;
            BuildHeadHUD(rig, ui);
            SweepSceneFonts();
        }

        /// <summary>全场景字体统一：把所有 TMP（含已序列化的旧文本，如公告板）刷成 FarmUIFont 当前字体。</summary>
        static void SweepSceneFonts()
        {
            int n = 0;
            foreach (var t in UObject.FindObjectsOfType<TMP_Text>(true))
            {
                FarmUIFont.Apply(t);
                n++;
            }
            Debug.Log($"[守望稻田] 场景字体已统一为 {(FarmUIFont.CJK != null ? FarmUIFont.CJK.name : "(未找到字体资产)")}，共刷 {n} 个文本");
        }

        // ---------- 田间搜查场 ----------
        // 田块 ×1.8 后 4.32×2.52m，重排成 2 列 × 3 行，留 ~2m 田间走道
        static readonly Vector3[] FieldLayout =
        {
            new Vector3(-0.5f, 0.225f, -4.5f), new Vector3(5.5f, 0.225f, -4.5f),
            new Vector3(-0.5f, 0.225f, 0f),    new Vector3(5.5f, 0.225f, 0f),
            new Vector3(-0.5f, 0.225f, 4.5f),  new Vector3(5.5f, 0.225f, 4.5f),
        };
        const float FieldScale = 1.8f;

        static void BuildSearchField(GameObject env)
        {
            // 幂等：田块已放大过就只重建装饰植株
            var p1 = env.transform.Find("Plot_1");
            bool alreadyScaled = p1 != null && p1.localScale.x > 3.5f;

            if (!alreadyScaled)
            {
                for (int i = 1; i <= 6; i++)
                {
                    var plot = env.transform.Find($"Plot_{i}");
                    if (plot == null) continue;
                    Vector3 oldPos = plot.position;
                    Vector3 newPos = FieldLayout[i - 1];
                    plot.localScale = plot.localScale * FieldScale;
                    plot.position = newPos;
                    // 木框围边是场景根级物件，跟随搬移缩放
                    foreach (var side in new[] { "FrN", "FrS", "FrW", "FrE" })
                    {
                        var f = env.transform.Find($"Plot_{i}_{side}");
                        if (f == null) continue;
                        f.position = newPos + (f.position - oldPos) * FieldScale;
                        f.localScale = f.localScale * FieldScale;
                    }
                }
                // 老版本 M3 的土堆全部退役
                for (int i = 1; i <= 8; i++)
                {
                    var m = env.transform.Find($"PestMound_{i}");
                    if (m != null) UObject.DestroyImmediate(m.gameObject);
                }
            }

            // 挪开与新布局冲突的物件（绝对坐标，每次重建都执行，天然幂等）
            MoveByName(env, "Well", new Vector3(-4.5f, 0f, -3.0f));
            MoveByName(env, "NPC_HeBo", new Vector3(2.6f, 0f, -6.9f));
            MoveByName(env, "Boat", new Vector3(10.3f, 0f, -2.0f));
            MoveByName(env, "Dock", new Vector3(10.6f, 0f, 4.2f));
            MoveByName(env, "Bridge", new Vector3(4.3f, 0f, 7.9f));
            MoveByName(env, "Pergola", new Vector3(10.2f, 0f, -6.5f));
            // 水车+水塘整体挪到温室北侧空地，别压菜田（pivot 在远处空地，无地面约束）
            MoveByName(env, "CN_WaterWheel", new Vector3(11.7f, 0f, 22.4f));
            var demo2 = env.transform.Find("Crops_Demo");
            if (demo2 != null) demo2.position = new Vector3(2.5f, 0.25f, 2.25f);

            // 装饰植株：每块田 4×3 株随机作物，虫子藏在它们之间。
            // 不能挂到田块下（田块 scale 非均匀会拉扯模型），挂独立根。
            var decoRootName = "PestFieldPlants";
            var oldDeco = env.transform.Find(decoRootName);
            if (oldDeco != null) UObject.DestroyImmediate(oldDeco.gameObject);
            var deco = new GameObject(decoRootName);
            deco.transform.SetParent(env.transform, false);
            string[] crops = { "Cabbage", "Carrot", "Pepper", "Corn" };
            for (int i = 1; i <= 6; i++)
            {
                var plot = env.transform.Find($"Plot_{i}");
                if (plot == null) continue;
                Vector3 c = plot.position;
                float topY = c.y + 0.225f;   // 田面高度
                for (int gx = 0; gx < 5; gx++)
                {
                    for (int gz = 0; gz < 4; gz++)
                    {
                        float dx = -1.7f + gx * 0.85f;
                        float dz = -0.94f + gz * 0.63f;
                        string crop = crops[Random.Range(0, crops.Length)];
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/crops/{crop}.glb");
                        if (prefab == null) continue;
                        var go = (GameObject)UObject.Instantiate(prefab);
                        go.name = $"{plot.name}_Deco_{gx}_{gz}";
                        float targetH = crop == "Corn" ? Random.Range(1.4f, 1.8f) : Random.Range(0.55f, 0.9f);
                        var rends = go.GetComponentsInChildren<Renderer>();
                        if (rends.Length == 0) { UObject.DestroyImmediate(go); continue; }
                        var b = rends[0].bounds;
                        for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
                        float s = targetH / Mathf.Max(b.size.y, 0.01f);
                        go.transform.localScale = go.transform.localScale * s;
                        b = rends[0].bounds;
                        for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
                        go.transform.position += new Vector3(c.x + dx - b.center.x, topY - b.min.y, c.z + dz - b.center.z);
                        go.transform.SetParent(deco.transform, true);
                        var cols = go.GetComponentsInChildren<Collider>();
                        foreach (var cc in cols) UObject.DestroyImmediate(cc);
                    }
                }
            }
        }

        static void MoveByName(GameObject env, string name, Vector3 pos)
        {
            var t = env.transform.Find(name);
            if (t != null) t.position = pos;
        }

        static FarmPlot[] FindPlots(GameObject env)
        {
            var list = new List<FarmPlot>();
            for (int i = 1; i <= 6; i++)
            {
                var t = env.transform.Find($"Plot_{i}");
                if (t == null) continue;
                var p = t.GetComponent<FarmPlot>();
                if (p != null) list.Add(p);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 只保留抓虫一个任务：种田交互（PlotInteractor）与 NPC 功能（FarmNPC）退役，
        /// 出货区自动卖出停用（金币只进不出）；NPC 模型留作背景板。HandGrabber/EventSystem 幂等补齐。
        /// </summary>
        static void EnsureFarmSystems(GameObject rig, GameObject env)
        {
            var pi = rig.GetComponent<PlotInteractor>();
            if (pi != null)
            {
                UObject.DestroyImmediate(pi);
                Debug.Log("[守望稻田] 已移除种田交互 PlotInteractor（只保留抓虫任务）");
            }
            var npcT = env.transform.Find("NPC_HeBo");
            if (npcT != null)
            {
                var npc = npcT.GetComponent<FarmNPC>();
                if (npc != null)
                {
                    UObject.DestroyImmediate(npc);
                    Debug.Log("[守望稻田] 禾伯已退居背景板（对话/巡逻功能移除，模型保留）");
                }
            }
            var sell = env.transform.Find("SellZone");
            if (sell != null)
            {
                var sc = sell.GetComponent<Collider>();
                if (sc != null && sc.enabled)
                {
                    sc.enabled = false;
                    Debug.Log("[守望稻田] 出货区自动卖出已停用");
                }
            }

            var right = rig.transform.Find("Camera Offset/Right Controller");
            var left = rig.transform.Find("Camera Offset/Left Controller");
            if (right != null && right.GetComponent<HandGrabber>() == null)
                right.gameObject.AddComponent<HandGrabber>();
            if (left != null && left.GetComponent<HandGrabber>() == null)
                left.gameObject.AddComponent<HandGrabber>();

            if (UObject.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
                Debug.Log("[守望稻田] 已补建 EventSystem（XRI 射线点 UI 所需）");
            }
        }

        /// <summary>头顶任务栏：跟随视角的游戏说明 + 开始按钮（搜查中由 FarmPestUI 收起）。</summary>
        static void BuildHeadHUD(GameObject rig, FarmPestUI ui)
        {
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam == null) { Debug.LogWarning("[守望稻田] 无相机，头顶任务栏跳过"); return; }

            var hudT = cam.transform.Find("TopHUD");
            GameObject hudGo;
            if (hudT == null)
            {
                hudGo = new GameObject("TopHUD");
                var canvas = hudGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                hudGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                hudGo.transform.SetParent(cam.transform, false);
                hudGo.transform.localPosition = new Vector3(0f, 0.40f, 1.5f);
                hudGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                hudGo.transform.localScale = Vector3.one * 0.0016f;
                var rt0 = hudGo.GetComponent<RectTransform>();
                rt0.sizeDelta = new Vector2(780f, 190f);
            }
            else
            {
                hudGo = hudT.gameObject;
                var canvas = hudGo.GetComponent<Canvas>();
                if (canvas != null)
                {
                    var rt0 = hudGo.GetComponent<RectTransform>();
                    rt0.sizeDelta = new Vector2(780f, 190f);
                    hudGo.transform.localScale = Vector3.one * 0.0016f;
                }
            }

            // 退役旧任务系统（FarmHUD 的种田任务扫描），内容重建
            var oldHud = hudGo.GetComponent<FarmHUD>();
            if (oldHud != null) UObject.DestroyImmediate(oldHud);
            while (hudGo.transform.childCount > 0)
                UObject.DestroyImmediate(hudGo.transform.GetChild(0).gameObject);

            // 说明面板（可整块收起）
            var headPanel = Panel(hudGo.transform, "HeadPanel", new Vector2(780f, 190f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
            var bg = new GameObject("BG", typeof(Image));
            bg.transform.SetParent(headPanel.transform, false);
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(780f, 190f);
            bg.GetComponent<Image>().color = new Color(0.06f, 0.10f, 0.07f, 0.66f);

            Tmp(headPanel.transform, "Title", "守望稻田 · 虫情大搜查", 34f, TextAlignmentOptions.Left,
                new Vector2(-65f, 60f), new Vector2(640f, 46f), new Color(1f, 0.9f, 0.5f));
            Tmp(headPanel.transform, "Guide",
                "走进田里，找出藏在菜叶间的害虫拍掉它\n青蛙 / 瓢虫 / 蜜蜂是除虫帮手，别误伤！\n限时 90 秒 · 敲虫情锣或点「开始搜查」",
                21f, TextAlignmentOptions.Left, new Vector2(-125f, -25f), new Vector2(500f, 110f));
            ui.hudStartButton = Btn(headPanel.transform, "StartButton", "开始搜查", new Vector2(298f, -18f), new Vector2(180f, 70f));

            // 收起/展开小按钮：常驻在面板下方，面板藏起来也能点回来。
            // 文案用单字"收/开"——simkai 图集是按作业字符集烤的，这两个字保证有字形
            ui.hudToggleButton = Btn(hudGo.transform, "HudToggle", "收", new Vector2(-295f, -128f), new Vector2(170f, 48f));
            ui.hudToggleLabel = ui.hudToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            ui.headPanel = headPanel;
            Debug.Log("[守望稻田] 头顶任务栏已重建：游戏说明 + 开始/收起按钮");
        }

        /// <summary>
        /// 手柄可视化：挂 XRI Starter Assets 官方控制器预制——SampleScene 实际游玩的那套装备
        /// 用的正是它（自带 UniversalController 模型 + 按键 + ControllerAnimator 按压动画
        /// + NearFar/Poke 交互器），local identity 跟随控制器锚点。
        /// </summary>
        static void EnsureControllerModels(GameObject rig)
        {
            AttachStarterController(rig.transform.Find("Camera Offset/Left Controller"),
                "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/Controllers/XR Controller Left.prefab");
            AttachStarterController(rig.transform.Find("Camera Offset/Right Controller"),
                "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/Controllers/XR Controller Right.prefab");
        }

        /// <summary>
        /// 按 XRI 3.x 官方文档重接移动（此前是手写 FarmLocomotion，且场景缺 XRInteractionManager）：
        /// XRInteractionManager + XRBodyTransformer + LocomotionMediator + ContinuousMoveProvider（左摇杆）
        /// + SnapTurnProvider（右摇杆）+ GravityProvider（官方重力）；移除手写 FarmLocomotion。
        /// 手柄模型已按官方装备模板挂为控制器子物体。
        /// </summary>
        static void EnsureOfficialLocomotion(GameObject rig)
        {
            if (UObject.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>() == null)
            {
                var mgrGo = new GameObject("XR Interaction Manager");
                mgrGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
                Debug.Log("[守望稻田] 已补建 XRInteractionManager（XRI 射线/交互的调度中枢，此前场景缺失）");
            }

            if (rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>() == null)
                rig.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
            if (rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>() == null)
                rig.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>();

            var cm = rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
            if (cm == null) cm = rig.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
            cm.moveSpeed = 1.8f;
            cm.enableStrafe = true;
            cm.leftHandMoveInput.inputSourceMode = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputValueReader<UnityEngine.Vector2>.InputSourceMode.InputAction;
            cm.leftHandMoveInput.inputAction = new UnityEngine.InputSystem.InputAction("FarmMove", UnityEngine.InputSystem.InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            cm.rightHandMoveInput.inputSourceMode = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputValueReader<UnityEngine.Vector2>.InputSourceMode.Unused;

            var st = rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>();
            if (st == null) st = rig.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>();
            st.turnAmount = 45f;
            st.rightHandTurnInput.inputSourceMode = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputValueReader<UnityEngine.Vector2>.InputSourceMode.InputAction;
            st.rightHandTurnInput.inputAction = new UnityEngine.InputSystem.InputAction("FarmTurn", UnityEngine.InputSystem.InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
            st.leftHandTurnInput.inputSourceMode = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputValueReader<UnityEngine.Vector2>.InputSourceMode.Unused;

            if (rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity.GravityProvider>() == null)
                rig.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity.GravityProvider>();

            EnsureInputActionManager();

            var fl = rig.GetComponent<FarmLocomotion>();
            if (fl != null)
            {
                UObject.DestroyImmediate(fl);
                Debug.Log("[守望稻田] 手写 FarmLocomotion 已移除，改用官方 ContinuousMoveProvider（左摇杆移动 / 右摇杆 45° 转身）");
            }

            // FarmPC 会杀掉官方 XRI 设备模拟器（编辑器运行时自动实例化，SampleScene 里那行
            // "Input Selection" 就是它）——它才是官方的编辑器测试路径，移除 FarmPC 让模拟器醒来。
            var pc = rig.GetComponent<FarmPC>();
            if (pc != null)
            {
                UObject.DestroyImmediate(pc);
                Debug.Log("[守望稻田] FarmPC 已移除：编辑器用官方 XRI 设备模拟器，实机用 PXR 输入");
            }
        }

        /// <summary>
        /// 射线调优：XRI 射线没指中任何可交互物时，30m 红色无效线会横穿整片田野（SampleScene 里
        /// 你永远指着游戏桌所以没见过）。收到 6m（告示牌/头顶栏够用），无效态渐隐到透明。
        /// </summary>
        static void TuneControllerRays(GameObject rig)
        {
            foreach (var ray in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true))
                ray.maxRaycastDistance = 6f;
            foreach (var v in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(true))
            {
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
                v.invalidColorGradient = g;
                g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.25f, 1f) });
                v.validColorGradient = g;
            }
            Debug.Log("[守望稻田] 控制器射线已调优：射程 6m，无效态渐隐（不再 30m 红线穿场）");
        }

        /// <summary>
        /// InputActionManager 是 XRI 输入的总开关（启用手柄位姿/按键/摇杆全部动作映射）。
        /// 装备上复制来的这个组件 m_ActionAssets 是空的——动作从未启用，控制器收不到位姿，
        /// 手柄模型自然"挂不上"。这里绑定官方 Starter Assets 的动作资产。
        /// </summary>
        static void EnsureInputActionManager()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/XRI Default Input Actions.inputactions");
            if (asset == null) { Debug.LogWarning("[守望稻田] 缺少 XRI Default Input Actions 资产"); return; }
            var mgr = UObject.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            if (mgr == null)
            {
                var go = new GameObject("Input Action Manager");
                mgr = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            }
            var so = new SerializedObject(mgr);
            var list = so.FindProperty("m_ActionAssets");
            if (list == null) return;
            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) has = true;
            if (!has)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mgr);
                Debug.Log("[守望稻田] InputActionManager 已绑定 XRI Default Input Actions（手柄位姿/交互动作启用）");
            }
        }

        static void AttachStarterController(Transform ctrl, string path)
        {
            if (ctrl == null) return;
            // 幂等：已挂过就跳过；老版本挂的 PICO 壳（ControllerModel）先移除
            var old = ctrl.Find("ControllerModel");
            if (old != null) UObject.DestroyImmediate(old.gameObject);
            if (ctrl.Find("StarterController") != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[守望稻田] 缺少 Starter Assets 控制器预制 " + path); return; }
            var go = (GameObject)UObject.Instantiate(prefab, ctrl, false);
            go.name = "StarterController";
            Debug.Log("[守望稻田] 已挂官方控制器预制（SampleScene 同款） " + path);
        }

        // ---------- 虫情锣 ----------
        static void BuildGong(GameObject root, Material woodM, Material gongM)
        {
            var g = new GameObject("PestGong");
            g.transform.SetParent(root.transform, false);
            g.transform.position = new Vector3(0f, 0f, 3.0f);

            Prim(g.transform, "Post_L", PrimitiveType.Cylinder, new Vector3(-0.5f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f), woodM);
            Prim(g.transform, "Post_R", PrimitiveType.Cylinder, new Vector3(0.5f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f), woodM);
            Prim(g.transform, "Beam", PrimitiveType.Cube, new Vector3(0f, 1.22f, 0f), new Vector3(1.2f, 0.08f, 0.08f), woodM);
            Prim(g.transform, "Disc", PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.03f, 0.42f), gongM);
            Prim(g.transform, "Boss", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0.21f), new Vector3(0.05f, 0.05f, 0.02f), gongM);

            // 敲击检测区（静态 trigger）
            var col = g.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.95f, 0f);
            col.size = new Vector3(0.7f, 0.5f, 0.35f);
            g.AddComponent<FarmPestGong>();

            var label = new GameObject("GongLabel");
            var tmp = label.AddComponent<TextMeshPro>();
            FarmUIFont.Apply(tmp);
            tmp.text = "虫情锣 · 敲响开始";
            tmp.fontSize = 0.26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.97f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.4f);
            label.transform.SetParent(g.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        }

        // ---------- 拍子 + 架子 ----------
        static void BuildSwatter(GameObject root, Material woodM, Material redM)
        {
            var rack = new GameObject("SwatterRack");
            rack.transform.SetParent(root.transform, false);
            rack.transform.position = new Vector3(-3.0f, 0f, 2.2f);

            Prim(rack.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.55f, 0.05f), woodM);
            Prim(rack.transform, "Fork", PrimitiveType.Cube, new Vector3(0f, 1.06f, 0.1f), new Vector3(0.3f, 0.05f, 0.06f), woodM);

            var label = new GameObject("RackLabel");
            var tmp = label.AddComponent<TextMeshPro>();
            FarmUIFont.Apply(tmp);
            tmp.text = "虫情拍";
            tmp.fontSize = 0.22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.97f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
            label.transform.SetParent(rack.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            // 拍子本体：柄在下、拍面在上；可抓取
            var s = new GameObject("Swatter");
            s.transform.SetParent(rack.transform, false);
            s.transform.localPosition = new Vector3(0.12f, 0.95f, 0.16f);
            s.transform.localRotation = Quaternion.Euler(0f, 20f, 16f);

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            handle.transform.SetParent(s.transform, false);
            handle.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            handle.transform.localScale = new Vector3(0.025f, 0.19f, 0.025f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = woodM;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(s.transform, false);
            head.transform.localPosition = Vector3.zero;
            head.transform.localScale = new Vector3(0.11f, 0.012f, 0.11f);
            head.GetComponent<MeshRenderer>().sharedMaterial = redM;
            var hc = head.GetComponent<CapsuleCollider>();
            hc.isTrigger = true;   // 拍面命中区

            Prim(s.transform, "Net", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), new Vector3(0.07f, 0.004f, 0.07f), woodM);

            var rb = s.AddComponent<Rigidbody>();
            rb.useGravity = true;
            var gr = s.AddComponent<Grabbable>();
            gr.heldLocalPosition = new Vector3(0f, 0.02f, 0.15f);
            gr.heldLocalEuler = new Vector3(-55f, 0f, 0f);
            gr.startKinematic = true;   // 初始斜靠架上，首捡后恢复物理
            gr.throwOnDrop = true;
            head.AddComponent<FarmPestSwatter>();
        }

        // ---------- 虫子模板 ----------
        const string InsectDir = "Assets/Farm/Models/Insects";

        static void BuildTemplates(GameObject root, List<GameObject> pestT, List<GameObject> goodT)
        {
            // 兜底材质（GLB 缺失时才用程序化球体）
            var greenM = SolidMat("M_BugGreen", new Color(0.35f, 0.70f, 0.20f));
            var darkM = SolidMat("M_BugDark", new Color(0.12f, 0.10f, 0.08f));
            var grayM = SolidMat("M_BugGray", new Color(0.55f, 0.53f, 0.50f));
            var pinkM = SolidMat("M_BugPink", new Color(0.75f, 0.55f, 0.55f));
            var frogM = SolidMat("M_BugFrog", new Color(0.25f, 0.75f, 0.30f));
            var whiteM = SolidMat("M_BugWhite", new Color(0.95f, 0.95f, 0.95f));
            var redM = SolidMat("M_BugRed", new Color(0.85f, 0.12f, 0.10f));
            var yellowM = SolidMat("M_BugYellow", new Color(0.95f, 0.78f, 0.15f));

            var tpl = new GameObject("Templates");
            tpl.transform.SetParent(root.transform, false);

            // —— 害虫（GLB 优先，见 Insects/credits.txt 的来源与授权）——
            pestT.Add(BuildGlbTemplate(tpl, "Locust", 0.20f, "蝗虫", false, 1,
                "一只蝗虫一天能吃掉和自身体重差不多的庄稼，大规模聚集就成蝗灾。发现即拍！",
                () => BuildLocust(tpl.transform, greenM, darkM)));
            pestT.Add(BuildGlbTemplate(tpl, "Vole", 0.26f, "田鼠", false, 2,
                "田鼠偷吃稻谷、打洞毁田埂；但蛇和猫头鹰是它的天敌——生态平衡才是最好的防治。",
                () => BuildVole(tpl.transform, grayM, pinkM, darkM)));
            pestT.Add(BuildGlbTemplate(tpl, "Caterpillar", 0.16f, "毛毛虫", false, 1,
                "毛毛虫多是蛾蝶的幼虫，专啃菜叶；长大后会变成传粉的蝶蛾。防治要赶在幼虫期。",
                () => BuildCaterpillar(tpl.transform, greenM, darkM)));

            // —— 益虫 ——
            goodT.Add(BuildGlbTemplate(tpl, "Frog", 0.18f, "青蛙", true, 0,
                "一只青蛙一个夏天能吃掉上万只害虫，是田间免费的“除虫卫士”，千万不能伤害！",
                () => BuildFrog(tpl.transform, frogM, whiteM, darkM)));
            goodT.Add(BuildGlbTemplate(tpl, "Ladybug", 0.11f, "瓢虫", true, 0,
                "一只七星瓢虫一生能吃掉几千只蚜虫，是农民伯伯的好帮手，看见请手下留情。",
                () => BuildLadybug(tpl.transform, redM, darkM)));
            goodT.Add(BuildGlbTemplate(tpl, "Bee", 0.13f, "蜜蜂", true, 0,
                "全球约三分之一的农作物靠蜜蜂传粉。没有蜜蜂，我们的餐桌会空一大半。",
                () => BuildBee(tpl.transform, yellowM, darkM, whiteM)));

            foreach (var t in pestT) t.SetActive(false);
            foreach (var t in goodT) t.SetActive(false);
        }

        /// <summary>
        /// GLB 模板：按目标高度归一化（超长的横向再限幅），底部落到模板原点，挂 FarmPestVariant。
        /// 缺资产或无渲染体时退回程序化低模（美术兜底）。
        /// </summary>
        static GameObject BuildGlbTemplate(GameObject parent, string file, float targetH,
            string display, bool beneficial, int score, string fact, System.Func<GameObject> fallback)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InsectDir}/{file}.glb");
            if (prefab == null) return fallback();
            var go = (GameObject)UObject.Instantiate(prefab);
            go.name = file;
            go.transform.SetParent(parent.transform, true);
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { UObject.DestroyImmediate(go); return fallback(); }

            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float s = targetH / Mathf.Max(b.size.y, 0.01f);
            float maxSide = Mathf.Max(b.size.x, b.size.z) * s;
            if (maxSide > 0.5f) s *= 0.5f / maxSide;   // 超长模型横向限幅，避免一条虫横跨菜畦
            go.transform.localScale = go.transform.localScale * s;
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(parent.transform.position.x - b.center.x,
                parent.transform.position.y - b.min.y, parent.transform.position.z - b.center.z);

            var v = go.AddComponent<FarmPestVariant>();
            v.displayName = display;
            v.isBeneficial = beneficial;
            v.score = score;
            v.factText = fact;
            go.AddComponent<FarmPest>();   // 虫本体组件随模板进池
            return go;
        }

        static FarmPestVariant NewTemplate(Transform parent, string name, string display, bool beneficial, int score, string fact)
        {
            var t = new GameObject(name);
            t.transform.SetParent(parent, false);
            var v = t.AddComponent<FarmPestVariant>();
            v.displayName = display;
            v.isBeneficial = beneficial;
            v.score = score;
            v.factText = fact;
            t.gameObject.AddComponent<FarmPest>();   // 虫本体组件随模板进池
            return v;
        }

        static GameObject BuildLocust(Transform parent, Material bodyM, Material darkM)
        {
            var v = NewTemplate(parent, "Locust", "蝗虫", false, 1,
                "一只蝗虫一天能吃掉和自身体重差不多的庄稼，大规模聚集就成蝗灾。发现即拍！");
            var t = v.transform;
            Prim(t, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.10f, -0.02f), new Vector3(0.06f, 0.09f, 0.06f), bodyM, 65f, 0f, 0f);
            Prim(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.07f), new Vector3(0.07f, 0.07f, 0.07f), bodyM);
            Prim(t, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.026f, 0.18f, 0.105f), new Vector3(0.018f, 0.018f, 0.018f), darkM);
            Prim(t, "Eye_R", PrimitiveType.Sphere, new Vector3(0.026f, 0.18f, 0.105f), new Vector3(0.018f, 0.018f, 0.018f), darkM);
            Prim(t, "Leg_L", PrimitiveType.Capsule, new Vector3(-0.05f, 0.08f, -0.03f), new Vector3(0.018f, 0.05f, 0.018f), darkM, 20f, 0f, 30f);
            Prim(t, "Leg_R", PrimitiveType.Capsule, new Vector3(0.05f, 0.08f, -0.03f), new Vector3(0.018f, 0.05f, 0.018f), darkM, 20f, 0f, -30f);
            Prim(t, "Ant_L", PrimitiveType.Capsule, new Vector3(-0.02f, 0.22f, 0.09f), new Vector3(0.008f, 0.03f, 0.008f), darkM, 30f, 0f, 15f);
            Prim(t, "Ant_R", PrimitiveType.Capsule, new Vector3(0.02f, 0.22f, 0.09f), new Vector3(0.008f, 0.03f, 0.008f), darkM, 30f, 0f, -15f);
            return v.gameObject;
        }

        static GameObject BuildVole(Transform parent, Material grayM, Material pinkM, Material darkM)
        {
            var v = NewTemplate(parent, "Vole", "田鼠", false, 2,
                "田鼠偷吃稻谷、打洞毁田埂；但蛇和猫头鹰是它的天敌——生态平衡才是最好的防治。");
            var t = v.transform;
            Prim(t, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.10f, -0.03f), new Vector3(0.11f, 0.09f, 0.13f), grayM);
            Prim(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.09f), new Vector3(0.065f, 0.06f, 0.065f), grayM);
            Prim(t, "Ear_L", PrimitiveType.Sphere, new Vector3(-0.035f, 0.18f, 0.07f), new Vector3(0.025f, 0.025f, 0.015f), pinkM);
            Prim(t, "Ear_R", PrimitiveType.Sphere, new Vector3(0.035f, 0.18f, 0.07f), new Vector3(0.025f, 0.025f, 0.015f), pinkM);
            Prim(t, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.025f, 0.13f, 0.14f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Eye_R", PrimitiveType.Sphere, new Vector3(0.025f, 0.13f, 0.14f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0.155f), new Vector3(0.012f, 0.012f, 0.012f), darkM);
            Prim(t, "Tail", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, -0.17f), new Vector3(0.012f, 0.13f, 0.012f), pinkM, 90f, 0f, 0f);
            return v.gameObject;
        }

        static GameObject BuildCaterpillar(Transform parent, Material bodyM, Material darkM)
        {
            var v = NewTemplate(parent, "Caterpillar", "毛毛虫", false, 1,
                "毛毛虫多是蛾蝶的幼虫，专啃菜叶；长大后会变成传粉的蝶蛾。防治要赶在幼虫期。");
            var t = v.transform;
            Prim(t, "Seg_1", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.09f), new Vector3(0.055f, 0.055f, 0.055f), bodyM);
            Prim(t, "Seg_2", PrimitiveType.Sphere, new Vector3(0f, 0.07f, 0f), new Vector3(0.06f, 0.06f, 0.06f), bodyM);
            Prim(t, "Seg_3", PrimitiveType.Sphere, new Vector3(0f, 0.06f, -0.09f), new Vector3(0.055f, 0.055f, 0.055f), bodyM);
            Prim(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.17f), new Vector3(0.06f, 0.06f, 0.06f), bodyM);
            Prim(t, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.024f, 0.10f, 0.215f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Eye_R", PrimitiveType.Sphere, new Vector3(0.024f, 0.10f, 0.215f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            return v.gameObject;
        }

        static GameObject BuildFrog(Transform parent, Material frogM, Material whiteM, Material darkM)
        {
            var v = NewTemplate(parent, "Frog", "青蛙", true, 0,
                "一只青蛙一个夏天能吃掉上万只害虫，是田间免费的“除虫卫士”，千万不能伤害！");
            var t = v.transform;
            Prim(t, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.09f, 0.11f), frogM);
            Prim(t, "Leg_L", PrimitiveType.Sphere, new Vector3(-0.10f, 0.04f, -0.05f), new Vector3(0.045f, 0.03f, 0.08f), frogM);
            Prim(t, "Leg_R", PrimitiveType.Sphere, new Vector3(0.10f, 0.04f, -0.05f), new Vector3(0.045f, 0.03f, 0.08f), frogM);
            Prim(t, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.05f, 0.155f, 0.04f), new Vector3(0.038f, 0.038f, 0.038f), whiteM);
            Prim(t, "Eye_R", PrimitiveType.Sphere, new Vector3(0.05f, 0.155f, 0.04f), new Vector3(0.038f, 0.038f, 0.038f), whiteM);
            Prim(t, "Pupil_L", PrimitiveType.Sphere, new Vector3(-0.05f, 0.16f, 0.07f), new Vector3(0.016f, 0.016f, 0.016f), darkM);
            Prim(t, "Pupil_R", PrimitiveType.Sphere, new Vector3(0.05f, 0.16f, 0.07f), new Vector3(0.016f, 0.016f, 0.016f), darkM);
            return v.gameObject;
        }

        static GameObject BuildLadybug(Transform parent, Material redM, Material darkM)
        {
            var v = NewTemplate(parent, "Ladybug", "瓢虫", true, 0,
                "一只七星瓢虫一生能吃掉几千只蚜虫，是农民伯伯的好帮手，看见请手下留情。");
            var t = v.transform;
            Prim(t, "Shell", PrimitiveType.Sphere, new Vector3(0f, 0.075f, 0f), new Vector3(0.11f, 0.075f, 0.11f), redM);
            Prim(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.095f), new Vector3(0.05f, 0.05f, 0.05f), darkM);
            Prim(t, "Dot_1", PrimitiveType.Sphere, new Vector3(-0.04f, 0.10f, 0.03f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Dot_2", PrimitiveType.Sphere, new Vector3(0.04f, 0.10f, 0.03f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Dot_3", PrimitiveType.Sphere, new Vector3(-0.03f, 0.085f, -0.045f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            Prim(t, "Dot_4", PrimitiveType.Sphere, new Vector3(0.03f, 0.085f, -0.045f), new Vector3(0.014f, 0.014f, 0.014f), darkM);
            return v.gameObject;
        }

        static GameObject BuildBee(Transform parent, Material yellowM, Material darkM, Material whiteM)
        {
            var v = NewTemplate(parent, "Bee", "蜜蜂", true, 0,
                "全球约三分之一的农作物靠蜜蜂传粉。没有蜜蜂，我们的餐桌会空一大半。");
            var t = v.transform;
            Prim(t, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0f), new Vector3(0.08f, 0.07f, 0.10f), yellowM);
            Prim(t, "Stripe_1", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.03f), new Vector3(0.084f, 0.028f, 0.104f), darkM);
            Prim(t, "Stripe_2", PrimitiveType.Sphere, new Vector3(0f, 0.09f, -0.03f), new Vector3(0.084f, 0.028f, 0.104f), darkM);
            Prim(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.09f), new Vector3(0.05f, 0.05f, 0.05f), darkM);
            Prim(t, "Wing_L", PrimitiveType.Cube, new Vector3(-0.045f, 0.15f, 0f), new Vector3(0.07f, 0.004f, 0.05f), whiteM, -15f, 0f, 18f);
            Prim(t, "Wing_R", PrimitiveType.Cube, new Vector3(0.045f, 0.15f, 0f), new Vector3(0.07f, 0.004f, 0.05f), whiteM, -15f, 0f, -18f);
            return v.gameObject;
        }

        // ---------- 告示牌 UI ----------
        static FarmPestUI BuildBoard(GameObject root)
        {
            var boardGo = new GameObject("PestBoard");
            var canvas = boardGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            boardGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            boardGo.transform.SetParent(root.transform, false);
            boardGo.transform.position = new Vector3(2.5f, 1.55f, 2.8f);
            boardGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 面向田间玩家
            boardGo.transform.localScale = Vector3.one * 0.0016f;
            var rt = boardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720f, 460f);

            var bg = new GameObject("BG", typeof(Image));
            bg.transform.SetParent(boardGo.transform, false);
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 460f);
            bg.GetComponent<Image>().color = new Color(0.07f, 0.11f, 0.08f, 0.72f);

            var ui = boardGo.AddComponent<FarmPestUI>();

            // —— 开始面板 ——
            var start = Panel(boardGo.transform, "StartPanel", new Vector2(700f, 440f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
            Tmp(start.transform, "Title", "守望稻田 · 虫情警报", 40f, TextAlignmentOptions.Center, new Vector2(0f, 175f), new Vector2(660f, 60f),
                new Color(1f, 0.9f, 0.5f));
            Tmp(start.transform, "Body",
                "虫情大搜查！\n" +
                "· 害虫（蝗虫 / 田鼠 / 毛毛虫）藏在菜叶间：走进田里仔细找，拍掉它！\n" +
                "· 青蛙 / 瓢虫 / 蜜蜂是除虫帮手：看清了再拍，别误伤\n" +
                "· 规定时间内拍掉全部害虫，越快金币越多\n" +
                "· 敲虫情锣 / 点下方按钮开始（PC 可按 P）",
                25f, TextAlignmentOptions.Left, new Vector2(-20f, 10f), new Vector2(650f, 230f));
            ui.startButton = Btn(start.transform, "StartButton", "开始搜查虫情", new Vector2(0f, -155f), new Vector2(300f, 62f));

            // —— HUD 面板 ——
            var hud = Panel(boardGo.transform, "HudPanel", new Vector2(700f, 440f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
            ui.waveText = Tmp(hud.transform, "WaveText", "时间 90s", 30f, TextAlignmentOptions.Left, new Vector2(-300f, 185f), new Vector2(240f, 44f));
            ui.pointsText = Tmp(hud.transform, "PointsText", "守护分 0", 30f, TextAlignmentOptions.Right, new Vector2(300f, 185f), new Vector2(240f, 44f),
                new Color(1f, 0.9f, 0.5f));
            ui.countdownText = Tmp(hud.transform, "CountdownText", "", 72f, TextAlignmentOptions.Center, new Vector2(0f, 40f), new Vector2(500f, 130f),
                new Color(1f, 0.85f, 0.3f));
            ui.remainText = Tmp(hud.transform, "RemainText", "本波剩余", 26f, TextAlignmentOptions.Left, new Vector2(-300f, 150f), new Vector2(240f, 36f));
            ui.hintText = Tmp(hud.transform, "HintText", "", 21f, TextAlignmentOptions.Center, new Vector2(0f, -190f), new Vector2(660f, 40f));

            // —— 结算面板 ——
            var report = Panel(boardGo.transform, "ReportPanel", new Vector2(700f, 440f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
            Tmp(report.transform, "Title", "守护报告", 38f, TextAlignmentOptions.Center, new Vector2(0f, 180f), new Vector2(400f, 56f),
                new Color(1f, 0.9f, 0.5f));
            ui.reportText = Tmp(report.transform, "Body", "", 24f, TextAlignmentOptions.Left, new Vector2(0f, 25f), new Vector2(650f, 250f));
            ui.againButton = Btn(report.transform, "AgainButton", "再来一轮", new Vector2(-130f, -155f), new Vector2(220f, 56f));
            ui.exportButton = Btn(report.transform, "ExportButton", "导出记录 CSV", new Vector2(130f, -155f), new Vector2(220f, 56f));
            ui.reportCountdownText = Tmp(report.transform, "ReportCountdown", "", 20f, TextAlignmentOptions.Center,
                new Vector2(0f, -198f), new Vector2(660f, 36f), new Color(1f, 0.9f, 0.5f));

            // —— 知识卡弹窗 ——
            var card = Panel(boardGo.transform, "CardPanel", new Vector2(620f, 150f), new Vector2(0f, 120f), new Color(0.10f, 0.22f, 0.14f, 0.88f));
            ui.cardTitle = Tmp(card.transform, "CardTitle", "小知识", 28f, TextAlignmentOptions.Left, new Vector2(-250f, 42f), new Vector2(560f, 40f),
                new Color(1f, 0.9f, 0.5f));
            ui.cardBody = Tmp(card.transform, "CardBody", "", 23f, TextAlignmentOptions.Left, new Vector2(0f, -18f), new Vector2(580f, 90f));

            // 初始显隐（运行时 Manager.Start 会再调 ShowStart）
            start.SetActive(true);
            hud.SetActive(false);
            report.SetActive(false);
            card.SetActive(false);
            return ui;
        }

        // ---------- 小工具 ----------
        static GameObject Panel(Transform canvas, string name, Vector2 size, Vector2 pos, Color bg)
        {
            var p = new GameObject(name, typeof(Image));
            p.transform.SetParent(canvas, false);
            var rt = p.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            p.GetComponent<Image>().color = bg;
            return p;
        }

        static TextMeshProUGUI Tmp(Transform parent, string name, string text, float size,
            TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta, Color? color = null)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            FarmUIFont.Apply(t);
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.rectTransform.sizeDelta = sizeDelta;
            t.rectTransform.anchoredPosition = pos;
            if (color.HasValue) t.color = color.Value;
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.18f, 0.30f, 0.20f, 0.95f);
            var bc = go.AddComponent<BoxCollider>();   // PC 模式鼠标射线点击
            bc.size = new Vector3(size.x, size.y, 10f);
            Tmp(go.transform, "Text", label, 26f, TextAlignmentOptions.Center, Vector2.zero, size);
            return go.GetComponent<Button>();
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Material mat, float rotX = 0f, float rotY = 0f, float rotZ = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UObject.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material SolidMat(string name, Color c)
        {
            var m = new Material(Shader.Find("Standard")) { name = name };
            m.color = c;
            m.SetFloat("_Glossiness", 0.15f);
            return m;
        }
    }
}
