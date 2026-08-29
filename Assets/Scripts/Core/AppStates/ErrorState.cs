using XeptKit.Core;

namespace XeptGame
{
    /// <summary>
    /// 启动失败呈现：读取 <see cref="AppContext.LastError"/> 展示失败原因，提供重试/放弃路径。
    /// 当前范围：仅覆盖启动失败（顶层状态）；运行期致命错误形态留待真实需求（见设计决议 §6）。
    /// UI 接线由业务细化；当前以日志兜底呈现。
    /// </summary>
    public sealed class ErrorState : AppStateBase
    {
        public override void OnEnter()
        {
            Log.Error($"[AppFSM] 应用启动失败：{App.LastError?.Message ?? "未知错误"}");
        }
    }
}
