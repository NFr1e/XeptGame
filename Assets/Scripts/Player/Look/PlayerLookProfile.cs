using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// PlayerLook默认参数配表
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.PlayerLookProfileMenuName,
        fileName = XeptGameConsts.Editor.PlayerLookProfileFileName, 
        order = XeptGameConsts.Editor.PlayerLookProfileOrder)]
    public class PlayerLookProfile : ScriptableObject
    {
        [Header("灵敏度（度/像素 或 度/摇杆单位）")]
        [Tooltip("水平灵敏度（yaw）")]
        public float sensitivityX = 0.15f;

        [Tooltip("垂直灵敏度（pitch）；独立于水平灵敏度，可分别调校")]
        public float sensitivityY = 0.12f;

        [Header("俯仰限制（度）")]
        [Tooltip("俯仰角下限")]
        public float minPitch = -89f;

        [Tooltip("俯仰角上限")]
        public float maxPitch = 89f;

        [Header("反转")]
        [Tooltip("反转 Y 轴（鼠标/右摇杆上推 = 低头）")]
        public bool invertY;

        [Header("摇杆死区")]
        [Tooltip("右摇杆输入模长低于该值时视为 0（消除漂移；仅对手柄生效，不影响鼠标）")]
        [Range(0f, 0.5f)]
        public float stickDeadzone = 0.15f;

        /// <summary>运行时默认配置（未挂资产时使用）。非资产实例，不可在资源库中编辑。</summary>
        public static PlayerLookProfile Default
        {
            get
            {
                var profile = CreateInstance<PlayerLookProfile>();
                profile.name = "PlayerLookProfile_Default";
                return profile;
            }
        }
    }
}
