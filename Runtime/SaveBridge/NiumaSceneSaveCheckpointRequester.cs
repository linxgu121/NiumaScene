using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;
using NiumaSave.Controller;
using NiumaScene.Checkpoint;
using NiumaScene.Data;
using UnityEngine;

namespace NiumaScene.SaveBridge
{
    /// <summary>
    /// NiumaScene 到 NiumaSave 的检查点保存桥接。
    /// Scene 模块只发起保存意图，真正的槽位轮替、Provider 导出和文件写入由 NiumaSaveController 负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaSceneSaveCheckpointRequester : MonoBehaviour, ISceneCheckpointRequester
    {
        [Header("存档控制器")]
        [Tooltip("NiumaSave 根控制器。为空时会在场景中自动查找。正式场景建议手动绑定 BootstrapRoot 上的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Tooltip("未手动绑定 SaveController 时，是否自动查找场景中的 NiumaSaveController。")]
        [SerializeField] private bool autoFindSaveController = true;

        [Header("保存策略")]
        [Tooltip("检查点显示名称前缀。最终名称会追加触发原因，便于调试存档列表。")]
        [SerializeField] private string checkpointDisplayNamePrefix = "Scene Checkpoint";

        [Tooltip("检查点写入策略。MiniGame 进入前建议 LocalOnly，避免网络抖动阻塞场景切换。")]
        [SerializeField] private SaveWriteMode writeMode = SaveWriteMode.LocalOnly;

        [Header("调试")]
        [Tooltip("保存失败或依赖缺失时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        public async Task<SceneCheckpointSaveResult> RequestCheckpointSaveAsync(
            SceneCheckpointSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!ResolveSaveController(true))
            {
                return SceneCheckpointSaveResult.Fail("未找到 NiumaSaveController，无法保存检查点。");
            }

            try
            {
                var displayName = BuildDisplayName(request);
                var result = await saveController.SaveCheckpointAsync(displayName, writeMode, cancellationToken);
                if (result.Succeeded)
                {
                    return SceneCheckpointSaveResult.Success(
                        result.Metadata?.SlotId,
                        string.IsNullOrWhiteSpace(result.Message) ? "检查点保存成功。" : result.Message);
                }

                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? $"检查点保存失败：State={result.State}"
                    : result.Message;
                Warn(message);
                return SceneCheckpointSaveResult.Fail(message);
            }
            catch (System.Exception exception)
            {
                var message = $"检查点保存异常：{exception.Message}";
                Warn(message);
                return SceneCheckpointSaveResult.Fail(message);
            }
        }

        private bool ResolveSaveController(bool warn)
        {
            if (saveController != null)
            {
                return true;
            }

            if (!autoFindSaveController)
            {
                Warn("未绑定 NiumaSaveController。", warn);
                return false;
            }

#if UNITY_2023_1_OR_NEWER
            saveController = FindFirstObjectByType<NiumaSaveController>();
#else
            saveController = FindObjectOfType<NiumaSaveController>();
#endif

            if (saveController == null)
            {
                Warn("自动查找 NiumaSaveController 失败。", warn);
            }

            return saveController != null;
        }

        private string BuildDisplayName(SceneCheckpointSaveRequest request)
        {
            var prefix = string.IsNullOrWhiteSpace(checkpointDisplayNamePrefix)
                ? "Scene Checkpoint"
                : checkpointDisplayNamePrefix.Trim();

            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return prefix;
            }

            return $"{prefix} - {request.Reason}";
        }

        private void Warn(string message, bool force = true)
        {
            if (logWarnings && force && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[NiumaSceneSaveCheckpointRequester] {message}", this);
            }
        }
    }
}
