using System;
using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景检查点保存请求。
    /// 只描述场景侧的保存意图，不包含任何 NiumaSave 内部文件格式或槽位策略。
    /// </summary>
    [Serializable]
    public sealed class SceneCheckpointSaveRequest
    {
        /// <summary>关联的场景切换 RequestId。</summary>
        public string RequestId;

        /// <summary>触发保存的原因，默认可使用场景切换目的。</summary>
        public string Reason;

        /// <summary>请求发起时所在场景。</summary>
        public string SourceSceneName;

        /// <summary>即将进入的目标场景。</summary>
        public string TargetSceneName;

        /// <summary>场景切换目的。</summary>
        public SceneLoadPurpose Purpose;

        /// <summary>请求创建时间，Unix 毫秒。</summary>
        public long CreatedUnixTimeMs;
    }
}
