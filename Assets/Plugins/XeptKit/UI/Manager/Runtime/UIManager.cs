using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using XeptKit.Event;
using Object = UnityEngine.Object;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// <see cref="IUIManager"/> 默认实现：请求分发 + 全局仲裁（视觉序/焦点链/输入可达/遮挡）。
    /// 构造注入事件总线（领域事件广播）；加载经 <see cref="IFormLoader"/>（默认 <see cref="DirectFormLoader"/>）；
    /// UI 上下文（组根挂载父级 + UI 相机）经 <see cref="SetUIContext"/> 推送（推模式，§5.3；未推送时降级：不建组根、跳过相机注入）。
    /// Group Root 模型：每组建独立 RectTransform 子节点（按 Depth 排 sibling 序），表单挂本组根下。
    /// 主线程 only、无锁。
    /// </summary>
    public sealed class UIManager : IUIManager, IUISceneContext
    {
        private readonly IEventBus _eventBus;
        private readonly IFormLoader _loader;

        /// <summary>组映射（按 UIGroupConfig 懒建，对齐「无注册表、运行时惰性解析」）。</summary>
        private readonly Dictionary<UIGroupConfig, UIGroup> _groups = new();

        /// <summary>组根节点映射（Group Root 模型：挂 canvasRoot 下，sibling 序 = Depth 序）。</summary>
        private readonly Dictionary<UIGroupConfig, RectTransform> _groupRoots = new();

        /// <summary>全局焦点顶（仲裁派生结果缓存，非权威存储——每次结构变更重算）。</summary>
        private FormHandle _focusTop;

        private Transform _canvasRoot;   // 经 SetUIContext 推送（推模式，可 null——降级：不建组根）
        private Camera _uiCamera;        // 经 SetUIContext 推送（推模式，可 null——降级：跳过相机注入）

        /// <inheritdoc />
        public FormHandle CurrentTop => _focusTop;

        /// <summary>
        /// 构造 UIManager。
        /// </summary>
        /// <param name="eventBus">用于广播领域事件（FormOpened/FormClosed/FocusChanged）的事件总线（构造注入，不静态依赖门面）。</param>
        /// <param name="loader">表单加载实现（可选，默认 <see cref="DirectFormLoader"/>）。</param>
        /// <remarks>UI 上下文（组根挂载父级 + UI 相机）不经构造传入，经 <see cref="SetUIContext"/> 推送（推模式，§5.3）；未推送时降级运行。</remarks>
        public UIManager(IEventBus eventBus, IFormLoader loader = null)
        {
            Guard.NotNull(eventBus, nameof(eventBus));
            _eventBus = eventBus;
            _loader = loader ?? new DirectFormLoader();
        }

        /// <summary>
        /// 推送 UI 上下文（推模式，纯显式接线）：设置组根挂载父级与 UI 相机，可随时调用（场景加载后经 <see cref="UIComponent"/> 推送）。
        /// **上下文切换契约（修订）**：canvasRoot 引用变化 = 旧组根**整体迁移**到新 root——表单随组根存活，
        /// 不销毁（适配"多 UI 世界共存、上下文切换"场景，如菜单 canvas → HUD canvas；迁移也消解了
        /// "表单先于上下文推送打开"的竞态：先挂旧根的组根随迁移存活）；已被销毁的组根（假 null，
        /// 场景卸载路径）直接丢弃；**新 root 为 null（上下文清除/降级）= 旧 UI 世界作废** → Clear + 销毁组根（原语义）。
        /// 相机单独变化不清场，但**重注入全部存活表单**（链上/关闭中/休眠——相机变更刷新契约：推送相机持续生效，见 <see cref="ReapplyUICameraToAll"/>）。
        /// 未推送时降级运行：组根不建、表单不重挂父级、相机注入跳过。
        /// 边界：**持久 UI 根（DontDestroyOnLoad）+ 场景重载**时根引用未变、不会自动清场——该场景由组合根在场景卸载时显式 <c>Clear()</c>。
        /// </summary>
        public void SetUIContext(Transform canvasRoot, Camera uiCamera)
        {
            bool rootChanged = !ReferenceEquals(_canvasRoot, canvasRoot);

            if (rootChanged && canvasRoot != null)
            {
                ReparentGroupRoots(canvasRoot); // 修订：活根整体迁移，表单存活
            }
            else if (rootChanged) // canvasRoot == null：上下文清除/降级 → 旧世界作废（无迁移目标）
            {
                Clear();
                DestroyGroupRoots();
            }

            bool cameraChanged = !ReferenceEquals(_uiCamera, uiCamera);
            _canvasRoot = canvasRoot;
            _uiCamera = uiCamera;

            if (cameraChanged && _uiCamera != null)
            {
                ReapplyUICameraToAll(); // 相机变更刷新：已存活表单（含休眠）重注入新相机；变更为 null = 降级，不清除已注入
            }
        }

        /// <summary>
        /// 组根整体迁移到新 root（上下文切换修订语义）：存活组根重挂（表单随组根存活）；
        /// **已被销毁的组根（假 null，场景卸载路径）丢弃并从字典移除**——其表单随销毁，迁移无从谈起；
        /// 残留假 null 会导致后续 <see cref="SortGroupRoots"/> 触碰已销毁对象（MissingReferenceException）。
        /// 迁移后重排 sibling 序（SetParent 追加到新根末尾，字典迭代序 ≠ Depth 序）。
        /// </summary>
        private void ReparentGroupRoots(Transform newRoot)
        {
            List<UIGroupConfig> stale = null;

            foreach (var kv in _groupRoots)
            {
                var root = kv.Value;
                if (root == null)
                {
                    (stale ??= new List<UIGroupConfig>()).Add(kv.Key); // 假 null：收集待清理（不可在遍历中改字典）
                    continue;
                }

                root.SetParent(newRoot, false);
            }

            if (stale != null)
            {
                foreach (var key in stale)
                {
                    _groupRoots.Remove(key);
                }
            }

            SortGroupRoots();
        }

        /// <summary>销毁全部组根（上下文切换「旧世界作废」：旧组根属旧 canvasRoot，销毁并清缓存）。</summary>
        private void DestroyGroupRoots()
        {
            foreach (var root in _groupRoots.Values)
            {
                if (root != null)
                {
                    Object.Destroy(root.gameObject);
                }
            }

            _groupRoots.Clear();
        }

        /// <inheritdoc />
        public event Action<FormEntry, Exception> FormException;

        // ===============================================================
        // IUIManager
        // ===============================================================

        /// <inheritdoc />
        public UniTask<FormHandle> OpenAsync(
            FormEntry entry, object args = null, CancellationToken cancellationToken = default)
        {
            Guard.NotNullObject(entry, nameof(entry));
            Guard.NotNullObject(entry.Group, "entry.Group");
            return GetGroup(entry.Group).OpenAsync(entry, args, cancellationToken);
        }

        /// <inheritdoc />
        public void Close(FormHandle handle) => RouteToGroup(handle)?.Close(handle);

        /// <inheritdoc />
        public void Close(FormEntry entry)
        {
            Guard.NotNullObject(entry, nameof(entry));
            if (entry.Group == null || !_groups.TryGetValue(entry.Group, out var group))
            {
                return;
            }

            var handle = group.GetOpened(entry);
            if (handle.Entry != null)
            {
                group.Close(handle);
            }
        }

        /// <inheritdoc />
        public void CloseTop()
        {
            if (_focusTop.Entry != null)
            {
                Close(_focusTop);
            }
        }

        /// <inheritdoc />
        public UniTask CloseAsync(FormHandle handle)
            => RouteToGroup(handle) is UIGroup group ? group.CloseAsync(handle) : UniTask.CompletedTask;

        /// <inheritdoc />
        public FormHandle GetOpened(FormEntry entry)
        {
            Guard.NotNullObject(entry, nameof(entry));
            return entry.Group != null && _groups.TryGetValue(entry.Group, out var group)
                ? group.GetOpened(entry)
                : default;
        }

        /// <inheritdoc />
        public T GetFormLogic<T>(FormEntry entry) where T : FormLogicBase
        {
            Guard.NotNullObject(entry, nameof(entry));
            return entry.Group != null && _groups.TryGetValue(entry.Group, out var group)
                ? group.GetFormLogic<T>(entry)
                : null;
        }

        /// <inheritdoc />
        public bool IsValid(FormHandle handle)
            => handle.Entry != null && handle.Entry.Group != null
               && _groups.TryGetValue(handle.Entry.Group, out var group)
               && group.IsValid(handle);

        /// <inheritdoc />
        public void Clear()
        {
            foreach (var group in _groups.Values)
            {
                group.Clear();
            }

            _groups.Clear();
            _focusTop = default;
            // 组根节点保留（结构物，随场景销毁；下次打开复用）
        }

        // ===============================================================
        // 内部（UIGroup 回调 / 组合根装配）
        // ===============================================================

        internal IFormLoader Loader => _loader;

        /// <summary>结构变更上报：触发跨组仲裁重算。</summary>
        internal void NotifyStructureChanged() => Arbitrate();

        /// <summary>打开完成后刷新遮挡通知（born-covered 情形：实例转 Opened 时补发 OnCover）。</summary>
        internal void RefreshCoverStates()
        {
            var groups = GetGroupsSortedByDepth();
            var instances = CollectInstances(groups);
            ApplyCovered(instances);
        }

        internal void PublishFormOpened(FormHandle handle) => _eventBus.Publish(new FormOpenedEvent(handle));

        internal void PublishFormClosed(FormHandle handle) => _eventBus.Publish(new FormClosedEvent(handle));

        internal void RaiseFormException(FormEntry entry, Exception ex)
        {
            try
            {
                FormException?.Invoke(entry, ex);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx); // 钩子自身防御：不打断调用方流程
            }
        }

        /// <summary>相机注入管道（管理器决策、UIForm 应用）：将已推送的相机交给表单；未推送相机（降级）时短路跳过（文档约定，非错误）。</summary>
        internal void ApplyUICameraTo(UIForm view)
        {
            if (_uiCamera == null)
            {
                return; // 降级：未推送相机——跳过注入（静默，文档约定；避免降级场景每次打开刷警告）
            }

            view.ApplyUICamera(_uiCamera);
        }

        /// <summary>相机变更刷新（SetUIContext 相机变更契约）：对全部组内实例（链上/关闭中/休眠）重注入当前相机。</summary>
        private void ReapplyUICameraToAll()
        {
            foreach (var group in _groups.Values)
            {
                group.ReapplyUICamera(_uiCamera);
            }
        }

        // ===============================================================
        // 内部
        // ===============================================================

        private UIGroup GetGroup(UIGroupConfig config)
            => _groups.GetOrAdd(config, () => new UIGroup(this, config));

        private UIGroup RouteToGroup(FormHandle handle)
        {
            if (handle.Entry == null || handle.Entry.Group == null)
            {
                return null;
            }

            return _groups.TryGetValue(handle.Entry.Group, out var group) ? group : null;
        }

        /// <summary>建组根节点（Group Root 模型）：挂 canvasRoot 下、拉伸铺满，按 Depth 排 sibling 序（高 Depth 在上）。</summary>
        internal RectTransform EnsureGroupRoot(UIGroupConfig config)
        {

            if (_groupRoots.TryGetValue(config, out var root) && root != null)
            {
                return root;
            }

            // 伪空条目清理（场景销毁后旧根失效）：移除并重建——「伪空查表命中阻止重建」的修复
            _groupRoots.Remove(config);

            var go = new GameObject(config.name + "Root", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRoot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _groupRoots[config] = rt;
            SortGroupRoots();
            return rt;
        }

        /// <summary>组根 sibling 序 = Depth 序（Depth 升序 → sibling index 升序 → 高 Depth 渲染在上）。</summary>
        private void SortGroupRoots()
        {
            var roots = new List<KeyValuePair<UIGroupConfig, RectTransform>>(_groupRoots.Count);
            foreach (var kv in _groupRoots)
            {
                if (kv.Value != null)
                {
                    roots.Add(kv); // 防御：残留假 null 条目（场景卸载路径）跳过，避免 SetSiblingIndex 触碰已销毁对象
                }
            }

            roots.Sort((a, b) => a.Key.Depth.CompareTo(b.Key.Depth));
            for (int i = 0; i < roots.Count; i++)
            {
                roots[i].Value.SetSiblingIndex(i);
            }
        }

        // ===============================================================
        // 跨组仲裁（结构变更后统一重算；O(n log n)，n = 存活表单数）
        // ===============================================================

        private void Arbitrate()
        {
            var groups = GetGroupsSortedByDepth();

            // 1. 视觉序下发：组内 sibling 序（组根 sibling 已按 Depth 排好）
            ApplyOrder(groups);

            var instances = CollectInstances(groups);

            // 2. 焦点链：全局焦点 = 参与焦点的最高视觉位表单（纯派生，无存储态可失同步）
            UpdateFocus(FindFocusTop(instances));

            // 3. 遮挡派生：视觉序中任何表单在其上即 covered（仅 Opened 实例通知）
            ApplyCovered(instances);

            // 4. 输入可达：模态为界（自顶向下至首个模态为止，含它自身）
            ApplyInput(instances);
        }

        private List<UIGroup> GetGroupsSortedByDepth()
        {
            var groups = new List<UIGroup>(_groups.Count);
            foreach (var group in _groups.Values)
            {
                groups.Add(group);
            }

            groups.Sort((a, b) => b.Config.Depth.CompareTo(a.Config.Depth));
            return groups;
        }

        private List<FormInstance> CollectInstances(List<UIGroup> groups)
        {
            var instances = new List<FormInstance>(_groups.Count * 4);
            foreach (var group in groups)
            {
                group.CollectTopToBottom(instances);
            }

            return instances;
        }

        private void ApplyOrder(List<UIGroup> groups)
        {
            var slice = new List<FormInstance>(8);
            foreach (var group in groups)
            {
                slice.Clear();
                group.CollectTopToBottom(slice); // slice[0] = 组内顶
                for (int i = 0; i < slice.Count; i++)
                {
                    // Unity 大 index 靠前渲染，顶 → 最高 sibling index
                    slice[i].View.ApplyOrder(slice.Count - 1 - i);
                }
            }
        }

        private static FormInstance FindFocusTop(List<FormInstance> instances)
        {
            foreach (var instance in instances)
            {
                if (instance.Group.Config.ParticipatesInFocus)
                {
                    return instance;
                }
            }

            return null;
        }

        private void UpdateFocus(FormInstance newFocus)
        {
            FormHandle newTop = newFocus != null ? newFocus.Handle : default;
            if (newTop == _focusTop)
            {
                return;
            }

            var oldTop = _focusTop;
            _focusTop = newTop;

            // 旧顶失焦（若仍存活）
            if (IsValid(oldTop) && TryGetInstance(oldTop, out var oldInstance))
            {
                try
                {
                    if(oldInstance.Logic)
                        oldInstance.Logic.OnLoseFocus();
                }
                catch (Exception ex)
                {
                    IsolateNotification(ex, oldTop.Entry);
                }
            }

            // 新顶聚焦
            if (newFocus != null && newFocus.Logic != null)
            {
                try
                {
                    newFocus.Logic.OnFocus();
                }
                catch (Exception ex)
                {
                    IsolateNotification(ex, newFocus.Handle.Entry);
                }
            }

            _eventBus.Publish(new FocusChangedEvent(oldTop, newTop));
        }

        /// <summary>遮挡派生（含通知去重）：covered 变化时触发 OnCover/OnReveal（+ 组 PauseWhenCovered 时 OnPause/OnResume）。</summary>
        private void ApplyCovered(List<FormInstance> instances)
        {
            bool anyAbove = false;
            foreach (var instance in instances)
            {
                if (instance.State == FormInstance.LifecycleState.Opened)
                {
                    UpdateCoverState(instance, anyAbove);
                }

                anyAbove = true;
            }
        }

        private void UpdateCoverState(FormInstance instance, bool covered)
        {
            if (instance.Covered == covered)
            {
                return;
            }

            instance.Covered = covered;

            // 视图过渡（能力接口探测，fire-and-forget）：被遮/恢复时播放 Cover/Reveal 动画
            instance.View.PlayCoverAnimation(covered);

            var logic = instance.Logic;
            if (logic == null)
            {
                return;
            }

            try
            {
                if (covered)
                {
                    logic.OnCover();
                    if (instance.Group.Config.PauseWhenCovered)
                    {
                        logic.OnPause();
                    }
                }
                else
                {
                    logic.OnReveal();
                    if (instance.Group.Config.PauseWhenCovered)
                    {
                        logic.OnResume();
                    }
                }
            }
            catch (Exception ex)
            {
                IsolateNotification(ex, instance.Handle.Entry);
            }
        }

        private void ApplyInput(List<FormInstance> instances)
        {
            bool blocked = false;
            foreach (var instance in instances)
            {
                instance.View.ApplyInputEnabled(!blocked);
                if (instance.Group.Config.Modal)
                {
                    blocked = true;
                }
            }
        }

        private bool TryGetInstance(FormHandle handle, out FormInstance instance)
        {
            instance = null;
            if (handle.Entry == null || handle.Entry.Group == null
                || !_groups.TryGetValue(handle.Entry.Group, out var group))
            {
                return false;
            }

            return group.TryGetInstance(handle, out instance);
        }

        /// <summary>通知型钩子（OnFocus/OnLoseFocus/OnCover/OnReveal/OnPause/OnResume）异常：隔离（记日志 + 上报），不中断仲裁。</summary>
        private void IsolateNotification(Exception ex, FormEntry entry)
        {
            Log.Exception(ex);
            RaiseFormException(entry, ex);
        }
    }
}
