namespace NiumaScene.Enum
{
    /// <summary>
    /// 场景切换错误码。
    /// 普通配置错误通过失败 Handle 返回，不通过异常暴露给按钮或交互入口。
    /// </summary>
    public enum SceneLoadErrorCode
    {
        None = 0,
        EmptySceneName = 1,
        AlreadyLoading = 2,
        SceneNotFound = 3,
        SpawnPointNotFound = 4,
        SpawnTargetMissing = 5,
        ReturnContextMissing = 6,
        Cancelled = 7,
        InvalidRequest = 8,
        ReturnStackOverflow = 9,
        LoadFailed = 99
    }
}
