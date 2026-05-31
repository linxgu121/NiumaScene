using System;
using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景请求结束后的稳定结果。
    /// UI、调试器、自动测试和上层流程都应读取 Result，而不是推断内部执行细节。
    /// </summary>
    [Serializable]
    public sealed class SceneTransitionResult
    {
        /// <summary>请求 ID。</summary>
        public string RequestId;

        /// <summary>本次请求最终是否成功。</summary>
        public bool Succeeded;

        /// <summary>最终状态。应与 Handle 的最终状态一致。</summary>
        public SceneLoadStatus FinalStatus = SceneLoadStatus.None;

        /// <summary>错误码。成功时应为 None。</summary>
        public SceneLoadErrorCode ErrorCode = SceneLoadErrorCode.None;

        /// <summary>调试错误信息。正式 UI 不应直接展示内部调试文本。</summary>
        public string ErrorMessage;

        /// <summary>请求发起时所在场景。</summary>
        public string FromSceneName;

        /// <summary>请求目标场景。</summary>
        public string TargetSceneName;

        /// <summary>实际激活的场景。使用 fallback 时可能与目标场景不同。</summary>
        public string ActivatedSceneName;

        /// <summary>实际使用的出生点 ID。没有移动玩家时为空。</summary>
        public string AppliedSpawnPointId;

        /// <summary>是否压入了返回上下文。</summary>
        public bool PushedReturnContext;

        /// <summary>是否弹出了返回上下文。</summary>
        public bool PoppedReturnContext;

        /// <summary>是否因 DropOldest 策略丢弃了返回上下文。</summary>
        public bool DroppedReturnContext;

        /// <summary>被丢弃的返回上下文 ID。</summary>
        public string DroppedReturnContextId;

        /// <summary>是否使用了 fallback 场景。</summary>
        public bool UsedFallbackScene;

        /// <summary>实际使用的 fallback 场景名。</summary>
        public string FallbackSceneName;

        /// <summary>本次场景切换是否请求了检查点保存。</summary>
        public bool RequestedCheckpointSave;

        /// <summary>检查点保存是否成功。未请求时为 false。</summary>
        public bool CheckpointSaveSucceeded;

        /// <summary>检查点保存写入的槽位 ID。</summary>
        public string CheckpointSaveSlotId;

        /// <summary>检查点保存的调试信息。</summary>
        public string CheckpointSaveMessage;

        public static SceneTransitionResult Success(string requestId, SceneLoadStatus status = SceneLoadStatus.Completed)
        {
            return new SceneTransitionResult
            {
                RequestId = requestId,
                Succeeded = true,
                FinalStatus = status,
                ErrorCode = SceneLoadErrorCode.None
            };
        }

        public static SceneTransitionResult Failure(
            string requestId,
            SceneLoadErrorCode errorCode,
            string errorMessage = null,
            SceneLoadStatus status = SceneLoadStatus.Failed)
        {
            return new SceneTransitionResult
            {
                RequestId = requestId,
                Succeeded = false,
                FinalStatus = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
