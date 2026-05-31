namespace NiumaScene.Enum
{
    /// <summary>
    /// 返回上下文栈满时的处理策略。
    /// 正式剧情和 MiniGame 返回链默认使用 RejectNew，避免破坏完整返回路径。
    /// </summary>
    public enum SceneReturnOverflowPolicy
    {
        RejectNew = 0,
        DropOldest = 1
    }
}
