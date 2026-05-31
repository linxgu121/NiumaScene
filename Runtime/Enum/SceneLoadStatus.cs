namespace NiumaScene.Enum
{
    /// <summary>
    /// 场景请求运行状态。
    /// Completed / Failed / Cancelled / Skipped 都表示请求已经结束。
    /// </summary>
    public enum SceneLoadStatus
    {
        None = 0,
        Pending = 1,
        Loading = 2,
        Activating = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6,
        Skipped = 7,
        CancelRequested = 8
    }
}
