using System;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景返回策略。
    /// 只描述“是否压入返回上下文”和“以后回到哪里”，不描述目标场景加载方式。
    /// </summary>
    [Serializable]
    public sealed class SceneReturnPolicy
    {
        /// <summary>是否把本次切换压入返回上下文栈。</summary>
        public bool PushReturnContext;

        /// <summary>返回时加载的场景名。为空时由服务层使用当前激活场景名。</summary>
        public string ReturnSceneName;

        /// <summary>返回场景后使用的出生点 ID。</summary>
        public string ReturnSpawnPointId;

        /// <summary>压入新上下文前是否清空旧返回栈。用于主菜单进入游戏等顶层流程。</summary>
        public bool ClearReturnStackBeforePush;
    }
}
