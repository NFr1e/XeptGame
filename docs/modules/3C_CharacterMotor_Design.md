# 3C CharacterMotor 设计决议

> 状态：**定稿（状态设计）/ 待确认（KCC 提取方案）**
> 模块：XeptGame 玩家角色控制（移动/跳跃/蹲伏/滑动）
> 关联：`Player/Input/`（输入意图层，已实现）、`Player/Look/`（视角层，已实现）、XeptKit FSM（HFSM）

## 1. 目标与边界

### 1.1 目标

第一人称角色控制（3C）的运动决策层：把 KCC 示例中"模糊 bool 状态表达"（`_isCrouching`/`_shouldBeCrouching`/`_jumpRequested` 等）转换为 **XeptKit HFSM 的严格状态类 + 接口语义**；底层运动学物理求解（碰撞/坡度/step/移动平台）仍由 **KCC KinematicCharacterMotor** 成熟电机承担。

### 1.2 分层与数据流（五层）

```
输入层   PlayerInputController（绑定 InputLayer，写意图状态）       [已实现]
意图层   PlayerMotorInputState / PlayerLookInputState              [已实现]
决策层   PlayerMotor FSM（HFSM：姿态状态类）                        [本模块]
适配层   PlayerCharacterController : ICharacterController（翻译器） [本模块]
电机层   KinematicCharacterMotor（KCC，第三方，黑盒）              [待提取]
```

依赖方向单向：决策层 → 意图层/视角层/电机层；适配层是**决策层与电机的唯一接触面**。

## 2. 状态设计（定稿）

### 2.1 结构

```
PlayerMotor FSM（HFSM，Context = PlayerMotorContext）
├── Grounded（复合状态：稳定接地）
│   ├── Idle        —— 静止（速度趋零）
│   ├── Walk        —— 行走（默认速度档）
│   ├── Sprint      —— 冲刺（高速 + 规则：禁蹲/禁瞄准/视场变化）
│   └── Crouch      —— 蹲伏（低速移动 + 胶囊变矮 + 起身 overlap 检查；禁冲刺）
└── Airborne（复合状态：非稳定接地）
    ├── Fall        —— 自由落体（空气控制 + 重力 + 拖拽）
    └── UnstableGround     —— 不可站立地面（不稳定接地：坡面（法线超稳定角）**或**主动向下探测到非 StableGroundLayers 对象；可跳）
```

### 2.2 体验规则（定稿）

| 规则 | 决策 | 说明 |
|---|---|---|
| 蹲 vs 冲刺 | **互斥同层**（Crouch 与 Idle/Walk/Sprint 同级互斥） | 调研结论：冲刺与蹲伏在主流游戏中为两种互斥状态，不存在蹲伏冲刺；最多蹲伏翻滚（见 §6 扩展） |
| 蹲下移动 | Crouch 内可**低速移动**（蹲走），速度受限于蹲伏档 | 互斥的是"冲刺"，不是"移动" |
| 蹲下跳跃 | **禁跳**（先起身再跳） | 简化决策，可后续调整（蹲跳需起身+跳的组合） |
| 上下坡速度 | `slopeGravityInfluence`（0~1）+ `minSlopeUpSpeedFactor`（B1 目标速度修正） | 地面目标速度 = speed × (1 ± 系数×sinθ)——**下坡加速、上坡减速**（重力切向影响；平地无影响）；上坡速度钳制到 speed × minFactor（避免陡坡卡住）。0 系数 = KCC 默认（坡面速度恒等于速度档） |
| Sprint 中按蹲 | 直接转移 Crouch（视为冲刺取消） | 保证互斥语义无歧义 |
| 跳跃 | 支持（土狼时间 PostGroundingGrace；跳跃缓冲 PreGroundingGrace 待实现） | 计时器放 Context（Kit FSM 约定：会话数据不得存状态字段） |
| UnstableGround 跳跃 | 可跳（AllowJumpingWhenUnstableGround，可配置） | 沿不稳定地面法线起跳 |
| 滑动受限运动 | `slidingControl`（0~1）+ `slidingAcceleration`（单开配置，独立于 AirControl） | **合成而非钳制**：保留原有速度（跑上滑动面动量连续，不减速）；输入沿表面切向附加弱化加速（可操控方向/脱离，避免卡死）。不设目标速度/速度上限——"受限感"由弱化系数 + 有限加速度体现。语义独立：空中转向（AirControl）与表面滑动操控（SlidingControl）参数互不干扰 |
| 空中控制 | `airControl` 系数（0~1，UE AirControl 语义，手感决议：**保持惯性、弱转向**） | 空中**不沿速度方向加速**（起跳保持地面速度）；仅"转向"分量（垂直当前水平速度的输入）乘系数弱化生效；无水平速度时弱化启动。修复 KCC 示例"限速投影把转向投影掉"导致空中输入无效的问题 |
| 参考系 = body（局部移动意图） | `LocalMoveIntent`（body 局部空间）= MoveInput × 相对 body 的视角 yaw；消费方经 `Context.WorldMoveIntent`（`IMotor.TransformDirection`，body 局部 → 世界）使用 | **参考系统一**：body 被移动平台旋转带动时，视角（相机 local 相对 body）与移动（局部意图转世界）同步跟随参考系——天然一致，**无需"平台脱离并入基准"**；正常地面 body=identity 时局部=世界，FPS"W 朝视角"语义不变。替代了"相机世界旋转/全共轭/离开并入"等方案 |

