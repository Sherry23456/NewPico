# 《守望稻田》——Farm 场景生态科普小游戏（含 SampleScene 保护措施）

## Context
纯游戏难参赛，定包装方向：在 Farm 场景内加"守望稻田"生态科普玩法——害虫（蝗虫/田鼠/毛毛虫）从田间土堆钻出要快速拍掉，益虫（青蛙/瓢虫/蜜蜂）钻出**绝不能拍**（打地鼠"炸弹机制"反转为"益虫保护"），结算给科普知识卡 + 训练报告 + 金币/经验奖励接入农场经济。

## ⛔ SampleScene 保护承诺（用户红线）
1. **不修改、不打开、不保存 `Assets/Scenes/SampleScene.unity`**——M3 工具第一行就检查当前打开场景必须是 Farm.unity，否则直接中止
2. **不修改 `Assets/Scripts/` 下任何脚本**（GameManager/Mole/HammerHit 等 13 个），只只读复用 `PrefabPool`、`Grabbable`、`HandGrabber`
3. 开工前先把 SampleScene.unity 备份一份到 `Tools_Dev/backup/`（Assets 外，Unity 不会导入），双保险

已确认决策：程序化低模（零生成额度）、苍蝇拍（复用抓取+挥速判定）、顺手修复 Farm 失效的种植交互（PlotInteractor 没进场景）。

## 玩法设计
- 敲**虫情锣**或点告示牌"开始"（PC 可按 P）→ 3 秒倒计时 → 3 波虫情（10/12/14 只，停留 2.4s→1.5s，益虫率 15%→30%）
- 拍害虫 +分；拍到益虫 → 误伤警告 + 弹科普知识卡（青蛙/瓢虫/蜜蜂等 6 条文案）；害虫逃逸 → 庄稼受损计数
- 结算告示牌：消灭明细/逃逸/误伤/命中率/平均反应时 + S-A-B-C 评级 + 历史对比 + 金币（`消灭×2−误伤×5`，全胜+10）+ 经验接入 FarmProgress
- 自适应难度：上一波命中率 ≥85% 停留 ×0.85，≤50% ×1.15
- 数据落盘：`persistentDataPath/farm_pest_log.json` + 一键导出 CSV（答辩用）

## 新增文件（全部在 Assets/Farm/ 下）
- `Scripts/FarmPestVariant.cs` — 虫子数据（名称/是否益虫/分值/知识卡文案）
- `Scripts/FarmPest.cs` — 土堆冒头状态机（仿 Mole.cs 协程模式，Farm 自治版）
- `Scripts/FarmPestSwatter.cs` — 拍面挥速判定（≥1.5m/s，仿 HammerHit.cs）+ 敲锣检测 + 震动反馈
- `Scripts/FarmPestManager.cs` — 会话状态机/自适应/统计/发金币经验
- `Scripts/FarmPestUI.cs` — 告示牌三面板 + 知识卡弹窗（脏检查仿 UIController）
- `Scripts/FarmPestLog.cs` — JSON 记录 + CSV 导出
- `Editor/FarmPestBuilder.cs` — 菜单 `Farm/M3 添加守望稻田小游戏`（幂等增量）：土堆×5、虫情锣、拍子架、告示牌 Canvas、6 种虫子低模模板（基础几何拼装）、补挂 PlotInteractor/HandGrabber/EventSystem、接线并保存
- `守望稻田-说明与答辩要点.md` — 操作流程、机制映射表、答辩卖点

## 修改文件（3 个 Farm 侧小改）
1. `Farm/Scripts/FarmEconomy.cs` — 加 `AddCoins(int)` 静态方法（约 8 行）
2. `Farm/Scripts/PlotInteractor.cs` — 虫情进行中抑制 grip 种田误触（1 处条件）
3. `Farm/Editor/FarmSceneBuilder.cs` — M1 末尾调用 M3 搭建函数 + 修 SimHei SDF 字体加载路径（`Assets/Font/` → 兼容 `Assets/Fonts/`）；`Farm/Scripts/FarmUIFont.cs` 同步加 Fonts 路径回退

## 验证
1. Unity 编译无报错（有我修）
2. 菜单 `Farm/M3 添加守望稻田小游戏` → 搭建并保存
3. `Farm/工具/截图自查` → 我看 farm_shot.png 核对摆放
4. Play 按 P：三波流程 → 结算报告 → JSON 生成/CSV 导出/金币刷新
5. PICO 实机：拿拍子、敲锣、挥拍判定、坐姿可拍
6. 收尾核对 `git status`：SampleScene.unity 必须零改动