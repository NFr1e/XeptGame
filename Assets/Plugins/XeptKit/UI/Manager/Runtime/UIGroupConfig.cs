using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 组行为配置（数据层资产，不持有运行时状态）。组内表单的通用行为由此决定。
    /// </summary>
    [CreateAssetMenu(menuName = "XeptKit/UI Group Config", fileName = "UI Group Config")]
    public sealed class UIGroupConfig : ScriptableObject
    {
        /// <summary>视觉序：高盖低。跨组遮挡排序键（跨组仲裁）。</summary>
        public int Depth;

        /// <summary>单例：同 FormEntry 同时最多一个存活表单（重复 Open 置顶/幂等/排队重开）。</summary>
        public bool Singleton;

        /// <summary>参与焦点（焦点链成员）：false = 永不持有焦点（HUD/toast/tooltip）。重定义原「参与栈」。</summary>
        public bool ParticipatesInFocus;

        /// <summary>模态：阻断其下输入（输入可达边界，模态为界）。</summary>
        public bool Modal;

        /// <summary>被遮挡时是否同时暂停逻辑（OnCover + OnPause）；false（默认）= 仅 OnCover，逻辑继续。</summary>
        public bool PauseWhenCovered;

        /// <summary>关闭时休眠复用（缓存）；false（默认）= 销毁。状态重置责任在每次重跑的 OnOpenAsync。</summary>
        public bool CacheForms;

        /// <summary>休眠池容量上限（CacheForms 时生效，按每个 FormEntry 计）：0 = 无上限。入池超限即销毁（防无界增长）。</summary>
        public int CacheCapacity;
    }
}
