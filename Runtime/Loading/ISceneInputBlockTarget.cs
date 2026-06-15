namespace NiumaScene.Loading
{
    /// <summary>
    /// 场景加载输入阻塞目标。
    /// TPC、Interact、MiniGame 等模块通过适配器实现该接口，避免 NiumaScene 直接依赖具体输入系统。
    /// </summary>
    public interface ISceneInputBlockTarget
    {
        /// <summary>
        /// 设置由场景加载引起的输入阻塞。
        /// 实现层应尽量按 reason 只解除自己加上的阻塞，不要覆盖剧情、死亡、菜单等其他系统的禁用状态。
        /// </summary>
        void SetSceneInputBlocked(bool blocked, string reason);
    }
}
