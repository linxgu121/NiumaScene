using System;
using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景加载状态快照。
    /// UI 和输入桥接层应读取该快照，而不是追踪 SceneService 内部协程。
    /// </summary>
    [Serializable]
    public sealed class SceneLoadingSnapshot
    {
        public string RequestId;
        public SceneLoadStatus Status = SceneLoadStatus.None;
        public string TargetSceneName;
        public float Progress;
        public bool IsLoading;
        public bool FreezeInputDuringLoad;
        public bool ShowLoadingUI;
        public SceneLoadErrorCode ErrorCode = SceneLoadErrorCode.None;
        public string ErrorMessage;

        public static SceneLoadingSnapshot Empty()
        {
            return new SceneLoadingSnapshot
            {
                Status = SceneLoadStatus.None,
                Progress = 0f,
                IsLoading = false,
                FreezeInputDuringLoad = false,
                ShowLoadingUI = false,
                ErrorCode = SceneLoadErrorCode.None
            };
        }
    }
}
