using System;

namespace NiumaScene.Data
{
    /// <summary>
    /// 返回上下文。
    /// 只保存稳定 ID 和轻量数据，不保存 GameObject、MonoBehaviour 或旧场景对象引用。
    /// </summary>
    [Serializable]
    public sealed class SceneReturnContext
    {
        /// <summary>上下文 ID，用于调试返回栈和溢出丢弃记录。</summary>
        public string ContextId;

        /// <summary>发起切换时所在场景。</summary>
        public string SourceSceneName;

        /// <summary>返回时要加载的场景。</summary>
        public string ReturnSceneName;

        /// <summary>返回场景后使用的出生点 ID。</summary>
        public string ReturnSpawnPointId;

        /// <summary>创建该上下文的原因，例如 MiniGame、Building、Respawn。</summary>
        public string Reason;

        /// <summary>创建时间，Unix 毫秒。用于日志和调试，不参与业务判断。</summary>
        public long CreatedUnixTimeMs;
    }
}
