using NiumaScene.Data;

namespace NiumaScene.Loading
{
    /// <summary>
    /// 场景加载状态接收者。
    /// Loading UI、调试面板或过渡动画可实现该接口，从桥接层接收稳定快照。
    /// </summary>
    public interface ISceneLoadingReceiver
    {
        /// <summary>
        /// 应用最新加载快照。
        /// 实现层只负责表现，不应反向推进场景加载流程。
        /// </summary>
        void ApplySceneLoadingSnapshot(SceneLoadingSnapshot snapshot);
    }
}
