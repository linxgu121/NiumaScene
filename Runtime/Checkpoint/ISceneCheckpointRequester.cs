using System.Threading;
using System.Threading.Tasks;
using NiumaScene.Data;

namespace NiumaScene.Checkpoint
{
    /// <summary>
    /// 场景检查点保存请求接口。
    /// NiumaScene 只依赖该抽象发起保存意图，不直接引用 NiumaSave 或写文件。
    /// </summary>
    public interface ISceneCheckpointRequester
    {
        /// <summary>
        /// 请求保存检查点。
        /// 实现层应在返回 Task 前完成必要的快照收集，避免源场景卸载后 Provider 丢失。
        /// </summary>
        Task<SceneCheckpointSaveResult> RequestCheckpointSaveAsync(
            SceneCheckpointSaveRequest request,
            CancellationToken cancellationToken = default);
    }
}
