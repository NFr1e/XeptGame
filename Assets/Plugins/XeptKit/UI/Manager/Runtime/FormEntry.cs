using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 表单唯一身份键（数据层资产）。业务以 FormEntry 资产打开/关闭表单，杜绝硬编码字符串 ID。
    /// 打开时经 <see cref="Group"/> 路由到对应组，组行为（单例/焦点/模态/缓存等）由组配置决定。
    /// </summary>
    [CreateAssetMenu(menuName = "XeptKit/UI Form Entry", fileName = "Form Entry")]
    public sealed class FormEntry : ScriptableObject
    {
        /// <summary>面板预制体（直接引用，v1 加载决议；异步/热更实现经 <see cref="IFormLoader"/> seam 接入）。</summary>
        public GameObject Prefab;

        /// <summary>所属组（必填，打开时 <c>Guard.NotNullObject</c> 校验）。</summary>
        public UIGroupConfig Group;

        /// <summary>自动关闭时长（秒）；0 = 不自动关闭。计时取消语义由框架管理（中止开启/关闭/清场时取消）。</summary>
        public float AutoCloseSeconds;
    }
}