### 2.3 转移表

| From | To | 条件 | 驱动 |
|---|---|---|---|
| Idle | Walk | 移动输入非零 | 输入 |
| Walk | Idle | 移动输入归零 | 输入 |
| Idle/Walk | Sprint | 冲刺输入（长按/切换按配置） | 输入 |
| Sprint | Idle/Walk | 冲刺释放 / 移动输入归零 | 输入 |
| Idle/Walk/Sprint | Crouch | 蹲伏输入（Sprint 中按蹲 = 取消冲刺进蹲） | 输入 |
| Crouch | Idle | 蹲伏释放 + 起身 overlap 检查通过（失败保持 Crouch） | 输入 + 物理检查 |
| Grounded（任意子态） | Fall | 跳跃（缓冲窗口 + 土狼时间窗口内接地）或稳定接地丢失 | 输入 / 物理 |
| Grounded | UnstableGround | 接地但不稳定（坡面或非 StableGroundLayers 对象，经 Fall 中转） | 物理 |
| Fall | Grounded | 稳定接地边沿（IsStableOnGround） | 物理 |
| Fall | UnstableGround | 重新探到"可滑面"（IsUnstableGroundSurface：坡面 或 不可站立层） | 物理 |
| UnstableGround | Grounded | 地面变稳定（稳定层 + 法线角恢复） | 物理 |
| UnstableGround | Fall | 跳跃（可配）、地面丢失、或地面恢复可站立（非可滑面） | 输入 / 物理 |

### 2.4 状态职责划分原则

- **状态 = 行为类别**（引入新的运动学规则或物理副作用）：Grounded/Airborne/Fall/UnstableGround/Crouch 各自独立；
- **档位 = 参数**：Walk/Sprint 的速度、加速度等为参数，但**作为独立状态表达**（定稿）——理由：FPS 中 Sprint 携带规则（禁蹲/FOV/体力），状态化使规则内聚、转移显式、调试清晰；
- 状态类**禁止承载跨会话可变数据**（Kit FSM 约定：实例按类型缓存复用），全部会话数据放 `PlayerMotorContext`。

## 3. 时序与驱动（定稿）

### 3.1 Fsm.Tick 挂 Update（含时序修正记录）

