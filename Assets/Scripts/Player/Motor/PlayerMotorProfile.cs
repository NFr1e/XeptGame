using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// PlayerMotor 默认参数配表（速度档/加速度/跳跃/重力/胶囊尺寸）。
    /// 对标 PlayerLookProfile；运行时默认配置见 <see cref="Default"/>。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.PlayerMotorProfileMenuName,
        fileName = XeptGameConsts.Editor.PlayerMotorProfileFileName,
        order = XeptGameConsts.Editor.PlayerMotorProfileOrder)]
    public class PlayerMotorProfile : ScriptableObject
    {
        [Header("胶囊（唯一配置源）")]
        [Tooltip("胶囊半径。注意：KinematicCharacterMotor 的 ValidateData 会用内部硬编码值强制胶囊，请勿配置场景 CapsuleCollider 或 KCC 电机上的胶囊，一律在本配表设置")]
        public float capsuleRadius = 0.5f;

        [Tooltip("站立高度")]
        public float standingHeight = 2f;

        [Tooltip("站立时胶囊中心 Y 偏移（相对角色 transform；默认 1 = 脚在底部、中心居中，胶囊范围 0~2）")]
        public float standingYOffset = 1f;

        [Tooltip("蹲伏高度")]
        public float crouchedHeight = 1f;

        [Tooltip("蹲伏时胶囊中心 Y 偏移（相对角色 transform；默认 0.5 = 脚不动、头顶下降，胶囊范围 0~1）")]
        public float crouchedYOffset = 0.5f;

        [Header("地面移动")]
        [Tooltip("行走速度")]
        public float walkSpeed = 4f;

        [Tooltip("冲刺速度")]
        public float sprintSpeed = 8f;

        [Tooltip("蹲伏移动速度")]
        public float crouchSpeed = 2f;

        [Tooltip("地面速度平滑（越大越快到达目标速度）")]
        public float stableMovementSharpness = 15f;

        [Tooltip("上下坡速度修正强度（0~1）：重力切向对地面目标速度的影响——下坡加速、上坡减速（B1 方案，见设计决议 §2.2）。0=关闭（坡面速度恒等于速度档，KCC 默认）；平地无影响")]
        [Range(0f, 1f)]
        public float slopeGravityInfluence = 0.5f;

        [Tooltip("上坡最低速度比例（0~1，相对当前速度档）：避免陡坡上坡速度趋零卡住")]
        [Range(0f, 1f)]
        public float minSlopeUpSpeedFactor = 0.3f;

        [Header("空中移动")]
        [Tooltip("空中加速度（空中转向/启动的速度基准；与 airControl 乘积为有效输入加速度）")]
        public float airAccelerationSpeed = 15f;

        [Tooltip("空气控制系数（0~1，UE AirControl 语义）：空中转向分量（垂直于当前水平速度的输入）弱化比例。0=空中纯惯性不可转向；1=空中可全向加速。默认 0.35 手感柔和")]
        [Range(0f, 1f)]
        public float airControl = 0.35f;

        [Tooltip("空气拖拽（越大减速越快）")]
        public float drag = 0.1f;

        [Tooltip("重力")]
        public Vector3 gravity = new Vector3(0f, -30f, 0f);

        [Header("跳跃")]
        [Tooltip("起跳速度（沿起跳方向）")]
        public float jumpUpSpeed = 10f;

        [Tooltip("土狼时间（离地后仍可起跳的窗口）")]
        public float jumpPostGroundingGraceTime = 0.2f;

        [Tooltip("不稳定地面滑动时允许起跳")]
        public bool allowJumpingWhenUnstableGround = true;

        [Header("滑动受限运动")]
        [Tooltip("滑动输入控制系数（0~1）：世界空间移动意图（Context.WorldMoveIntent）沿表面切向的附加加速乘此系数弱化。0=滑动不可操控（纯重力下滑）；1=全量输入加速。默认 0.5")]
        [Range(0f, 1f)]
        public float slidingControl = 0.5f;

        [Tooltip("滑动输入加速度（每帧输入带来的速度增量；小于地面/空中加速度体现受限感）。注意：受限运动是合成而非钳制——不设目标速度，保留原有速度动量")]
        public float slidingAcceleration = 8f;

        [Tooltip("true=长按冲刺（按住激活/松开复位）；false=点按切换")]
        public bool pressToSprint = true;

        [Tooltip("true=长按蹲伏（按住激活/松开复位）；false=点按切换")]
        public bool pressToCrouch = true;

        /// <summary>运行时默认配置（未挂资产时使用）。非资产实例，不可在资源库中编辑。</summary>
        public static PlayerMotorProfile Default
        {
            get
            {
                var profile = CreateInstance<PlayerMotorProfile>();
                profile.name = "PlayerMotorProfile_Default";
                return profile;
            }
        }
    }
}
