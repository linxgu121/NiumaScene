using NiumaScene.Data;

namespace NiumaScene.Service
{
    /// <summary>
    /// 场景服务接口。
    /// 负责场景加载、返回上下文和同场景出生点传送的统一入口。
    /// </summary>
    public interface ISceneService
    {
        /// <summary>当前是否正在执行场景加载。</summary>
        bool IsLoading { get; }

        /// <summary>当前加载状态快照。UI 和输入桥接层可以读取该数据。</summary>
        SceneLoadingSnapshot LoadingSnapshot { get; }

        /// <summary>返回上下文栈顶。为空表示当前没有可返回目标。</summary>
        SceneReturnContext CurrentReturnContext { get; }

        /// <summary>当前返回上下文数量。</summary>
        int ReturnContextCount { get; }

        /// <summary>
        /// 发起场景切换请求。
        /// 普通配置错误应返回失败 Handle，不应向按钮或交互入口抛出异常。
        /// </summary>
        SceneTransitionHandle LoadScene(SceneTransitionRequest request);

        /// <summary>
        /// 返回上一层业务场景。
        /// 没有返回上下文时返回失败 Handle。
        /// </summary>
        SceneTransitionHandle ReturnToPreviousScene(SceneTransitionOptions options = null);

        /// <summary>
        /// 在当前场景内传送到指定出生点。
        /// 该操作不加载新场景，也不修改返回上下文栈。
        /// </summary>
        SceneTransitionHandle TeleportToSpawnPoint(string spawnPointId);

        /// <summary>
        /// 取消尚未真正进入 Unity 场景加载的请求。
        /// ActiveLoad 只能做软取消标记，不能保证中断 Unity AsyncOperation。
        /// </summary>
        bool CancelLoad(string requestId);

        /// <summary>清空返回上下文栈。用于主菜单进入游戏等顶层流程。</summary>
        void ClearReturnContexts();
    }
}
