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
        /// 本地空间移动意图（**body 局部坐标**，y=0 的单位方向或零向量）
        /// = MoveInput 经"相对 body 的视角 yaw"旋转（PlayerLook 写入）。
        /// 参考系 = body（见设计决议 §2.2）：body 被移动平台旋转带动时，
        /// 消费方经 Context.WorldMoveIntent（body 局部 → 世界）得到世界方向——
        /// 视角与移动在同一参考系下天然一致，无需"平台脱离并入基准"。
        /// </summary>
        public Vector3 LocalMoveIntent;

        /// <summary>
        /// PlayerController.LateUpdate 调用：清除边沿标记（当前仅跳跃；Sprint/Crouch 为意图状态无需清除）。
        /// 命名 EndFrame 而非 LateUpdate：本类为纯 C# 状态类（非 MonoBehaviour），
        /// 避免与 Unity 生命周期方法名混淆。
        /// </summary>
        public void EndFrame()
        {
            JumpIntent = false;
        }
    }
}
