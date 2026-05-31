using NiumaScene.Data;
using NiumaScene.Enum;
using NiumaScene.Loading;
using TMPro;
using UnityEngine;

namespace NiumaScene.UIBridge
{
    /// <summary>
    /// 场景加载面板桥接。
    /// 只把 SceneLoadingSnapshot 转换为 Canvas 表现，不反向驱动场景加载流程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoadingPanelBridge : MonoBehaviour, ISceneLoadingReceiver
    {
        [Header("面板节点")]
        [Tooltip("加载面板根节点。建议绑定脚本下方的全屏 LoadingPanel 子物体；为空时使用当前物体且不会自动 SetActive(false)。")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("面板 CanvasGroup。用于淡入淡出、控制射线阻挡。为空时会从当前物体或 PanelRoot 自动获取。")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("进度条填充 RectTransform。脚本会通过修改 anchorMax.x 表示加载进度。为空时不显示进度条。")]
        [SerializeField] private RectTransform progressFill;

        [Header("文本")]
        [Tooltip("主状态文本，例如“加载中”“加载失败”。为空时不写入。")]
        [SerializeField] private TMP_Text statusText;

        [Tooltip("目标场景文本。为空时不写入。")]
        [SerializeField] private TMP_Text targetSceneText;

        [Tooltip("进度百分比文本。为空时不写入。")]
        [SerializeField] private TMP_Text progressText;

        [Tooltip("错误文本。为空时不写入。")]
        [SerializeField] private TMP_Text errorText;

        [Header("显示文案")]
        [Tooltip("加载进行中显示的状态文案。")]
        [SerializeField] private string loadingText = "加载中";

        [Tooltip("激活目标场景时显示的状态文案。")]
        [SerializeField] private string activatingText = "正在进入";

        [Tooltip("加载完成时显示的状态文案。")]
        [SerializeField] private string completedText = "加载完成";

        [Tooltip("加载失败时显示的状态文案。")]
        [SerializeField] private string failedText = "加载失败";

        [Tooltip("目标场景为空时显示的占位文案。为空则清空目标场景文本。")]
        [SerializeField] private string emptyTargetSceneText;

        [Header("过渡动画")]
        [Tooltip("淡入耗时，单位秒。设为 0 时立即显示。")]
        [Min(0f)]
        [SerializeField] private float fadeInDuration = 0.15f;

        [Tooltip("淡出耗时，单位秒。设为 0 时立即隐藏。")]
        [Min(0f)]
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Tooltip("是否使用 unscaledDeltaTime。建议开启，避免场景加载时 TimeScale 为 0 导致 UI 不动。")]
        [SerializeField] private bool useUnscaledTime = true;

        [Tooltip("透明度为 0 后是否关闭 PanelRoot。建议开启，避免隐藏面板继续参与布局和射线。")]
        [SerializeField] private bool disableRootWhenHidden = true;

        [Tooltip("面板可见时是否阻挡 UI 射线。Loading 遮罩通常需要开启。")]
        [SerializeField] private bool blockRaycastsWhenVisible = true;

        [Header("失败显示")]
        [Tooltip("加载失败时是否短暂保留面板显示错误。关闭后失败也会立即淡出。")]
        [SerializeField] private bool keepVisibleOnFailure = true;

        [Tooltip("加载失败后保留错误提示的时间，单位秒。")]
        [Min(0f)]
        [SerializeField] private float failureVisibleSeconds = 3f;

