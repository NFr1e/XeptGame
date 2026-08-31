using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 视角输入（原始）：本帧 Look 增量（已做摇杆死区预处理）。
    /// 注意：这里只承载"输入"——视角角度（Yaw/Pitch）是累积状态，
    /// 由 <see cref="PlayerLookController"/> 持有与维护，不属于输入状态（见 Look 域设计决议）。
    /// 增量输入为消费即清语义：PlayerLook 每帧读取后清零，避免静止时残留重复应用。
    /// </summary>
    public class PlayerLookInputState
    {
        /// <summary>本帧视角增量（像素/摇杆单位；灵敏度由视角控制器应用）。</summary>
        public Vector2 Delta;
    }
}
