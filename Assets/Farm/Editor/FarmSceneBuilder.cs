using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace FarmTools
{
    /// <summary>
    /// M1 一键搭建水乡场景：
    /// 1. 从 SampleScene 复制已验证的 XR Origin 装备
    /// 2. 程序化搭建庭院（地面/院墙/菜畦/小路）
    /// 3. 归一化摆放 GLB 资产（中式建筑 + Quaternius 通用件）
    /// 4. 光照与雾效
    /// 5. 保存 Assets/Scenes/Farm.unity
    /// </summary>
    public static class FarmSceneBuilder
    {
        const string MenuPath = "Farm/M1 一键搭建水乡场景";
        const string RigName = "XR Origin (XR Rig)";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        const string FarmScenePath = "Assets/Scenes/Farm.unity";
        const string ModelDir = "Assets/Farm/Models";

        // 剥离 XRI 装备里自带的移动组件，避免与 FarmLocomotion 冲突
        static readonly string[] StripTypes =
        {
            "DynamicMoveProvider", "TeleportationProvider", "GrabMoveProvider",
            "SnapTurnProvider", "CharacterControllerDriver", "ContinuousMoveProvider",
            "ContinuousTurnProvider"
        };

        [MenuItem("Farm/工具/截图自查")]
        public static void Capture()
        {
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 15f, -20f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            cam.fieldOfView = 55f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            RenderTexture.active = null;
            cam.targetTexture = null;
            byte[] png = tex.EncodeToPNG();
            UObject.DestroyImmediate(rt);
            UObject.DestroyImmediate(tex);
            UObject.DestroyImmediate(camGo);
            string p1 = @"D:\Qklunity\NewPico\Assets\Farm\farm_shot.png";
            System.IO.File.WriteAllBytes(p1, png);
            Debug.Log($"[FarmShot] saved len={png.Length} exists={System.IO.File.Exists(p1)}");
        }

        [MenuItem("Farm/工具/截图俯视")]
        public static void CaptureTop()
        {
            var camGo = new GameObject("ShotCamTop");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 45f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 21f;
            cam.clearFlags = CameraClearFlags.Skybox;
            int w = 1680, h = 1500;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            byte[] png = tex.EncodeToPNG();
            UObject.DestroyImmediate(rt);
            UObject.DestroyImmediate(tex);
            UObject.DestroyImmediate(camGo);
            string p1 = @"D:\Qklunity\NewPico\Assets\Farm\farm_top.png";
            System.IO.File.WriteAllBytes(p1, png);
            Debug.Log($"[FarmTop] saved len={png.Length}");
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // 质量修复：模板把阴影禁用了 + MSAA=0（策划案技术红线要求 4x MSAA + 实时阴影）
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 60f;
            // ---------- 1. 取装备 ----------
            var sample = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            GameObject rigSrc = null;
            foreach (var go in sample.GetRootGameObjects())
                if (go.name == RigName) { rigSrc = go; break; }
            if (rigSrc == null)
            {
                Debug.LogError($"[FarmM1] {SampleScenePath} 里找不到 {RigName}，中止。");
                return;
            }
            var rig = UObject.Instantiate(rigSrc);
            rig.name = RigName;

            // ---------- 2. 新场景 ----------
            var farm = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(rig, farm);

            var root = new GameObject("FarmEnvironment");

            // ---------- 3. 地面 / 院墙 / 菜畦 / 小路 ----------
            var grass = SolidMat("M_Grass", new Color(0.30f, 0.64f, 0.18f));
            var soil = SolidMat("M_Soil", new Color(0.50f, 0.32f, 0.16f));
            var wallM = SolidMat("M_Wall", new Color(0.87f, 0.82f, 0.72f));
            var woodM = SolidMat("M_Wood", new Color(0.48f, 0.33f, 0.19f));
            var pathM = SolidMat("M_Path", new Color(0.78f, 0.72f, 0.58f));

            Box(root, "Ground", new Vector3(40f, 0.1f, 40f), new Vector3(0f, -0.05f, 0f), grass);
            Box(root, "Path", new Vector3(2.4f, 0.02f, 12f), new Vector3(0f, 0.011f, -2.5f), pathM);

            // 院墙：石墙模块段拼接（Quaternius StoneWall），南墙留门
            PlaceWallRun(root, "WallN", new Vector3(0f, 0f, 9f), 8, 0f);
            PlaceWallRun(root, "WallW", new Vector3(-12f, 0f, 0f), 6, 90f);
            PlaceWallRun(root, "WallE", new Vector3(12f, 0f, 0f), 6, 90f);
            PlaceWallRun(root, "WallS_L", new Vector3(-7.0f, 0f, -9f), 3, 0f);
            PlaceWallRun(root, "WallS_R", new Vector3(7.0f, 0f, -9f), 3, 0f);
            Box(root, "GatePost_L", new Vector3(0.4f, 1.35f, 0.4f), new Vector3(-1.6f, 0.67f, -9f), woodM);
            Box(root, "GatePost_R", new Vector3(0.4f, 1.35f, 0.4f), new Vector3(1.6f, 0.67f, -9f), woodM);

            // 6 垄菜畦（东侧菜园，3×2）
            float[] xs = { 1.2f, 4.0f, 6.8f };
            float[] zs = { -2.2f, 0.8f };
            int plot = 0;
            foreach (float z in zs)
                foreach (float x in xs)
                    Box(root, $"Plot_{++plot}", new Vector3(2.4f, 0.25f, 1.4f), new Vector3(x, 0.125f, z), soil);

            // ---------- 4. GLB 资产 ----------
            // 水乡下载件（poly.pizza，CC 授权，见 shuixiang/credits.txt）
            string SD = ModelDir + "/shuixiang";
            PlaceNorm($"{SD}/VillageHouse.glb", "House", root, new Vector3(-7.2f, 0f, 3.6f), 6.5f, 180f);
            PlaceNorm($"{SD}/Well_J.glb", "Well", root, new Vector3(2.2f, 0f, -4.8f), 1.5f, 0f);
            PlaceNorm($"{SD}/SmallBridge.glb", "Bridge", root, new Vector3(5.5f, 0f, 2.6f), 3.0f, 0f);
            PlaceNorm($"{SD}/Boat.glb", "Boat", root, new Vector3(6.4f, 0f, 5.6f), 2.6f, 40f);
            PlaceNorm($"{SD}/Dock.glb", "Dock", root, new Vector3(3.9f, 0f, 4.0f), 3.0f, -25f);
            PlaceNorm($"{SD}/MarketStand.glb", "MarketStand", root, new Vector3(-3.0f, 0f, -7.4f), 2.2f, 15f);
            PlaceNorm($"{SD}/Greenhouse.glb", "Greenhouse", root, new Vector3(9.6f, 0f, 1.0f), 3.5f, 0f);
            PlaceNorm($"{SD}/Pergola.glb", "Pergola", root, new Vector3(7.9f, 0f, -4.8f), 3.0f, 90f);
            PlaceNorm($"{SD}/WoodPile.glb", "WoodPile", root, new Vector3(-5.6f, 0f, 7.0f), 1.2f, -15f);
            PlaceNorm($"{SD}/Hut_Q.glb", "Hut", root, new Vector3(-10.3f, 0f, -6.4f), 3.0f, 160f);
            PlaceNorm($"{SD}/PostLantern.glb", "PostLantern_L", root, new Vector3(-2.0f, 0f, -7.0f), 1.1f, 0f);
            PlaceNorm($"{SD}/PostLantern.glb", "PostLantern_R", root, new Vector3(2.0f, 0f, -7.0f), 1.1f, 0f);
            PlaceNorm($"{SD}/Lantern.glb", "Lantern", root, new Vector3(-5.9f, 0f, 1.6f), 0.7f, 0f);
            PlaceNorm($"{SD}/StoneLamp.glb", "StoneLamp", root, new Vector3(-8.9f, 0f, 1.2f), 1.0f, 0f);

            // 自制水车（全网无更优水车件，保留）与石磨（小件）
            // 水车坐标为实测校准值：pivot 语义与包围盒不同，(7.2,20.4) 可使水塘中心落在约 (5.5,4.5)
            PlaceCNChild("Assets/Farm/Models/cn_shuixiang.glb", "CN_WaterWheel", root, new Vector3(7.2f, 0f, 20.4f), 0f);
            PlaceCNChild("Assets/Farm/Models/cn_shuixiang.glb", "CN_Mill", root, new Vector3(-9.1f, 0f, -7.4f), 0f);

            PlaceNorm($"{ModelDir}/Trees.glb", "Trees_A", root, new Vector3(9.6f, 0f, 7.6f), 4.5f, 20f);
            PlaceNorm($"{ModelDir}/Trees.glb", "Trees_B", root, new Vector3(-10.6f, 0f, 7.8f), 4.5f, 140f);
            PlaceNorm($"{ModelDir}/Fence.glb", "Fence_A", root, new Vector3(-0.2f, 0f, 2.6f), 2.2f, 0f);
            PlaceNorm($"{ModelDir}/Fence.glb", "Fence_B", root, new Vector3(2.4f, 0f, 2.6f), 2.2f, 0f);
            PlaceNorm($"{ModelDir}/Fence.glb", "Fence_C", root, new Vector3(5.0f, 0f, 2.6f), 2.2f, 0f);
            PlaceNorm($"{ModelDir}/Fence.glb", "Fence_D", root, new Vector3(7.6f, 0f, 2.6f), 2.2f, 0f);
            PlaceNorm($"{ModelDir}/Haystack.glb", "Haystack", root, new Vector3(9.8f, 0f, -7.4f), 1.8f, 0f);
            PlaceNorm($"{ModelDir}/Pumpkin.glb", "Pumpkin_A", root, new Vector3(-4.6f, 0f, 1.4f), 0.45f, 30f);
            PlaceNorm($"{ModelDir}/Pumpkin.glb", "Pumpkin_B", root, new Vector3(-4.1f, 0f, 0.9f), 0.40f, 0f);
            PlaceNorm($"{ModelDir}/Pumpkin.glb", "Pumpkin_C", root, new Vector3(-5.0f, 0f, 0.7f), 0.42f, 70f);
            PlaceNorm($"{ModelDir}/Chicken.glb", "Chicken_A", root, new Vector3(2.6f, 0f, 6.8f), 0.45f, -40f);
            PlaceNorm($"{ModelDir}/Chicken.glb", "Chicken_B", root, new Vector3(3.5f, 0f, 7.3f), 0.45f, 100f);
            PlaceNorm($"{ModelDir}/Sheep.glb", "Sheep", root, new Vector3(10.3f, 0f, 3.2f), 1.4f, -70f);
            PlaceNorm($"{ModelDir}/Crops.glb", "Crops_Demo", root, new Vector3(4.0f, 0.25f, -2.2f), 2.0f, 0f);

            // 点缀：鸡舍/灌木/花丛/草丛（M6 加密）
            PlaceNorm($"{ModelDir}/deco/ChickenCoop.glb", "ChickenCoop", root, new Vector3(1.6f, 0f, 7.9f), 1.4f, 180f);
            PlaceNorm($"{ModelDir}/deco/Bushes.glb", "Bushes_N", root, new Vector3(-11.0f, 0f, 8.2f), 1.6f, 0f);
            PlaceNorm($"{ModelDir}/deco/Bushes.glb", "Bushes_NE", root, new Vector3(11.0f, 0f, 8.2f), 1.6f, 90f);
            PlaceNorm($"{ModelDir}/deco/BushFlowers.glb", "BushFlowers_W", root, new Vector3(-11.2f, 0f, -2.0f), 1.3f, 0f);
            PlaceNorm($"{ModelDir}/deco/Flowers.glb", "Flowers_House", root, new Vector3(-5.6f, 0f, 5.9f), 0.7f, 0f);
            PlaceNorm($"{ModelDir}/deco/Flowers.glb", "Flowers_Gate", root, new Vector3(-2.6f, 0f, -7.6f), 0.6f, 20f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_A", root, new Vector3(-4.0f, 0f, -3.5f), 1.3f, 0f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_B", root, new Vector3(7.5f, 0f, -6.5f), 1.3f, 90f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_C", root, new Vector3(-8.5f, 0f, 1.5f), 1.3f, 30f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_D", root, new Vector3(9.2f, 0f, -3.0f), 1.2f, 60f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_E", root, new Vector3(3.2f, 0f, 6.6f), 1.2f, 120f);
            PlaceNorm($"{ModelDir}/deco/GrassMix.glb", "Grass_F", root, new Vector3(-6.4f, 0f, -5.4f), 1.1f, 200f);

            // ---------- 4.5 M2 玩法系统 ----------
            BuildM2Extras(root);
            BuildTopHUD(root, rig);
            FarmPestBuilder.BuildInto(root, rig);   // M3：守望稻田虫情小游戏

            // ---------- 5. 移动组件 ----------
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>();
                for (int i = comps.Length - 1; i >= 0; i--)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    var tn = c.GetType().Name;
                    foreach (var s in StripTypes)
                        if (tn == s) { UObject.DestroyImmediate(c); break; }
                }
            }
            var cc = rig.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.center = new Vector3(0f, 0.9f, 0f); cc.radius = 0.3f; cc.slopeLimit = 45f; cc.stepOffset = 0.4f;
            rig.AddComponent<FarmLocomotion>();
            rig.AddComponent<FarmTeleport>();
            rig.AddComponent<FarmPC>();   // 无 HMD 时自动启用 PC 键鼠模式

            // ---------- 6. 光照与雾 ----------
            var sunGo = new GameObject("FarmSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sunGo.transform.SetParent(root.transform, true);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.90f);
            RenderSettings.ambientIntensity = 0.85f;
            RenderSettings.sun = sun;

            // ---------- 7. 收尾 ----------
            EditorSceneManager.CloseScene(sample, true);
            EditorSceneManager.MarkSceneDirty(farm);
            EditorSceneManager.SaveScene(farm, FarmScenePath);
            Debug.Log($"[FarmM1] BUILD OK —— Farm 场景已生成并保存：{FarmScenePath}");
        }

        static GameObject Box(GameObject parent, string name, Vector3 size, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;
            go.transform.position = pos;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(parent.transform, true);
            return go;
        }

        static Material SolidMat(string name, Color c)
        {
            var m = new Material(Shader.Find("Standard")) { name = name };
            m.color = c;
            m.SetFloat("_Glossiness", 0.15f);
            return m;
        }

        /// <summary>实例化 glb 预制体，按包围盒最大边归一化到 targetMax；包围盒中心对齐 pos.xz，底部落到 pos.y。</summary>
        static GameObject PlaceNorm(string path, string name, GameObject parent, Vector3 pos, float targetMax, float rotY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[FarmM1] 缺少资产 {path}"); return null; }
            var go = (GameObject)UObject.Instantiate(prefab);
            go.name = name;
            float s = 1f;
            if (Measure(go, out var b) && b.size.sqrMagnitude > 1e-6f)
            {
                float m = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                s = targetMax / m;
                go.transform.localScale *= s;
            }
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            if (Measure(go, out var b2))
            {
                var center = b2.center;
                go.transform.position += new Vector3(pos.x - center.x, 0f, pos.z - center.z);
                go.transform.position += Vector3.up * (pos.y - b2.min.y);
            }
            go.transform.SetParent(parent.transform, true);
            return go;
        }

        /// <summary>cn_shuixiang.glb 是三件合一的包：取出指定子级并摆位（子级自带真实尺寸，不归一化）。</summary>
        static GameObject PlaceCNChild(string path, string childName, GameObject parent, Vector3 pos, float rotY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[FarmM1] 缺少资产 {path}"); return null; }
            var root = (GameObject)UObject.Instantiate(prefab);
            root.name = "CN_Pack";
            var t = root.transform.Find(childName);
            if (t == null) { Debug.LogWarning($"[FarmM1] {path} 里没有 {childName}"); UObject.DestroyImmediate(root); return null; }
            t.SetParent(parent.transform, true);
            t.rotation = Quaternion.Euler(0f, rotY, 0f);
            t.position = pos;
            UObject.DestroyImmediate(root);
            return t.gameObject;
        }

        static bool Measure(GameObject go, out Bounds bounds)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { bounds = default; return false; }
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }

        // ================= M2 玩法系统 =================

        static void BuildM2Extras(GameObject root)
        {
            var sproutM = SolidMat("M_Sprout", new Color(0.35f, 0.75f, 0.30f));
            var crops = new (string id, string file, float h)[]
            {
                ("cabbage", "Cabbage", 0.35f),
                ("carrot",  "Carrot",  0.40f),
                ("pepper",  "Pepper",  0.55f),
                ("corn",    "Corn",    1.10f),
            };

            // 1) 地块组件化 + 高亮环 + 作物阶段套件
            var plots = new List<GameObject>();
            foreach (Transform t in root.transform)
                if (t.name.StartsWith("Plot_")) plots.Add(t.gameObject);

            foreach (var plotGo in plots)
            {
                var plot = plotGo.AddComponent<FarmPlot>();
                Vector3 top = plotGo.transform.position + Vector3.up * 0.125f;

                // 菜畦木框围边（抬升观感）
                var woodM2 = SolidMat("M_BedWood", new Color(0.52f, 0.36f, 0.22f));
                var pc = plotGo.transform.position;
                Box(root, plotGo.name + "_FrN", new Vector3(2.6f, 0.34f, 0.12f), new Vector3(pc.x, 0.17f, pc.z - 0.74f), woodM2);
                Box(root, plotGo.name + "_FrS", new Vector3(2.6f, 0.34f, 0.12f), new Vector3(pc.x, 0.17f, pc.z + 0.74f), woodM2);
                Box(root, plotGo.name + "_FrW", new Vector3(0.12f, 0.34f, 1.5f), new Vector3(pc.x - 1.24f, 0.17f, pc.z), woodM2);
                Box(root, plotGo.name + "_FrE", new Vector3(0.12f, 0.34f, 1.5f), new Vector3(pc.x + 1.24f, 0.17f, pc.z), woodM2);

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Ring";
                ring.transform.localScale = new Vector3(2.7f, 0.012f, 2.7f);
                ring.transform.position = top + Vector3.up * 0.06f;
                ring.GetComponent<MeshRenderer>().sharedMaterial = SolidMat("M_Ring", Color.white);
                UObject.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(plotGo.transform, true);
                ring.SetActive(false);

                // 遮阳棚布（光变量视觉，Lv4 解锁开合）
                var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cloth.name = "ShadeCloth";
                cloth.transform.localScale = new Vector3(2.6f, 0.04f, 1.6f);
                cloth.transform.position = top + Vector3.up * 1.15f;
                cloth.GetComponent<MeshRenderer>().sharedMaterial = SolidMat("M_Cloth", new Color(0.72f, 0.55f, 0.35f));
                UObject.DestroyImmediate(cloth.GetComponent<Collider>());
                cloth.transform.SetParent(plotGo.transform, true);
                cloth.SetActive(false);

                foreach (var c in crops)
                {
                    var def = CropDB.Get(c.id);
                    var set = new GameObject("Set_" + c.id);
                    set.transform.SetParent(plotGo.transform, false);
                    set.transform.position = top;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/crops/{c.file}.glb");

                    var s0 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    s0.name = "S0";
                    s0.transform.SetParent(set.transform, false);
                    s0.transform.localScale = new Vector3(0.16f, 0.18f, 0.16f);
                    s0.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    s0.GetComponent<MeshRenderer>().sharedMaterial = sproutM;
                    UObject.DestroyImmediate(s0.GetComponent<Collider>());

                    if (def.Stages >= 3)
                        PlaceCropModel(prefab, set.transform, c.h * 0.45f);
                    var last = PlaceCropModel(prefab, set.transform, c.h);
                    if (last != null) last.name = $"S{set.transform.childCount - 1}";

                    set.SetActive(false);
                }
            }

            // 2) 出货区（集市摊前）+ 经济系统
            var sell = new GameObject("SellZone");
            sell.AddComponent<FarmEconomy>();
            sell.transform.SetParent(root.transform, false);
            sell.transform.position = new Vector3(-3.0f, 1.0f, -6.2f);
            var sc = sell.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(2.6f, 2.4f, 1.8f);

            // 3) 公告板（出生点对面）
            var board = new GameObject("FarmBoard");
            var tmp = board.AddComponent<TextMeshPro>();
            var heiSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/simkai SDF.asset");
            if (heiSdf == null) heiSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SimHei SDF.asset");
            if (heiSdf == null) heiSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/SimHei SDF.asset");
            FarmUIFont.SetFont(heiSdf);
            FarmUIFont.Apply(tmp);
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.97f, 0.85f);
            tmp.text = "欢迎回到小院";
            tmp.rectTransform.sizeDelta = new Vector2(3.6f, 1.8f);
            board.transform.SetParent(root.transform, false);
            board.transform.position = new Vector3(0f, 2.35f, -8.0f);
            board.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 4) 音频源
            var audioGo = new GameObject("FarmAudio");
            var fa = audioGo.AddComponent<FarmAudio>();
            var clipList = new List<AudioClip>();
            foreach (var n in new[] { "dig", "water", "plant", "harvest", "coin", "ui" })
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ModelDir}/../Audio/{n}.wav");
                if (c != null) clipList.Add(c);
                else Debug.LogWarning($"[FarmM1] 缺少音效 {n}.wav");
            }
            fa.clips = clipList.ToArray();
            audioGo.transform.SetParent(root.transform, false);

            // 6) NPC 禾伯
            BuildNpc(root);
        }

        static AudioSource MakeAmbSource(GameObject parent, string name, AudioClip clip)
        {
            var go = new GameObject("Amb_" + name);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = clip != null;
            a.spatialBlend = 0f;
            a.volume = 0f;
            if (clip != null) a.Play();
            go.transform.SetParent(parent.transform, false);
            return a;
        }

        /// <summary>石墙段拼接一排（center 为排中心，自动回推导起点）。</summary>
        static void PlaceWallRun(GameObject root, string baseName, Vector3 center, int count, float rotY)
        {
            var dir = Quaternion.Euler(0f, rotY, 0f) * Vector3.right;
            var start = center - dir * (3.2f * count * 0.5f);
            for (int i = 0; i < count; i++)
            {
                var p = start + dir * (3.2f * i);
                PlaceNorm($"{ModelDir}/deco/StoneWall.glb", $"{baseName}_{i}", root, p, 3.2f, rotY);
            }
        }

        [MenuItem("Farm/工具/出APK")]
        public static void BuildApk()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Farm.unity", true) };
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Farm.unity" },
                locationPathName = "D:/Qklunity/NewPico/Builds/Farm.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[FarmApk] result={report.summary.result} size={report.summary.totalSize / 1048576}MB -> {options.locationPathName}");
        }

        static void BuildNpc(GameObject root)
        {
            // 6) NPC 禾伯（姿势已烘焙的自然站立版，绕过 glTFast 蒙皮导入崩溃）
            var npc = PlaceNorm($"{ModelDir}/npc/NPC_A_static.glb", "NPC_HeBo", root, new Vector3(0.8f, 0f, -5.6f), 1.55f, 180f);
            if (npc != null) npc.AddComponent<FarmNPC>();
            else Debug.LogWarning("[FarmM1] 禾伯模型缺失");
        }

        /// <summary>作物阶段模型：按目标高度归一化，底部落在 parent 平面。</summary>
        static GameObject PlaceCropModel(GameObject prefab, Transform parent, float targetH)
        {
            if (prefab == null) { Debug.LogWarning("[FarmM1] 作物预制体缺失"); return null; }
            var go = (GameObject)UObject.Instantiate(prefab);
            go.name = "cropModel";
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { UObject.DestroyImmediate(go); return null; }
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float s = targetH / Mathf.Max(b.size.y, 0.01f);
            go.transform.localScale *= s;
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y, parent.position.z - b.center.z);
            go.transform.SetParent(parent, true);
            return go;
        }

        /// <summary>顶部 HUD：任务/时段/天气按钮（跟随视角的世界空间画布，XRI 射线可点）。</summary>
        static void BuildTopHUD(GameObject root, GameObject rig)
        {
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam == null) { Debug.LogWarning("[FarmM1] 无相机，HUD 跳过"); return; }

            var hudGo = new GameObject("TopHUD");
            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            hudGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            hudGo.transform.SetParent(cam.transform, false);
            hudGo.transform.localPosition = new Vector3(0f, 0.40f, 1.5f);
            hudGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            hudGo.transform.localScale = Vector3.one * 0.0016f;
            var rt = hudGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(780f, 170f);

            var bgGo = new GameObject("BG", typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(hudGo.transform, false);
            bgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(780f, 170f);
            bgGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.62f);

            var questGo = new GameObject("QuestText", typeof(TextMeshProUGUI));
            questGo.transform.SetParent(hudGo.transform, false);
            var q = questGo.GetComponent<TextMeshProUGUI>();
            FarmUIFont.Apply(q);
            q.fontSize = 30f;
            q.alignment = TextAlignmentOptions.Left;
            q.rectTransform.sizeDelta = new Vector2(430f, 150f);
            questGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-165f, 0f);

            hudGo.AddComponent<FarmHUD>();
        }

        static void MakeHudButton(GameObject canvas, string name, string label, Vector2 pos, Vector2 size)
        {
            var btnGo = new GameObject(name, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            btnGo.transform.SetParent(canvas.transform, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            btnGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.28f, 0.42f, 0.92f);

            // PC 模式：给按钮加碰撞体，供鼠标射线点击
            var bc = btnGo.AddComponent<BoxCollider>();
            bc.size = new Vector3(size.x, size.y, 10f);

            var txtGo = new GameObject("Text", typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            var t = txtGo.GetComponent<TextMeshProUGUI>();
            FarmUIFont.Apply(t);
            t.text = label;
            t.fontSize = 24f;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = size;
        }
    }
}