- **时序**：FixedUpdate 中 `KinematicCharacterSystem`（`[DefaultExecutionOrder(-100)]`）先更新电机 → Update 阶段 InputSystem 处理输入事件并触发回调（本帧边沿标记置位）→ `PlayerMotor.Update` 调用 `Fsm.Tick(dt)`——同时读到**本物理帧 GroundingStatus** 与**本帧输入边沿**（输入零延迟）；
- **修正记录**（初版挂 FixedUpdate 的问题）：InputSystem 回调在 Update 阶段（晚于 FixedUpdate），若 `Fsm.Tick` 挂 FixedUpdate，边沿标记（跳跃按下等）在置位帧内已被错过、又被 LateUpdate 清除，**Fsm 永远读不到**——表现为"仅能移动，冲刺/蹲伏/跳跃失效"。改挂 Update 后输入零延迟，物理状态仍为本帧值（FixedUpdate 先于 Update）；
- **输入折叠**：Sprint/Crouch 的"长按/切换"语义在**输入层折叠为 Held 持续值**（长按=按住激活/松开复位，切换=按下翻转），Motor 只读 `SprintHeld`/`CrouchHeld`——避免切换模式的"开关状态"无处记忆与边沿时序问题；仅跳跃保留边沿标记（瞬时事件），由 `PlayerMotor.LateUpdate` 清除；
- 物理转移与输入转移统一在 Fsm.Tick 内判定，无需依赖 `PostGroundingUpdate` 钩子（保留为备选挂点）；
- **落地转移防抖**（二段跳修复，两轮）：起跳瞬间 `ForceUnground` 生效前，电机 `GroundingStatus` 可能滞后一物理帧仍为"稳定接地"旧值——若据此误判落地转回 Grounded，`GroundedState.OnEnter` 会重置 `JumpConsumed` 导致土狼窗口内二段跳。修复组合：
  1. `Fall → Grounded` 与 `UnstableGround → Grounded` 转移均增加 `!Motor.MustUnground` 条件（ForceUnground 生效期间禁止落地转移）；
  2. `Fall → UnstableGround` 转移同样增加 `!Motor.MustUnground` 条件，且**土狼跳与 PerformJump 一致调用 `ForceUnground`**——堵死"土狼跳后 Fall→UnstableGround→Grounded"链（该链会经 UnstableGround 的落地转移重置 JumpConsumed，且 PendingJumpImpulse 在 UnstableGround 中不被消费导致冲量丢失）。
- **边缘振荡修复**（悬崖边缘粘滞）：KCC 的"不稳定接地"含多种语义——陡坡（法线角 > `MaxStableSlopeAngle`，应 UnstableGround）、**悬崖边缘/落差**（`LedgeDetected`，法线≈up，应直接 Fall 下落）、**不可站立表面**（非 StableGroundLayers 对象，应 UnstableGround）。原 `Fall→UnstableGround` 仅看 `FoundAnyGround && !IsStableOnGround`，把边缘误判为滑动 → Fall⇄UnstableGround 振荡 + UnstableGround 沿 up 法线切向滑动速度为零 → 边缘粘滞。修复：`Context.IsUnstableGroundSurface`（`IsSlopeSlide`（法线角 > `IMotor.MaxStableSlopeAngle`，读 KCC 电机单一配置源）**或** `IsOnNonStableLayer`（`IMotor.GroundColliderLayer` 不在 `IMotor.StableGroundLayers`——互斥语义，KCC 接地探测已改覆盖全部碰撞层，见 §5.4））区分三者；另 **`UnstableGroundState.ApplyVelocity` = 重力切向（全量，沿坡下滑）+ 法向（微量 ×0.1，贴附）**——速度方向以切向为主：若加速度含全量法向，速度被拉向垂直，KCC 垂直 sweep 命中坡面后垂直速度投影到水平切向≈0 → 卡坡；仅切向则未接触时悬空。法向微量保证缓慢贴附又不破坏下滑。KCC `GetDirectionTangentToSurface` 是"归一化方向"工具（恒返回单位向量），误用于速度会破坏模长/方向，UnstableGround 不使用——`Fall→UnstableGround` 用 `IsUnstableGroundSurface`，`UnstableGround→Fall` 对称。边缘过渡（KCC `MaxStableDistanceFromLedge` 提前触发导致的下落前粘滞）可通过调小该电机参数（0~0.1）进一步优化。
- **KCC sweep 迭代配置**（凸曲面滑动卡坡修复）：KCC `MaxMovementIterations=5` 且 `KillVelocityWhenExceedMaxMovementIterations=true`（默认）——角色在凸曲面（球面/曲面坡）上滑动时，sweep 命中法线持续变化、迭代频繁超限 → **速度被清零** → "滑行一下停住、卡在坡上"。装配时（PlayerController）设 `MaxMovementIterations=15`、`KillVelocityWhenExceedMaxMovementIterations=false`（超限保留速度；正常移动 1-2 次迭代即完成，无副作用）。
- **移动平台跳跃 Y 轴惯性修复**：落上平台时 KCC 把平台水平分量从 BaseVelocity 分离（避免双倍），平台垂直速度由 `_attachedRigidbodyVelocity` 驱动；跳跃（ForceUnground）后 KCC 动量保持把平台全速度（含垂直）加回 BaseVelocity，但 `FallState` 消费起跳冲量时 `- Project(velocity, up)` 会丢掉垂直分量 → 升降平台跳跃丢失 Y 惯性。修复：`PerformJump` 把平台垂直速度（`IMotor.AttachedRigidbodyVelocity` 沿 up 投影）并入起跳冲量——`impulse = up × (jumpUpSpeed + 平台垂直) + 水平意图`，起跳后 Y = 起跳速度 + 平台垂直。