        [Header("调试")]
        [Tooltip("缺少 CanvasGroup 或配置异常时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private float _targetAlpha;
        private float _failureVisibleTimer;
        private bool _isShowingFailure;

        private void Awake()
        {
            ResolveReferences(true);
            ApplyAlpha(0f);
            SetRootActive(false);
            SetProgress(0f);
        }

        private void OnEnable()
        {
            ResolveReferences(false);
        }

        private void OnDisable()
        {
            _isShowingFailure = false;
            _failureVisibleTimer = 0f;
        }

        private void Update()
        {
            TickFailureTimer();
            TickFade();
        }

        public void ApplySceneLoadingSnapshot(SceneLoadingSnapshot snapshot)
        {
            snapshot ??= SceneLoadingSnapshot.Empty();

            var shouldShow = ShouldShow(snapshot);
            if (shouldShow)
            {
                SetRootActive(true);
            }

            _targetAlpha = shouldShow ? 1f : 0f;
            ApplyTexts(snapshot);
            SetProgress(snapshot.Progress);

            if (snapshot.ShowLoadingUI
                && snapshot.ErrorCode != SceneLoadErrorCode.None
                && keepVisibleOnFailure
                && failureVisibleSeconds > 0f)
            {
                _isShowingFailure = true;
                _failureVisibleTimer = failureVisibleSeconds;
                _targetAlpha = 1f;
                SetRootActive(true);
            }
            else if (snapshot.IsLoading)
            {
                _isShowingFailure = false;
                _failureVisibleTimer = 0f;
            }

            if (fadeInDuration <= 0f && _targetAlpha > 0f)
            {
                ApplyAlpha(1f);
            }
            else if (fadeOutDuration <= 0f && _targetAlpha <= 0f)
            {
                ApplyAlpha(0f);
                SetRootActive(false);
            }
        }

        /// <summary>
        /// 立即隐藏加载面板。
        /// 常用于调试按钮或异常流程兜底。
        /// </summary>
        public void HideImmediately()
        {
            _targetAlpha = 0f;
            _isShowingFailure = false;
            _failureVisibleTimer = 0f;
            ApplyAlpha(0f);
            SetRootActive(false);
        }

        private void ResolveReferences(bool warn)
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (canvasGroup == null)
            {
                canvasGroup = panelRoot != null
                    ? panelRoot.GetComponent<CanvasGroup>()
                    : GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null && warn)
            {
                Warn("未绑定 CanvasGroup，淡入淡出和射线控制不会生效。");
            }
        }

        private bool ShouldShow(SceneLoadingSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.ShowLoadingUI)
            {
                return _isShowingFailure;
            }

            return snapshot.IsLoading
                   || snapshot.Status == SceneLoadStatus.Pending
                   || snapshot.Status == SceneLoadStatus.Loading
                   || snapshot.Status == SceneLoadStatus.Activating
                   || (keepVisibleOnFailure && snapshot.ErrorCode != SceneLoadErrorCode.None);
        }

        private void ApplyTexts(SceneLoadingSnapshot snapshot)
        {
            SetText(statusText, ResolveStatusText(snapshot));

            var targetName = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.TargetSceneName)
                ? snapshot.TargetSceneName
                : emptyTargetSceneText;
            SetText(targetSceneText, targetName);

            var progress = snapshot != null ? Mathf.Clamp01(snapshot.Progress) : 0f;
            SetText(progressText, $"{Mathf.RoundToInt(progress * 100f)}%");

            var error = snapshot != null && snapshot.ErrorCode != SceneLoadErrorCode.None
                ? string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                    ? snapshot.ErrorCode.ToString()
                    : snapshot.ErrorMessage
                : string.Empty;
            SetText(errorText, error);
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

        private void TickFailureTimer()
        {
            if (!_isShowingFailure || failureVisibleSeconds <= 0f)
            {
                return;
            }

            _failureVisibleTimer -= DeltaTime;
            if (_failureVisibleTimer <= 0f)
            {
                _isShowingFailure = false;
                _targetAlpha = 0f;
            }
        }

        private void TickFade()
        {
            if (canvasGroup == null)
            {
                if (_targetAlpha <= 0f && disableRootWhenHidden)
                {
                    SetRootActive(false);
                }

                return;
            }

            var current = canvasGroup.alpha;
            if (Mathf.Approximately(current, _targetAlpha))
            {
                ApplyAlpha(_targetAlpha);
                if (_targetAlpha <= 0f && disableRootWhenHidden)
                {
                    SetRootActive(false);
                }

                return;
            }

            var duration = _targetAlpha > current ? fadeInDuration : fadeOutDuration;
            var next = duration <= 0f
                ? _targetAlpha
                : Mathf.MoveTowards(current, _targetAlpha, DeltaTime / duration);

            ApplyAlpha(next);

            if (next <= 0f && _targetAlpha <= 0f && disableRootWhenHidden)
            {
                SetRootActive(false);
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            var visible = canvasGroup.alpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible && blockRaycastsWhenVisible;
        }

        private void SetProgress(float progress)
        {
            if (progressFill == null)
            {
                return;
            }

            var anchorMax = progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(progress);
            progressFill.anchorMax = anchorMax;
        }

        private void SetRootActive(bool active)
        {
            // 脚本和根节点在同一个物体上时，关闭根节点会让本脚本停止 Update，淡入淡出无法继续。
            if (!active && panelRoot == gameObject)
            {
                return;
            }

            if (panelRoot != null && panelRoot.activeSelf != active)
            {
                panelRoot.SetActive(active);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[SceneLoadingPanelBridge] {message}", this);
            }
        }
    }
}
