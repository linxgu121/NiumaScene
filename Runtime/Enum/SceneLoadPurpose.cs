namespace NiumaScene.Enum
{
    /// <summary>
    /// 场景切换目的。
    /// None 是默认无效值，服务层应按普通场景切换处理并输出调试警告。
    /// </summary>
    public enum SceneLoadPurpose
    {
        None = 0,
        Bootstrap = 1,
        MainGame = 2,
        MiniGame = 3,
        EnterBuilding = 4,
        ExitBuilding = 5,
        Teleport = 6,
        Respawn = 7,
        Return = 8,
        Debug = 100
    }
}