### 3.2 输入时序

- 输入为事件驱动（InputManager 回调写意图状态标志），与物理帧率无关；
- `Fsm.Tick` 读取意图状态做输入转移判定（跳跃按下、蹲伏按下等）。

### 3.3 KCC 回调映射（翻译器 `PlayerCharacterController`）

| KCC 回调 | 翻译器动作 |
|---|---|
| `BeforeCharacterUpdate` | （预留）帧首钩子，透传当前状态 |
| `PostGroundingUpdate` | （备选物理转移挂点；当前统一 Fsm.Tick） |
| `UpdateRotation(ref q, dt)` | 分发当前状态 `ApplyRotation`（**FPS 决议：body 保持不旋转**——视角 yaw/pitch 由 PlayerLook 在相机上即时设置，避免 KCC 渲染插值造成的视角缓冲；待有身体模型时再实现"身体朝向移动方向"转向） |
| `UpdateVelocity(ref v, dt)` | 分发当前状态 `ApplyVelocity`（读 Context 意图 + GroundingStatus） |
| `AfterCharacterUpdate` | 分发当前状态 `PostUpdate`（起身 overlap 检查等） |
| `OnGroundHit` / `OnMovementHit` | （备选）状态碰撞事件（如 UnstableGround 判定辅助） |
| `ProcessHitStabilityReport` | 默认透传（不改写） |
| `IsColliderValidForCollisions` | 忽略碰撞列表（Context 配置） |
| `OnDiscreteCollisionDetected` | 默认空 |

## 4. Context 设计（PlayerMotorContext）

| 字段 | 类型 | 说明 |
|---|---|---|
| `Motor` | `IMotor` | 电机端口（决策层唯一电机依赖；KccMotorAdapter 实现） |
| `Input` | `PlayerMotorInputState` | 输入意图（MoveInput/Jump/Sprint/Crouch/LocalMoveIntent） |
| `Profile` | `PlayerMotorProfile` | 参数配置（速度档/加速度/跳跃/重力/胶囊，ScriptableObject，对标 PlayerLookProfile） |
| `TimeSinceLastAbleToJump` | `float` | 土狼时间计时（Grounded 置 0，Airborne 递增） |
| `JumpConsumed` | `bool` | 本着陆周期跳跃已消耗（落地进入 Grounded 时重置） |
| `PendingJumpImpulse` | `Vector3` | 待应用起跳初速度（跳跃转移时写入，Airborne 首帧 ApplyVelocity 消费） |
| `AddVelocityAccumulator` | `Vector3` | 通用加力通道（受击击退等，任意状态可经 `AddVelocity` 注入） |
| `IgnoredColliders` | `List<Collider>` | 碰撞过滤（当前翻译器恒通过，待接入） |

## 5. KCC 提取与电机端口（定稿）

### 5.1 提取方案：保留独立程序集（vendor 模式）

```
Assets/Plugins/KCC/
├── KinematicCharacterController.asmdef     # 仅依赖 UnityEngine（含 Physics）
└── Core/                                   # 原样保留：namespace/代码/版权头不改
    ├── KinematicCharacterMotor.cs
    ├── ICharacterController.cs
    ├── KCCSettings.cs
    ├── KinematicCharacterSystem.cs
    ├── PhysicsMover.cs
    ├── IMoverController.cs
    └── ReadOnlyAttribute.cs
```

