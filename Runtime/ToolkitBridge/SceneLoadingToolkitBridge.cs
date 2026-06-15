using NiumaScene.Data;
using NiumaScene.Enum;
using NiumaScene.Loading;
using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaScene.ToolkitBridge
{
    /// <summary>
    /// SceneLoadingSnapshot 到 NiumaUI Toolkit Loading View 的桥接。
    /// 挂在核心场景 UIRoot/UIBridges 下，然后拖给 SceneLoadingStateBridge.Loading Receiver Provider。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoadingToolkitBridge : MonoBehaviour, ISceneLoadingReceiver
    {
        [Header("Toolkit UI")]
        [Tooltip("UI Toolkit 根控制器。拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager；为空时可自动查找。")]
        [SerializeField] private UIToolkitUIManager uiManager;

        [Tooltip("未绑定 UI Manager 时是否自动查找场景中的 UIToolkitUIManager。正式核心场景建议手动绑定后关闭。")]
        [SerializeField] private bool autoFindUIManager = true;

        [Header("显示文案")]
        [Tooltip("加载进行中显示的状态文案。")]
        [SerializeField] private string loadingText = "加载中";

        [Tooltip("激活目标场景时显示的状态文案。")]
        [SerializeField] private string activatingText = "正在进入";

        [Tooltip("加载完成时显示的状态文案。")]
        [SerializeField] private string completedText = "加载完成";

        [Tooltip("加载失败时显示的状态文案。")]
        [SerializeField] private string failedText = "加载失败";

        [Header("行为")]
        [Tooltip("加载结束时是否调用 HideLoading。建议开启。")]
        [SerializeField] private bool hideWhenLoadingEnds = true;

        [Tooltip("Loading View 是否按 SceneLoadingSnapshot.FreezeInputDuringLoad 阻塞玩法输入。关闭后，只要显示 Loading 就视为阻塞。")]
        [SerializeField] private bool useSnapshotInputBlockFlag = true;

        [Header("调试")]
        [Tooltip("缺少 UI Manager 时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        public void ApplySceneLoadingSnapshot(SceneLoadingSnapshot snapshot)
        {
            snapshot ??= SceneLoadingSnapshot.Empty();

            if (!snapshot.ShowLoadingUI || !ShouldShow(snapshot))
            {
                if (hideWhenLoadingEnds && EnsureUIManager(false))
                {
                    uiManager.HideLoading();
                }

                return;
            }

            if (!EnsureUIManager())
            {
                return;
            }

            uiManager.ShowLoading(new UIToolkitLoadingViewData
            {
                LoadingId = string.IsNullOrWhiteSpace(snapshot.RequestId) ? "scene-loading" : snapshot.RequestId,
                Message = BuildMessage(snapshot),
                Progress01 = Mathf.Clamp01(snapshot.Progress),
                IsBlocking = !useSnapshotInputBlockFlag || snapshot.FreezeInputDuringLoad
            });
        }

        private static bool ShouldShow(SceneLoadingSnapshot snapshot)
        {
            return snapshot.IsLoading
                   || snapshot.Status == SceneLoadStatus.Pending
                   || snapshot.Status == SceneLoadStatus.Loading
                   || snapshot.Status == SceneLoadStatus.Activating
                   || snapshot.ErrorCode != SceneLoadErrorCode.None;
        }

        private string BuildMessage(SceneLoadingSnapshot snapshot)
        {
            var status = ResolveStatusText(snapshot);
            if (snapshot.ErrorCode != SceneLoadErrorCode.None)
            {
                var error = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                    ? snapshot.ErrorCode.ToString()
                    : snapshot.ErrorMessage;
                return string.IsNullOrWhiteSpace(error) ? status : $"{status}：{error}";
            }

            if (string.IsNullOrWhiteSpace(snapshot.TargetSceneName))
            {
                return status;
            }

            return $"{status}：{snapshot.TargetSceneName}";
        }

        private string ResolveStatusText(SceneLoadingSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            if (snapshot.ErrorCode != SceneLoadErrorCode.None || snapshot.Status == SceneLoadStatus.Failed)
            {
                return failedText;
            }

            switch (snapshot.Status)
            {
                case SceneLoadStatus.Pending:
                case SceneLoadStatus.Loading:
                    return loadingText;
                case SceneLoadStatus.Activating:
                    return activatingText;
                case SceneLoadStatus.Completed:
                case SceneLoadStatus.Skipped:
                    return completedText;
                default:
                    return snapshot.IsLoading ? loadingText : string.Empty;
            }
        }

        private bool EnsureUIManager(bool logMissing = true)
        {
            if (uiManager != null)
            {
                return true;
            }

            if (autoFindUIManager)
            {
                uiManager = FindAnyObjectByType<UIToolkitUIManager>();
            }

            if (uiManager == null && logMissing)
            {
                Warn("未绑定 UIToolkitUIManager，无法显示 Scene Loading。请拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。");
            }

            return uiManager != null;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[SceneLoadingToolkitBridge] {message}", this);
            }
        }
    }
}
