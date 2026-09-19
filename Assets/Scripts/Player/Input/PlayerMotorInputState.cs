using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 运动输入意图（PlayerInputController 写入，PlayerMotor FSM 读取）。
    /// Sprint/Crouch 已在输入层折叠为**意图状态**（是否想冲刺/想蹲伏）——长按=按住激活/松开终止、
    /// 点按=按下翻转，**输入模式的差异到此为止**：决策层只识别意图，不识别输入状态。
    /// 不再使用"按下边沿 + 帧末清除"的双轨表达——Motor 只读意图，无切换保持/时序问题。
    /// 仅跳跃保留边沿标记（瞬时事件语义：一次按下只触发一次跳跃），由 PlayerController.LateUpdate 清除。
    /// </summary>
    public class PlayerMotorInputState
    {
        /// <summary>
        /// 移动输入（原始，尚未旋转到世界空间）
        /// </summary>
        public Vector2 MoveInput;
        /// <summary>
        /// 跳跃按下（边沿，PlayerController.LateUpdate 清除）
        /// </summary>
        public bool JumpIntent;
        /// <summary>
        /// 冲刺**意图**激活（长按/点按模式的差异已在输入层折叠，本层只表达意图）
        /// </summary>
        public bool SprintIntent;
        /// <summary>
        /// 蹲伏**意图**激活（同上：意图终止 = "不想蹲伏"——滑铲退出条件之一，§2.5）
        /// </summary>
        public bool CrouchIntent;
        /// <summary>
        /// 滑铲**请求**（**瞬时意图**，边沿）：蹲伏意图由 false→true 的那一帧置位，帧末清除。
        /// 与 <see cref="JumpIntent"/> 同类——"冲刺中按蹲"是一个动作/事件，不是持续条件；
        /// 若改用持续蹲伏意图判定滑铲进入，会出现"蹲伏→奔跑→到速→滑铲→回蹲→又奔跑"的自激循环（§2.5）。
        /// </summary>
        public bool SlideIntent;
        /// <summary>
        /// 本地空间移动意图（**body 局部坐标**，y=0 的单位方向或零向量）
        /// = MoveInput 经"相对 body 的视角 yaw"旋转（PlayerLook 写入）。
        /// 参考系 = body（见设计决议 §2.2）：body 被移动平台旋转带动时，
        /// 消费方经 Context.WorldMoveIntent（body 局部 → 世界）得到世界方向——
        /// 视角与移动在同一参考系下天然一致，无需"平台脱离并入基准"。
        /// </summary>
        public Vector3 LocalMoveIntent;

        /// <summary>
        /// 前向移动意图（**冲刺方向准入**）：原始 <see cref="MoveInput"/> 的 Y 轴为**视图相对**前后
        /// （+1 = 正前、0 = 纯侧向、−1 = 正后）。中性带 +0.1 吸收摇杆抖动与相邻键同按，
        /// 避免 Sprint↔Walk 在临界处抖动。
        /// 轴约定留在**意图层**（决策层只读这个语义判定，不认识轴）。
        /// 用途：**只有前半球可冲刺**（含前斜向 x≠0、y&gt;0）；**纯侧向（y≈0）与后向（y&lt;0）均降级 Walk**
        /// （用户决议，取代早期"后半球禁止、侧向允许"版本）——见 <see cref="PlayerMotorContext.CanSprint"/>。
        /// </summary>
        public bool MoveForwardIntent => MoveInput.y > 0.1f;

        /// <summary>
        /// PlayerController.LateUpdate 调用：清除边沿标记（**瞬时意图**：跳跃、滑铲请求；Sprint/Crouch 为持续意图状态，无需清除）。
        /// 命名 EndFrame 而非 LateUpdate：本类为纯 C# 状态类（非 MonoBehaviour），
        /// 避免与 Unity 生命周期方法名混淆。
        /// </summary>
        public void EndFrame()
        {
            JumpIntent = false;
            SlideIntent = false;
        }
    }
}
