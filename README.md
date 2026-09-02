# XeptGame

基于 **Unity 2022.3 LTS** 的第一人称游戏 Demo（Far Cry 式 FPS × 生存玩法雏形），
**以落地成熟游戏架构为主要目标**：代码分层、接口隔离、状态机、事件驱动、组合根装配，都是刻意练习的对象。

## 两层结构

依赖方向严格单向：`XeptGame → XeptKit → UniTask / Unity.Addressables / Unity.InputSystem`

| 层 | 位置 | 定位 |
|---|---|---|
| **XeptKit** | `Assets/Plugins/XeptKit` | 通用基础设施库：事件、FSM（HFSM）、资源加载、对象池、输入分层、本地化、场景管理、UI（Manager + Procedural）。**无任何业务/玩法逻辑**；各模块提供可 `new` 的默认实现，可构造注入替换 |
| **XeptGame** | `Assets/Scripts` | 游戏本体：组合根（`Core/AppEntry.cs`）、应用级 FSM（AppFSM）、3C 玩家控制（输入/视角/电机/观感） |

## 技术栈

| 类别 | 选型 |
|---|---|
| 引擎 | Unity 2022.3 LTS（2022.3.62f3） |
| 异步 | **UniTask 优先，全库不用协程**；异步统一绑定 `KitLifecycle.GlobalToken` |
| 资产 | Unity Addressables（运行时加载默认实现；编辑器注入 EditorAssetLoader 加速迭代） |
| 输入 | Unity Input System（`InputManager` 按 `IInputLayer` 优先级/阻断分发） |
| 渲染 | URP |
| UI | uGUI（`UIManager` Form/Group + Procedural 程序化图片） |
| 测试 | Unity Test Framework（EditMode；`Assets/Plugins/XeptKit/Tests`，程序集 `XeptKit.Tests`） |

## 目录结构

```
XeptGameProject/
├── Assets/
│   ├── Plugins/
│   │   ├── XeptKit/          # 框架（Core → Event/FSM/Asset → Pool/Input/Localization/Scenes → UI）
│   │   ├── KCC/              # KinematicCharacterController（vendor 程序集，黑盒电机，1 处业务修改点）
│   │   ├── UniTask/          # 第三方
│   │   ├── Demigiant/        # DOTween（移动平台示例用）
│   │   └── Sirenix/          # Odin Inspector（暂未使用）
│   ├── Scripts/              # 游戏本体
│   │   ├── Core/             # 组合根 AppEntry + AppFSM（6 态）+ 全局输入
│   │   ├── Player/           # 3C：Input / Look / Motor（HFSM）/ CameraFeel（观感）/ Platforms
│   │   └── UI/               # 界面薄壳
│   ├── Resource/#Game/       # 游戏配置资产（InputActions / Profiles / SceneGroups）
│   └── Scenes/               # SampleScene（3C 开发场景）
└── docs/                     # 设计文档（位于仓库根，不在工程内，见下方"文档"）
```

## 运行与测试

- **打开工程**：Unity Hub 选择 `XeptGameProject/`（Unity 2022.3）；
- **运行**：打开 `Assets/Scenes/SampleScene.unity` 进入 Play Mode（3C 开发场景：移动/跳跃/冲刺/蹲伏/移动平台；F8 调试 HUD）；
- **测试**（EditMode）：Window > General > Test Runner，或命令行：
  ```
  "<UnityEditor>/Unity.exe" -batchmode -nographics -projectPath "XeptGameProject" \
    -runTests -testPlatform EditMode -testResults "test-results.xml" -logFile -
  ```
- **构建**：File > Build Settings（暂无自动化构建脚本）。

## 文档

设计文档位于 **仓库根 `docs/`**（`XeptGameProject/` 之外，不入版本库）：

```
docs/
├── DESIGN.md                          # 项目总述（架构概览 + 文档地图）
├── modules/
│   ├── 3C_CharacterMotor_Design.md    # 3C 电机设计决议（HFSM 状态/时序/端口/踩坑修复）
│   └── 3C_CameraFeel_Design.md        # 相机观感层设计决议（HeadBob/LandingKick/FOV + 调试记录）
└── samples/                           # 学习对照素材（KCC 原始副本 + CameraViewSample 试实现）
```

## License

[MIT](LICENSE)
