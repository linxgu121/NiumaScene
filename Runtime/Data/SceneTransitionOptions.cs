using System;
using NiumaScene.Enum;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景切换选项。
    /// 这些选项描述“怎么执行本次请求”，不保存具体业务模块引用。
    /// </summary>
    [Serializable]
    public sealed class SceneTransitionOptions
    {
        /// <summary>是否请求检查点保存。NiumaScene 只发起意图，不直接写存档文件。</summary>
        public bool RequestCheckpointSave;

        /// <summary>加载期间是否冻结玩家输入。默认开启，避免切场景时玩家继续移动或交互。</summary>
        public bool FreezeInputDuringLoad = true;

        /// <summary>加载期间是否显示加载 UI。第一版仅作为数据选项保留。</summary>
        public bool ShowLoadingUI;

        /// <summary>加载中收到新请求时，是否替换尚未执行的 Pending 请求。</summary>
        public bool ReplacePendingRequest = true;

        /// <summary>返回上下文栈满时的处理策略。正式流程默认拒绝新上下文。</summary>
        public SceneReturnOverflowPolicy ReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew;
    }
}