理由：电机为**黑盒依赖**（明确不改成熟电机），依赖方向清晰（XeptGame → KCC 单向）、可对照上游升级、风险隔离在适配层。Editor 代码（`KinematicCharacterMotorEditor`）暂不提取（可选后续）。保留每个文件的版权头；`docs/samples/` 原始副本保留作学习对照（不在编译路径）。

### 5.2 电机端口（端口-适配器，为换底层留后路）

决策层**不直接依赖 KCC 类型**，只依赖自研电机端口接口 `IMotor`；KCC 是端口的一个适配器实现。换底层时决策层不动，只替换适配器。

**原则：接口最小化**——只抽象状态类真正需要的电机能力，不把 KCC API 包成翻版；不出现任何 KCC 类型。

```csharp
/// 接地状态报告（电机无关抽象，替代 KCC GroundingStatus 的接地部分）
public readonly struct MotorGroundState
{
    public bool FoundAnyGround;      // 探测到任何地面（UnstableGround 判定用）
    public bool IsStableOnGround;    // 稳定接地（Grounded/UnstableGround 分界）
    public Vector3 GroundNormal;     // 起跳方向、斜坡速度重定向
}

/// 电机端口：决策层对底层电机的唯一依赖
public interface IMotor
{
    // —— 只读状态 ——
    MotorGroundState Ground { get; }
    Vector3 CharacterUp { get; }        // 起跳方向、速度平面投影
    Vector3 CharacterForward { get; }
    Vector3 TransientPosition { get; }  // 起身 overlap、移动平台跟随
    Quaternion TransientRotation { get; }

    // —— 操作 ——
    void SetCapsuleDimensions(float radius, float height, float yOffset);  // Crouch 进出
    void ForceUnground(float time = 0.1f);                                 // 跳跃
    bool CharacterOverlapCheck(Vector3 position, Quaternion rotation,
        float radius, float height, float yOffset,
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore);  // 起身检查
    Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal);   // 斜坡速度
}
```

注意：**速度不进接口**——KCC 为"电机回调拉取"模型（`UpdateVelocity(ref v)`），状态类求值方法直接改 `ref Vector3`（纯引擎类型）。

**对象图**：

```
KinematicCharacterMotor (KCC, 黑盒)
        ↑ 持有
KccMotorAdapter : IMotor          ← 端口实现（薄包装：接地映射 + 胶囊 + ForceUnground + overlap）
        ↑ 持有（经 Context）
PlayerCharacterController : ICharacterController  ← KCC 回调入口（电机回调 → Fsm 当前状态求值方法）
PlayerMotor FSM（决策层）→ Context（IMotor + Input + Look + Profile + 跳跃时序 + AddVelocity）
```

**状态类求值方法签名**（全部经 Context 访问 `IMotor`，零 KCC 依赖）：

```csharp
ApplyVelocity(ref Vector3 velocity, float dt)
ApplyRotation(ref Quaternion rotation, float dt)
PostUpdate(float dt)     // AfterCharacterUpdate 分发（起身 overlap 等）
```

**换底层改动面**：

| 层 | 换底层时是否要改 |
|---|---|
| 决策层（状态类 / Context / Fsm） | 不动（接口契约稳定） |
| `KccMotorAdapter` | 换实现（新底层实现 `IMotor`） |
| `PlayerCharacterController` | 小改（新底层回调映射；同为"拉取"模型则几乎不动） |

**边界纪律**：接口只按"状态类需要"生长，不在前期臆造 KCC 之外的 API。

### 5.3 胶囊配置源（定稿：消除双配置歧义）

