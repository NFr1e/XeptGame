using System;
using System.Collections.Generic;
using UnityEngine;
using XeptKit.Input;

namespace XeptGame
{
    /// <summary>
    /// 暂停原因（Time_Authority_Design.md §5.3）：<b>值域按需生长</b>——谁需要停就加一个值，
    /// 不做"暂停等级/优先级"（原因之间无强弱，任一存在即暂停）。
    /// <list type="bullet">
    /// <item>带原因是"请求口"能同时服务多个来源（背包界面、将来的暂停菜单）的前提：
    /// 后开的界面释放自己的原因时，不会把先开的那一个也顺带恢复；</item>
    /// <item>原因入集合 → <see cref="ITimeControl.Request"/> / <see cref="ITimeControl.Release"/> 天然幂等。</item>
    /// </list>
    /// </summary>
    public enum PauseReason
    {
        /// <summary>暂停菜单（将来接线；当前无触发者）。</summary>
        Menu = 0,

        /// <summary>背包界面打开。</summary>
        Backpack = 1,
    }

    /// <summary>
    /// 暂停请求口（<b>窄口</b>，Time_Authority_Design.md §5.1）：<b>谁都能报，谁都改不了时间</b>——
    /// 调用方只表达"我需要停"与原因，执行一律由 App 域的时间权威落地。
    /// 与 <see cref="PauseReason"/> 成对调用是调用方责任（不变量 7：异常路径也必须 release）。
    /// </summary>
    public interface ITimeControl
    {
        /// <summary>请求暂停（幂等）。返回是否引起状态变化（重复请求返回 false）。</summary>
        bool Request(PauseReason reason);

        /// <summary>释放暂停请求（幂等）。返回是否引起状态变化（未请求过 / 重复释放返回 false）。</summary>
        bool Release(PauseReason reason);
    }

    /// <summary>
    /// 暂停的"应用侧效果"出口：把"该不该暂停"翻译成引擎与世界侧的动作。
    /// <b>抽成接口的唯一理由是编辑期可测</b>——真实实现要写 <c>Time.timeScale</c> 与全局输入层，
    /// EditMode 里不能这么做（会污染编辑器状态）；测试注入假实现即可验证原因集合逻辑。
    /// </summary>
    public interface IPauseEffect
    {
        /// <summary>应用暂停/恢复（只在状态真正变化时被调用一次）。</summary>
        void SetPaused(bool paused);
    }

    /// <summary>
    /// 引擎侧暂停效果（<b>全工程唯一写 <c>Time.timeScale</c> 的地方</b>，Time_Authority_Design.md 不变量 1）：
    /// <list type="bullet">
    /// <item><b>写 <c>Time.timeScale</c></b>：物理、动画、粒子、KCC、<c>Time.time</c>、DOTween 默认轨道一并停
    /// ——这是"整个世界停住"的引擎侧落点（设计 §2 已澄清：它不是暂停的定义，只是执行手段）；</item>
    /// <item><b>切 Gameplay 输入层</b>：暂停时禁用 <see cref="GameplayInputLayer"/>（<see cref="MenuInputLayer"/>
    /// 的阻断语义随层生效）。放在同一出口是为了让两者<b>不可能漂移</b>——"暂停了但还能操作角色"是这类改动最常见的漏。</item>
    /// </list>
    /// <b>有意不在此处做的事</b>（V1 范围收窄，见设计 §11）：音频 <c>AudioListener.pause</c>、
    /// 域时基乘数、<c>PausedState</c> 接线。三者都是"知道该做、现在不做"。
    /// </summary>
    public sealed class EnginePauseEffect : IPauseEffect
    {
        /// <summary>游戏进行中的引擎时基（也是恢复时写入的值；timeScale 的常规值就是 1）。</summary>
        public const float PlayingScale = 1f;

        private readonly IInputManager _input;

        /// <param name="input">输入分发器（可为 null = 不切层；仅供无输入环境的装配/测试）。</param>
        public EnginePauseEffect(IInputManager input) => _input = input;

        public void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : PlayingScale;
            _input?.SetLayerActive<GameplayInputLayer>(!paused);
        }
    }

    /// <summary>
    /// 时间权威（App 域服务；Time_Authority_Design.md T3）：持<b>暂停原因集合</b>，集合非空即暂停。
    /// <list type="bullet">
    /// <item><b>为什么原因与执行同处一地</b>：<c>Time.timeScale</c> 只能有一个写者；把"为什么停"与"什么时候真的停"
    /// 分到两个对象，必然出现"集合空了但 scale 还是 0"（或反过来）的漂移；</item>
    /// <item><b>为什么不放 Game / 会话域</b>：请求来自多个域（会话域的背包、Game 域的菜单），
    /// 决策点若落在内层，外层就得"翻进内层"——违反"外层不依赖内层"（GameplaySession_Domain_Design.md §3.1）；</item>
    /// <item><b>边缘触发</b>：只在"暂停 ⇄ 恢复"真正翻转时调一次效果出口（重复 Request 不会重复写 timeScale）；</item>
    /// <item><b>无领域细节</b>：本类不认识背包、也不认识菜单，只认原因集合。</item>
    /// </list>
    /// </summary>
    public sealed class TimeAuthority : ITimeControl
    {
        private readonly HashSet<PauseReason> _reasons = new();
        private readonly IPauseEffect _effect;

        /// <summary>最近一次真正下发给效果出口的值（边缘触发用；<c>false</c> = 尚未暂停过）。</summary>
        private bool _applied;

        /// <param name="effect">应用侧效果出口（组合根注入 <see cref="EnginePauseEffect"/>；测试注入假实现）。</param>
        public TimeAuthority(IPauseEffect effect)
            => _effect = effect ?? throw new ArgumentNullException(nameof(effect));

        /// <summary>当前是否暂停（原因集合非空）。</summary>
        public bool IsPaused => _reasons.Count > 0;

        /// <summary>当前登记的原因数量（调试/测试读；正常业务只需 <see cref="IsPaused"/>）。</summary>
        public int ReasonCount => _reasons.Count;

        /// <summary>某原因当前是否被登记。</summary>
        public bool IsRequested(PauseReason reason) => _reasons.Contains(reason);

        public bool Request(PauseReason reason)
        {
            if (!_reasons.Add(reason))
            {
                return false;
            }

            Apply();
            return true;
        }

        public bool Release(PauseReason reason)
        {
            if (!_reasons.Remove(reason))
            {
                return false;
            }

            Apply();
            return true;
        }

        /// <summary>
        /// 强制恢复（清空全部原因并应用一次）——<b>组合根收尾专用</b>：
        /// 应用退出时若仍停在暂停态，全局 <c>Time.timeScale</c> 会留在 0（编辑器里表现为"下次运行不动"）。
        /// 业务代码不得调用（会静默吞掉别人的暂停请求）。
        /// </summary>
        public void ForceResume()
        {
            _reasons.Clear();
            _applied = false;
            _effect.SetPaused(false);
        }

        /// <summary>边缘触发：只在"暂停 ⇄ 恢复"真正翻转时下发一次（加第二个原因不会重复写 <c>timeScale</c>）。</summary>
        private void Apply()
        {
            var paused = IsPaused;
            if (paused == _applied)
            {
                return;
            }

            _applied = paused;
            _effect.SetPaused(paused);
        }
    }
}
