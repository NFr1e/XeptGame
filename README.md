# XeptKit

XeptKit（简称 Kit）是面向 **Unity 2022 LTS** 的通用基础设施库，服务于中小型项目。

## 1. 项目综述

团队启动新项目时导入 XeptKit，即可获得开发所需的通用基础设施，避免重复造轮子，将精力集中于业务逻辑。

**定位：基础设施服务，而非业务框架。** Kit 不承载任何业务逻辑与玩法规则，只覆盖游戏开发中的通用能力（事件、状态机、资产加载、对象池、输入、本地化、场景管理、UI 等）。

核心设计原则：

- **开箱即用**：各模块提供可直接 `new` 的默认实现类，无需第三方配置即可装配运行。
- **接口化**：Kit 向上层（业务）暴露接口，业务不感知具体实现；实现类均可替换、可构造注入。
- **异步友好**：全库以异步为第一公民（UniTask），不使用协程；异步操作统一受全局生命周期取消令牌约束。
- **模块化**：模块间依赖为有向无环图（DAG），依赖只能自上而下单向流动。

Kit 不提供统一注册表与模块级静态门面，服务装配由业务组合根显式完成（参考示例 [`Assets/Scripts/Runtime/AppEntry.cs`](Assets/Scripts/Runtime/AppEntry.cs)）。

## 2. 技术栈

| 类别 | 选型 |
|---|---|
| 引擎 | Unity 2022.3 LTS（当前 2022.3.62f3） |
| 语言 | C# |
| 异步基础设施 | [UniTask](https://github.com/Cysharp/UniTask)（全库异步第一公民，不使用协程） |
| 资产管理 | Unity Addressables（运行时资产加载默认实现的底层） |
| 输入 | Unity Input System |
| UI | uGUI（UGUI）|
| 测试 | Unity Test Framework（`Assets/XeptKit/Tests`） |
| 交付形态 | 单一运行时程序集 `XeptKit` + 单一编辑器程序集 `XeptKit.Editor` |

## 3. 模块介绍

Kit 划分为四层，依赖方向自上而下单向流动：

```
应用层   UI
服务层   Pool  Input  Localization  Scenes
基础层   Event  FSM  Asset
内核层   Core
```

### Core（内核层）

全框架地基，提供两类服务：

- **全局生命周期设施**：`KitLifecycle` 提供运行会话的统一初始化/关闭与全局取消令牌，所有绑定它的异步操作随会话结束而终止。
- **共享基础类型与工具**：`Result<T>` / `Optional<T>` / `Error`、`OverrideValue<T>` 多优先级覆盖值（多来源竞争单值：玩家设置/玩法效果/调试热改）、`Guard` 参数校验、`ILog` / `Log` 日志门面（可替换实现）、`IAsyncInitializable` / `IDisposableAsync` 异步约定，以及 Transform / GameObject / String / Collections 等常用扩展方法。

### Event（基础层）

应用内的事件发布与订阅，实现模块间解耦通信：

- **同步事件总线 `EventBus`**：类型安全、支持优先级（降序执行）、单 handler 异常隔离不中断后续、订阅返回句柄（`IDisposable`）便于反注册。
- **异步广播-等待 `AsyncEventBus`**：广播事件并等待所有 handler 执行完毕，支持取消令牌，用于"多系统就绪后再继续"的场景。

### FSM（基础层）

纯逻辑的有限状态机，提供轻量的状态机基础设施：

- 不绑定 MonoBehaviour、不依赖渲染与动画，可附着于任意 C# 对象（游戏实体、UI 流程、场景流程等）。
- 状态进入/退出支持异步（UniTask），推进（Tick）由业务显式驱动；取消只中断业务钩子，状态机收敛到确定状态（杜绝半进入/半退出态）。
- 支持**层级复合状态**（HFSM）：`CompositeStateBase` 父状态携带子状态机，父管共享逻辑、子管参数（如 3C 姿态状态机的地面分支）；`IsInHierarchy` 供外部做能力级语义查询；`StateChanged` 层级透明通知（携带 from/to，最深层先触发）。

### Asset（基础层）

资产加载与释放的统一抽象，隔离底层加载实现：

- 按地址（Addressables address）异步加载单个资产，返回持有资产的句柄（`AssetHandle`），`Dispose()` 精确释放引用（每次加载独立引用计数）。
- 提供三套实现：运行时 `AddressablesAssetLoader`（默认）、编辑器 `EditorAssetLoader`（基于 AssetDatabase，加速迭代）、`MockAssetLoader`（供上层单元测试解耦）。

### Pool（服务层）

对象实例的复用管理，消除高频创建/销毁开销：

- **`GameObjectPool`**：GameObject / Component 的复用（子弹、特效、UI 列表项等）。
- **`ReferencePool`**：纯 C# 引用类型对象的复用（临时集合、消息对象等）。
- 取用/归还/预热/收缩均为同步操作，由业务显式驱动。

### Input（服务层）

对 Unity Input System 分发编排的统一封装：

- 业务以"注册-分发"方式接收输入回调，通过输入层（`IInputLayer`）控制优先级与阻断语义。
- 业务直接使用 Input System 原生类型（`InputAction` / `CallbackContext`），Kit 只隔离"回调如何被排序、过滤、阻断"，不隔离输入后端。

### Localization（服务层）

多语言文本的存储、查找与运行时切换：

- 自研轻量表驱动方案，不引入第三方本地化框架；语言包加载完成后全部条目驻留内存字典，查询为同步、低分配。
- 语言切换经事件总线广播通知订阅方刷新文本。
- 编辑器工具：CSV 导入窗口（`LocalizationImporterWindow`）与键常量类生成器（`LocKeysGenerator`）。

### Scenes（服务层）

场景的异步加载、卸载与生命周期衔接：

- 类型安全地异步加载/卸载/切换场景，封装 Unity `SceneManager` 流程细节；场景生命周期事件经事件总线广播。
- 提供场景组（`SceneGroup`）、场景句柄（`SceneHandle`）、加载选项与请求模型，以及过渡抽象（默认实现 `FadeTransition` 淡入淡出）。

### UI（应用层）

处于依赖链顶端的界面基础设施模块，是业务最常使用的模块：

- **UIManager**：界面的加载、组织管理与生命周期。以 Form（界面表单）为单位，支持分组（`UIGroup`）、堆栈管理、打开/关闭过渡、加载与缓存策略，业务通过 `FormHandle` 持有界面引用。
- **Procedural**：程序化图片。在 uGUI 画布中直接应用圆角、描边、渐变、裁剪等效果（基于自定义 Shader 与顶点处理），省去设计-切图步骤。
- **Widgets**：uGUI 原生组件的重写/封装（视图与逻辑分层、虚拟列表等原生缺失能力），规划中。

## 4. 目录结构

```
Assets/
├── XeptKit/          # 框架本体（单一程序集）
│   ├── Core/         # 内核层
│   ├── Event/ FSM/ Asset/        # 基础层
│   ├── Pool/ Input/ Localization/ Scenes/   # 服务层
│   ├── UI/           # 应用层（Manager / Procedural）
│   └── Tests/        # 单元测试
├── Scripts/          # 业务示例：AppEntry 组合根 + SmokingTest 冒烟测试 UI
├── Plugins/          # 第三方库（UniTask 等）
└── Scenes/           # 场景
docs/                 # 设计文档：DESIGN.md 总设计 + modules/ 各模块文档
```

## License

[MIT](LICENSE)
