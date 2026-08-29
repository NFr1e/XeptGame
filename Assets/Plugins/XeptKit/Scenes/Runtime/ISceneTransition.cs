using Cysharp.Threading.Tasks;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景过渡策略接口。实现此接口以自定义场景切换时的过渡效果
    /// （如淡入淡出、滑动、Loading 画面等）。
    /// </summary>
    /// <remarks>
    /// 过渡不可取消：无令牌参数、不响应切换令牌，一旦开始必完整走完（遮住 → 揭开）；
    /// 切换的取消只作用于过渡之后的资源操作阶段。
    /// 实现约定（失败自清理）：EnterAsync / ExitAsync 抛异常时须自行清理残留
    /// （如销毁半成品遮罩对象），管理器不代为处理。
    /// 典型调用流程：
    /// <code>
    /// await transition.EnterAsync();   // 遮住画面
    /// // ... 加载/卸载场景 ...
    /// await transition.ExitAsync();    // 揭开画面
    /// </code>
    /// </remarks>
    public interface ISceneTransition
    {
        /// <summary>播放遮罩进入动画（如黑场淡入）。播放完成后画面应被完全遮挡。</summary>
        UniTask EnterAsync();

        /// <summary>播放遮罩退出动画（如黑场淡出）。播放完成后画面应完全恢复。</summary>
        UniTask ExitAsync();

        /// <summary>当前过渡进度，范围 [0, 1]。</summary>
        float Progress { get; }
    }
}
