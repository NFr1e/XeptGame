using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景组加载句柄——<see cref="IScenesManager.LoadSceneGroupAsync"/> 的返回值。
    /// 聚合组内所有 <see cref="SceneHandle"/>，并提供主场景引用与整体进度。
    /// </summary>
    public sealed class SceneGroupHandle
    {
        /// <summary>组内所有场景的句柄列表。</summary>
        public IReadOnlyList<SceneHandle> Handles { get; }

        /// <summary>
        /// 组内主场景句柄（按组内 IsMainScene 条目确定）。
        /// 无标记时取首个 Active 句柄兜底；全失败为 null。
        /// </summary>
        public SceneHandle MainScene { get; }

        /// <summary>组内所有场景的聚合进度，范围 [0, 1]（各句柄 Progress 算术平均；空组返回 1f）。</summary>
        public float OverallProgress
        {
            get
            {
                if (Handles.Count == 0) return 1f;
                float sum = 0f;
                foreach (var h in Handles) sum += h.Progress;
                return sum / Handles.Count;
            }
        }

        /// <summary>等待组内所有场景加载完成。</summary>
        public async UniTask WaitForCompletionAsync()
        {
            var tasks = new UniTask[Handles.Count];
            for (int i = 0; i < Handles.Count; i++)
            {
                tasks[i] = Handles[i].WaitForCompletionAsync();
            }
            await UniTask.WhenAll(tasks);
        }

        internal SceneGroupHandle(List<SceneHandle> handles, SceneHandle mainScene)
        {
            Handles = handles.AsReadOnly();
            MainScene = mainScene ?? FindFallbackMainScene(handles);
        }

        private static SceneHandle FindFallbackMainScene(List<SceneHandle> handles)
        {
            for (int i = 0; i < handles.Count; i++)
            {
                if (handles[i].State == SceneState.Active)
                {
                    return handles[i];
                }
            }
            return null;
        }
    }
}
