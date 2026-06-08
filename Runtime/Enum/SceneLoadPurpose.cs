using UnityEngine;

namespace NiumaScene.Enum
{
    /// <summary>
    /// 场景切换目的。
    /// None 是默认无效值，服务层应按普通场景切换处理并输出调试警告。
    /// </summary>
    public enum SceneLoadPurpose
    {
        [InspectorName("普通切换（无特殊用途）")]
        None = 0,

        [InspectorName("启动流程（Bootstrap/Core 场景初始化）")]
        Bootstrap = 1,

        [InspectorName("主游戏（进入 RPG 主场景）")]
        MainGame = 2,

        [InspectorName("小游戏（RPG 进入 MiniGame）")]
        MiniGame = 3,

        [InspectorName("进入建筑（室外进入室内）")]
        EnterBuilding = 4,

        [InspectorName("离开建筑（室内返回室外）")]
        ExitBuilding = 5,

        [InspectorName("传送（传送点或地图跳转）")]
        Teleport = 6,

        [InspectorName("复活（死亡后回检查点）")]
        Respawn = 7,

        [InspectorName("返回（ReturnToPreviousScene）")]
        Return = 8,

        [InspectorName("调试（测试按钮或开发场景）")]
        Debug = 100
    }
}
