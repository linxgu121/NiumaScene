using System;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景检查点保存结果。
    /// 该结果用于 SceneTransitionResult 记录调试事实，不暴露 NiumaSave 的内部实现类型。
    /// </summary>
    [Serializable]
    public sealed class SceneCheckpointSaveResult
    {
        /// <summary>是否成功完成检查点保存。</summary>
        public bool Succeeded;

        /// <summary>实际写入的槽位 ID。由保存模块决定，场景模块只记录结果。</summary>
        public string SlotId;

        /// <summary>失败或调试信息。</summary>
        public string Message;

        public static SceneCheckpointSaveResult Success(string slotId, string message = null)
        {
            return new SceneCheckpointSaveResult
            {
                Succeeded = true,
                SlotId = slotId,
                Message = message
            };
        }

        public static SceneCheckpointSaveResult Fail(string message)
        {
            return new SceneCheckpointSaveResult
            {
                Succeeded = false,
                Message = message
            };
        }
    }
}