- **KCC 电机胶囊不可配**：`KinematicCharacterMotor.ValidateData()`（Awake/OnValidate）用**内部硬编码值** `SetCapsuleDimensions(0.5f, 2f, 1f)` 强制覆盖胶囊（源码 570 行）——场景 CapsuleCollider 与 KCC 电机上的胶囊配置会被重置，不可作为配置入口；
- **唯一配置源 = `PlayerMotorProfile` 胶囊区**（`capsuleRadius`/`standingHeight`/`standingYOffset`/`crouchedHeight`/`crouchedYOffset`）；装配时（PlayerController）与 `GroundedState.OnEnter` 均按 Profile 设置站立胶囊，覆盖电机强制值；
- **约定**：请勿配置场景 CapsuleCollider 或 KCC 电机的胶囊字段（会被 ValidateData 覆盖）；
- **yOffset 语义**：`SetCapsuleDimensions` 的 `yOffset` = **胶囊中心 Y（相对角色 transform）**，非底部位置。默认站立 1（胶囊范围 0~2，脚在底部）、蹲伏 0.5（范围 0~1，脚不动头顶下降）；若要"底部对齐"，值 = 高度 × 0.5。

### 5.4 可站立层互斥语义（定稿，含 KCC 源码修改）

- **语义**：`StableGroundLayers` = 可站立（Grounded）；**非 StableGroundLayers**（在 `CollidableLayers` 内）= 不可站立（UnstableGround）。二者**互斥**，零额外配置；
- **KCC 源码修改点**（vendor 偏离，升级时注意）：`KinematicCharacterMotor.CharacterGroundSweep` 接地探测层由 `CollidableLayers & StableGroundLayers` 改为 `CollidableLayers`（已加 `[XeptGame 修改]` 注释标记）——否则非 StableGroundLayers 对象被 KCC 接地探测忽略（`FoundAnyGround=false`、无吸附、法线占位 up），角色无法正常落地；
- **修改后的行为**：所有碰撞层正常探测/吸附/法线（角色自由落体 + 精准落地，与可站立地面一致）；`IsStableOnGround` 仍按法线角判定（KCC 语义），"不可站立"由业务层判定覆盖；
- **判定**：`Context.IsOnNonStableLayer`（`IMotor.GroundColliderLayer` 不在 `IMotor.StableGroundLayers`）→ `IsUnstableGroundSurface = IsSlopeSlide || IsOnNonStableLayer`；
- **转移**：`Fall→Grounded` 与 `UnstableGround→Grounded` 均排除 `IsOnNonStableLayer`（KCC 法线角判稳定，但语义不可站立）；`GroundedState` 检测 `IsOnNonStableLayer` → Airborne（Fall 中转）→ UnstableGround；
- **UnstableGround 法线**：`GroundingStatus.GroundNormal` 即真实法线（接地探测已覆盖全部碰撞层），无需主动探测；
- **权衡记录**：早期方案（`slidableLayers` 并入 StableGroundLayers）避免改 vendor 但引入双配置认知负担，且语义不纯净（StableGroundLayers 内混入"不可站立"层）；互斥语义需改 KCC 一行，换取语义纯净 + 零配置，值得。

## 6. 扩展与待办

- **移动平台示例**（已实现 `Assets/Scripts/Player/Platforms/DotweenPlatformMover.cs`）：DOTween 数值驱动（插值 `_t` 变量，不碰 transform）+ PhysicsMover 执行（`[NonSerialized] MoverController` 由组件 Awake 注入）。待办：场景装配验证（站立跟随/跳跃保留动量/边缘滑落）；
- **Roll（蹲伏翻滚）**：Airborne 或独立动作状态（用户提及；蹲伏翻滚不与冲刺冲突，属动作状态，后续设计）；
- 双跳 / 墙跳（KCC 教程 10 有现成逻辑可参考）；
- 攀爬 / 游泳（KCC 教程 13/14）；
- 跳跃缓冲（PreGroundingGrace）实现（参数已规划，未落地）；
- 场景验证（3C 开发场景 + Player 预制体 + KCC 电机挂载）。

## 7. 参考

- KCC 源码与教程：`docs/samples/KinematicCharacterController/`（Core/、Walkthrough/10- Multiple movement states setup 等）
- XeptKit FSM 约定：`Assets/Plugins/XeptKit/FSM/`
- 输入意图层：`Assets/Scripts/Player/Input/`（已实现）
- 视角层：`Assets/Scripts/Player/Look/`（已实现）
