using System;
using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 一次场景切换请求。
    /// Request 只表达调用方意图，具体仲裁、校验和执行由 SceneService 完成。
    /// </summary>
    [Serializable]
    public sealed class SceneTransitionRequest
    {
        /// <summary>请求 ID。为空时由 SceneService 自动生成 GUID。</summary>
        public string RequestId;

        /// <summary>本次切换目的，用于日志、调试和后续策略分流。</summary>
        public SceneLoadPurpose Purpose = SceneLoadPurpose.None;

        /// <summary>目标场景信息。必填。</summary>
        public SceneTransitionTarget Target;

        /// <summary>返回策略。为空表示不记录返回上下文。</summary>
        public SceneReturnPolicy ReturnPolicy;

        /// <summary>执行选项。为空时使用默认选项。</summary>
        public SceneTransitionOptions Options;
    }
}
