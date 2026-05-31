using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景请求运行时句柄。
    /// 调用方只读取状态；状态推进由 SceneService 在实现层负责。
    /// </summary>
    public sealed class SceneTransitionHandle
    {
        public string RequestId { get; internal set; }
        public SceneLoadStatus Status { get; internal set; }
        public SceneLoadErrorCode ErrorCode { get; internal set; }
        public SceneTransitionResult Result { get; internal set; }

        public bool IsDone
        {
            get
            {
                return Status == SceneLoadStatus.Completed
                       || Status == SceneLoadStatus.Failed
                       || Status == SceneLoadStatus.Cancelled
                       || Status == SceneLoadStatus.Skipped;
            }
        }

        public SceneTransitionHandle(string requestId)
        {
            RequestId = requestId;
            Status = SceneLoadStatus.Pending;
            ErrorCode = SceneLoadErrorCode.None;
        }

        internal void SetStatus(SceneLoadStatus status)
        {
            Status = status;
        }

        internal void Complete(SceneTransitionResult result)
        {
            Result = result;
            Status = result != null ? result.FinalStatus : SceneLoadStatus.Completed;
            ErrorCode = result != null ? result.ErrorCode : SceneLoadErrorCode.None;
        }

        public static SceneTransitionHandle Failed(string requestId, SceneLoadErrorCode errorCode, string message = null)
        {
            var handle = new SceneTransitionHandle(requestId)
            {
                Status = SceneLoadStatus.Failed,
                ErrorCode = errorCode
            };
            handle.Result = SceneTransitionResult.Failure(requestId, errorCode, message);
            return handle;
        }

        public static SceneTransitionHandle Skipped(string requestId, string message = null)
        {
            var handle = new SceneTransitionHandle(requestId)
            {
                Status = SceneLoadStatus.Skipped,
                ErrorCode = SceneLoadErrorCode.None
            };
            handle.Result = SceneTransitionResult.Success(requestId, SceneLoadStatus.Skipped);
            handle.Result.ErrorMessage = message;
            return handle;
        }
    }
}
