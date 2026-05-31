using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NiumaScene.Checkpoint;
using NiumaScene.Data;
using NiumaScene.Enum;
using NiumaScene.Spawn;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.Service
{
    /// <summary>
    /// 场景服务第一版实现。
    /// 负责场景加载仲裁、返回上下文栈、Pending 请求替换、取消与 SpawnPoint 传送。
    /// </summary>
    public sealed class SceneService : ISceneService
    {
        private readonly MonoBehaviour _coroutineHost;
        private readonly string _fallbackSceneName;
        private readonly int _maxReturnContextDepth;
        private readonly SceneReturnOverflowPolicy _defaultOverflowPolicy;
        private readonly bool _freezeInputDuringLoadByDefault;
        private readonly bool _logWarnings;
        private readonly List<SceneReturnContext> _returnContexts = new List<SceneReturnContext>(8);
        private readonly HashSet<string> _returnRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private ISceneCheckpointRequester _checkpointRequester;

        private SceneLoadingSnapshot _loadingSnapshot = SceneLoadingSnapshot.Empty();
        private SceneTransitionHandle _activeHandle;
        private SceneTransitionRequest _activeRequest;
        private SceneTransitionHandle _pendingHandle;
        private SceneTransitionRequest _pendingRequest;
        private Coroutine _loadCoroutine;

        public bool IsLoading => _activeHandle != null && !_activeHandle.IsDone;
        public SceneLoadingSnapshot LoadingSnapshot => _loadingSnapshot;
        public SceneReturnContext CurrentReturnContext => _returnContexts.Count > 0 ? _returnContexts[_returnContexts.Count - 1] : null;
        public int ReturnContextCount => _returnContexts.Count;

        public SceneService(
            MonoBehaviour coroutineHost,
            string fallbackSceneName,
            int maxReturnContextDepth,
            SceneReturnOverflowPolicy defaultOverflowPolicy,
            bool freezeInputDuringLoadByDefault,
            bool logWarnings,
            ISceneCheckpointRequester checkpointRequester)
        {
            _coroutineHost = coroutineHost;
            _fallbackSceneName = fallbackSceneName;
            _maxReturnContextDepth = Mathf.Max(1, maxReturnContextDepth);
            _defaultOverflowPolicy = defaultOverflowPolicy;
            _freezeInputDuringLoadByDefault = freezeInputDuringLoadByDefault;
            _logWarnings = logWarnings;
            _checkpointRequester = checkpointRequester;
        }

        public void SetCheckpointRequester(ISceneCheckpointRequester checkpointRequester)
        {
            _checkpointRequester = checkpointRequester;
        }

        public SceneTransitionHandle LoadScene(SceneTransitionRequest request)
        {
            var normalized = NormalizeRequest(request);
            var validationError = ValidateRequest(normalized, out var validationMessage);
            if (validationError != SceneLoadErrorCode.None)
            {
                return Fail(normalized?.RequestId, validationError, validationMessage);
            }

            var handle = new SceneTransitionHandle(normalized.RequestId);
            if (IsLoading)
            {
                return HandleRequestWhileLoading(normalized, handle);
            }

            StartRequest(normalized, handle);
            return handle;
        }

        public SceneTransitionHandle ReturnToPreviousScene(SceneTransitionOptions options = null)
        {
            var context = CurrentReturnContext;
            if (context == null)
            {
                return Fail(CreateRequestId(), SceneLoadErrorCode.ReturnContextMissing, "没有可返回的场景上下文。");
            }

            var requestId = CreateRequestId();
            var request = new SceneTransitionRequest
            {
                RequestId = requestId,
                Purpose = SceneLoadPurpose.Return,
                Target = new SceneTransitionTarget
                {
                    SceneName = context.ReturnSceneName,
                    LoadMode = LoadSceneMode.Single,
                    PreferredSpawnPointId = context.ReturnSpawnPointId,
                    RestorePlayerAtSpawnPoint = !string.IsNullOrWhiteSpace(context.ReturnSpawnPointId)
                },
                Options = options ?? CreateDefaultOptions(),
                ReturnPolicy = null
            };

            _returnRequestIds.Add(requestId);
            var handle = LoadScene(request);
            if (handle.IsDone && (handle.Result == null || !handle.Result.Succeeded))
            {
                _returnRequestIds.Remove(requestId);
            }

            return handle;
        }

        public SceneTransitionHandle TeleportToSpawnPoint(string spawnPointId)
        {
            var requestId = CreateRequestId();

            if (!TryApplySpawnPoint(spawnPointId, out var appliedSpawnPointId, out var errorCode, out var message))
            {
                Warn(message);
                return Fail(requestId, errorCode, message);
            }

            var activeSceneName = SceneManager.GetActiveScene().name;
            var result = SceneTransitionResult.Success(requestId);
            result.FromSceneName = activeSceneName;
            result.TargetSceneName = activeSceneName;
            result.ActivatedSceneName = activeSceneName;
            result.AppliedSpawnPointId = appliedSpawnPointId;

            var handle = new SceneTransitionHandle(requestId);
            handle.Complete(result);
            return handle;
        }

        public bool CancelLoad(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return false;
            }

            if (_pendingHandle != null && string.Equals(_pendingHandle.RequestId, requestId, StringComparison.Ordinal))
            {
                CompleteCancelled(_pendingHandle, "Pending 请求已取消。");
                _pendingHandle = null;
                _pendingRequest = null;
                return true;
            }

            if (_activeHandle != null && string.Equals(_activeHandle.RequestId, requestId, StringComparison.Ordinal) && !_activeHandle.IsDone)
            {
                _activeHandle.SetStatus(SceneLoadStatus.CancelRequested);
                _pendingHandle = null;
                _pendingRequest = null;
                UpdateLoadingSnapshot(_activeHandle, _activeRequest, SceneLoadStatus.CancelRequested, 0f, SceneLoadErrorCode.Cancelled, "ActiveLoad 已标记软取消。");
                return true;
            }

            return false;
        }

        public void ClearReturnContexts()
        {
            _returnContexts.Clear();
        }

        private SceneTransitionHandle HandleRequestWhileLoading(SceneTransitionRequest request, SceneTransitionHandle handle)
        {
            if (!request.Options.ReplacePendingRequest)
            {
                return Fail(handle.RequestId, SceneLoadErrorCode.AlreadyLoading, "当前已有场景加载正在执行。");
            }

            if (_pendingHandle != null && !_pendingHandle.IsDone)
            {
                CompleteCancelled(_pendingHandle, "被新的 Pending 请求替换。");
            }

            _pendingRequest = request;
            _pendingHandle = handle;
            handle.SetStatus(SceneLoadStatus.Pending);
            return handle;
        }

        private void StartRequest(SceneTransitionRequest request, SceneTransitionHandle handle)
        {
            if (IsSameAsActiveScene(request.Target.SceneName))
            {
                CompleteSameSceneRequest(request, handle);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(request.Target.SceneName))
            {
                CompleteFailure(handle, SceneLoadErrorCode.SceneNotFound, $"场景未加入 Build Settings 或不存在：{request.Target.SceneName}");
                return;
            }

            if (!TryPushReturnContext(request, handle, out var pushedContext, out var droppedContextId))
            {
                return;
            }

            _activeRequest = request;
            _activeHandle = handle;
            _loadCoroutine = _coroutineHost.StartCoroutine(LoadSceneRoutine(request, handle, pushedContext, droppedContextId));
        }

        private IEnumerator LoadSceneRoutine(
            SceneTransitionRequest request,
            SceneTransitionHandle handle,
            bool pushedContext,
            string droppedContextId)
        {
            handle.SetStatus(SceneLoadStatus.Loading);
            UpdateLoadingSnapshot(handle, request, SceneLoadStatus.Loading, 0f, SceneLoadErrorCode.None, null);

            var fromScene = SceneManager.GetActiveScene().name;
            var checkpointResult = default(SceneCheckpointSaveResult);
            if (request.Options != null && request.Options.RequestCheckpointSave)
            {
                yield return RequestCheckpointSaveBeforeLoad(request, handle, fromScene, result => checkpointResult = result);

                if (handle.Status == SceneLoadStatus.CancelRequested)
                {
                    RollbackPushedContext(pushedContext);
                    CompleteCancelled(handle, "场景加载在检查点保存后被取消。");
                    FinishActiveRequest();
                    yield break;
                }
            }

            AsyncOperation operation = null;
            var loadFailed = false;
            var errorMessage = default(string);

            try
            {
                operation = SceneManager.LoadSceneAsync(request.Target.SceneName, request.Target.LoadMode);
            }
            catch (Exception exception)
            {
                loadFailed = true;
                errorMessage = exception.Message;
            }

            if (operation == null)
            {
                loadFailed = true;
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = $"无法启动场景加载：{request.Target.SceneName}";
                }
            }

            if (loadFailed)
            {
                RollbackPushedContext(pushedContext);
                var usedFallbackScene = TryLoadFallbackScene();
                CompleteLoadFailedWithFallbackResult(handle, request, fromScene, errorMessage, usedFallbackScene, checkpointResult);
                FinishActiveRequest();
                yield break;
            }

            while (!operation.isDone)
            {
                UpdateLoadingSnapshot(handle, request, handle.Status, Mathf.Clamp01(operation.progress), SceneLoadErrorCode.None, null);
                yield return null;
            }

            handle.SetStatus(SceneLoadStatus.Activating);
            UpdateLoadingSnapshot(handle, request, SceneLoadStatus.Activating, 1f, SceneLoadErrorCode.None, null);

            // 等待一帧，让目标场景对象完成 Awake / OnEnable，降低 SpawnPoint / SpawnTarget 初始化时序风险。
            yield return null;

            var activeSceneName = SceneManager.GetActiveScene().name;
            var appliedSpawnPointId = default(string);
            if (request.Target.RestorePlayerAtSpawnPoint && handle.Status != SceneLoadStatus.CancelRequested)
            {
                if (!TryApplySpawnPoint(request.Target.PreferredSpawnPointId, out appliedSpawnPointId, out _, out var spawnMessage))
                {
                    Warn(spawnMessage);
                }
            }

            var result = SceneTransitionResult.Success(
                handle.RequestId,
                handle.Status == SceneLoadStatus.CancelRequested ? SceneLoadStatus.Cancelled : SceneLoadStatus.Completed);
            result.Succeeded = handle.Status != SceneLoadStatus.CancelRequested;
            result.ErrorCode = handle.Status == SceneLoadStatus.CancelRequested ? SceneLoadErrorCode.Cancelled : SceneLoadErrorCode.None;
            result.FromSceneName = fromScene;
            result.TargetSceneName = request.Target.SceneName;
            result.ActivatedSceneName = activeSceneName;
            result.AppliedSpawnPointId = appliedSpawnPointId;
            result.PushedReturnContext = pushedContext;
            result.DroppedReturnContext = !string.IsNullOrWhiteSpace(droppedContextId);
            result.DroppedReturnContextId = droppedContextId;
            ApplyCheckpointResult(result, request, checkpointResult);

            if (result.Succeeded && TryConsumeReturnRequest(handle.RequestId))
            {
                result.PoppedReturnContext = true;
            }
            else if (!result.Succeeded)
            {
                _returnRequestIds.Remove(handle.RequestId);
            }

            handle.Complete(result);
            UpdateLoadingSnapshot(handle, request, handle.Status, 1f, handle.ErrorCode, result.ErrorMessage);
            FinishActiveRequest();
        }

        private void FinishActiveRequest()
        {
            var wasCancelRequested = _activeHandle != null && _activeHandle.Status == SceneLoadStatus.Cancelled;
            _activeHandle = null;
            _activeRequest = null;
            _loadCoroutine = null;

            if (wasCancelRequested)
            {
                _pendingHandle = null;
                _pendingRequest = null;
                return;
            }

            if (_pendingRequest == null || _pendingHandle == null)
            {
                return;
            }

            var nextRequest = _pendingRequest;
            var nextHandle = _pendingHandle;
            _pendingRequest = null;
            _pendingHandle = null;
            StartRequest(nextRequest, nextHandle);
        }

        private void CompleteSameSceneRequest(SceneTransitionRequest request, SceneTransitionHandle handle)
        {
            if (request.Target.RestorePlayerAtSpawnPoint)
            {
                CompleteSameSceneTeleportRequest(request, handle);
                return;
            }

            var result = SceneTransitionResult.Success(handle.RequestId, SceneLoadStatus.Skipped);
            result.FromSceneName = SceneManager.GetActiveScene().name;
            result.TargetSceneName = request.Target.SceneName;
            result.ActivatedSceneName = result.FromSceneName;
            result.PushedReturnContext = false;
            result.ErrorMessage = "目标场景已经是当前激活场景，跳过重复加载。";
            if (request.Purpose == SceneLoadPurpose.Return && TryConsumeReturnRequest(handle.RequestId))
            {
                result.PoppedReturnContext = true;
            }
            handle.Complete(result);
        }

        private void CompleteSameSceneTeleportRequest(SceneTransitionRequest request, SceneTransitionHandle handle)
        {
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (!TryApplySpawnPoint(request.Target.PreferredSpawnPointId, out var appliedSpawnPointId, out var errorCode, out var message))
            {
                Warn(message);
                handle.Complete(SceneTransitionResult.Failure(handle.RequestId, errorCode, message));
                return;
            }

            var result = SceneTransitionResult.Success(handle.RequestId, SceneLoadStatus.Skipped);
            result.FromSceneName = activeSceneName;
            result.TargetSceneName = request.Target.SceneName;
            result.ActivatedSceneName = activeSceneName;
            result.AppliedSpawnPointId = appliedSpawnPointId;
            result.ErrorMessage = "目标场景已经是当前激活场景，已执行同场景 SpawnPoint 传送。";
            if (request.Purpose == SceneLoadPurpose.Return && TryConsumeReturnRequest(handle.RequestId))
            {
                result.PoppedReturnContext = true;
            }

            handle.Complete(result);
        }

        private bool TryPushReturnContext(SceneTransitionRequest request, SceneTransitionHandle handle, out bool pushedContext, out string droppedContextId)
        {
            pushedContext = false;
            droppedContextId = null;

            var policy = request.ReturnPolicy;
            if (policy == null || !policy.PushReturnContext)
            {
                return true;
            }

            if (policy.ClearReturnStackBeforePush)
            {
                _returnContexts.Clear();
            }

            if (_returnContexts.Count >= _maxReturnContextDepth)
            {
                var overflowPolicy = request.Options != null ? request.Options.ReturnOverflowPolicy : _defaultOverflowPolicy;
                if (overflowPolicy == SceneReturnOverflowPolicy.DropOldest)
                {
                    droppedContextId = _returnContexts[0]?.ContextId;
                    _returnContexts.RemoveAt(0);
                    Warn("返回上下文栈已满，已按 DropOldest 策略丢弃最旧上下文。");
                }
                else
                {
                    CompleteFailure(handle, SceneLoadErrorCode.ReturnStackOverflow, "返回上下文栈已满，已拒绝新的场景切换请求。");
                    return false;
                }
            }

            _returnContexts.Add(new SceneReturnContext
            {
                ContextId = Guid.NewGuid().ToString("N"),
                SourceSceneName = SceneManager.GetActiveScene().name,
                ReturnSceneName = string.IsNullOrWhiteSpace(policy.ReturnSceneName)
                    ? SceneManager.GetActiveScene().name
                    : policy.ReturnSceneName.Trim(),
                ReturnSpawnPointId = policy.ReturnSpawnPointId,
                Reason = request.Purpose.ToString(),
                CreatedUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            pushedContext = true;
            return true;
        }

        private void RollbackPushedContext(bool pushedContext)
        {
            if (pushedContext && _returnContexts.Count > 0)
            {
                _returnContexts.RemoveAt(_returnContexts.Count - 1);
            }
        }

        private SceneTransitionRequest NormalizeRequest(SceneTransitionRequest request)
        {
            if (request == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                request.RequestId = CreateRequestId();
            }

            if (request.Options == null)
            {
                request.Options = CreateDefaultOptions();
            }
            else if (!request.Options.FreezeInputDuringLoad && _freezeInputDuringLoadByDefault)
            {
                // 第一版全局默认冻结输入。调用方显式关闭时仍允许，但保留这里的默认入口。
            }

            if (request.Target != null)
            {
                if (request.Target.LoadMode == LoadSceneMode.Additive)
                {
                    Warn("NiumaScene 第一版暂不支持 Additive 加载，已强制改为 Single。");
                }

                request.Target.LoadMode = LoadSceneMode.Single;
            }

            return request;
        }

        private SceneTransitionOptions CreateDefaultOptions()
        {
            return new SceneTransitionOptions
            {
                FreezeInputDuringLoad = _freezeInputDuringLoadByDefault,
                ReplacePendingRequest = true,
                ReturnOverflowPolicy = _defaultOverflowPolicy
            };
        }

        private SceneLoadErrorCode ValidateRequest(SceneTransitionRequest request, out string message)
        {
            message = null;
            if (request == null)
            {
                message = "场景请求为空。";
                return SceneLoadErrorCode.InvalidRequest;
            }

            if (request.Target == null)
            {
                message = "场景请求缺少 Target。";
                return SceneLoadErrorCode.InvalidRequest;
            }

            if (string.IsNullOrWhiteSpace(request.Target.SceneName))
            {
                message = "目标场景名为空。";
                return SceneLoadErrorCode.EmptySceneName;
            }

            var policy = request.ReturnPolicy;
            if (policy != null && !policy.PushReturnContext)
            {
                if (!string.IsNullOrWhiteSpace(policy.ReturnSceneName) || !string.IsNullOrWhiteSpace(policy.ReturnSpawnPointId))
                {
                    message = "PushReturnContext 为 false 时不允许填写返回场景或返回点。";
                    return SceneLoadErrorCode.InvalidRequest;
                }
            }

            request.Target.SceneName = request.Target.SceneName.Trim();
            return SceneLoadErrorCode.None;
        }

        private bool IsSameAsActiveScene(string sceneName)
        {
            return string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal);
        }

        private bool TryApplySpawnPoint(
            string preferredSpawnPointId,
            out string appliedSpawnPointId,
            out SceneLoadErrorCode errorCode,
            out string message)
        {
            appliedSpawnPointId = null;
            errorCode = SceneLoadErrorCode.None;
            message = null;

            if (!SceneSpawnRegistry.TryFindSpawnPoint(preferredSpawnPointId, out var spawnPoint, out var usedDefault, out var spawnWarning))
            {
                errorCode = SceneLoadErrorCode.SpawnPointNotFound;
                message = spawnWarning;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(spawnWarning))
            {
                Warn(spawnWarning);
            }

            if (!SceneSpawnRegistry.TryFindSpawnTarget(out var spawnTarget))
            {
                errorCode = SceneLoadErrorCode.SpawnTargetMissing;
                message = "当前场景没有可用 ISceneSpawnTarget，已跳过玩家位置恢复。";
                return false;
            }

            spawnTarget.TeleportTo(spawnPoint.Position, spawnPoint.Rotation);
            appliedSpawnPointId = spawnPoint.SpawnPointId;

            if (usedDefault && !string.IsNullOrWhiteSpace(preferredSpawnPointId))
            {
                message = $"指定 SpawnPoint 不可用，已使用默认出生点：{appliedSpawnPointId}";
            }

            return true;
        }

        private void CompleteFailure(SceneTransitionHandle handle, SceneLoadErrorCode errorCode, string message)
        {
            _returnRequestIds.Remove(handle.RequestId);
            handle.Complete(SceneTransitionResult.Failure(handle.RequestId, errorCode, message));
            UpdateLoadingSnapshot(handle, _activeRequest, SceneLoadStatus.Failed, 0f, errorCode, message);
            Warn(message);
        }

        private void CompleteLoadFailedWithFallbackResult(
            SceneTransitionHandle handle,
            SceneTransitionRequest request,
            string fromScene,
            string message,
            bool usedFallbackScene,
            SceneCheckpointSaveResult checkpointResult)
        {
            _returnRequestIds.Remove(handle.RequestId);

            var result = SceneTransitionResult.Failure(handle.RequestId, SceneLoadErrorCode.LoadFailed, message);
            result.FromSceneName = fromScene;
            result.TargetSceneName = request?.Target?.SceneName;
            result.ActivatedSceneName = usedFallbackScene ? _fallbackSceneName : SceneManager.GetActiveScene().name;
            result.UsedFallbackScene = usedFallbackScene;
            result.FallbackSceneName = usedFallbackScene ? _fallbackSceneName : null;
            ApplyCheckpointResult(result, request, checkpointResult);

            handle.Complete(result);
            UpdateLoadingSnapshot(handle, request, SceneLoadStatus.Failed, 0f, SceneLoadErrorCode.LoadFailed, message);
            Warn(message);
        }

        private IEnumerator RequestCheckpointSaveBeforeLoad(
            SceneTransitionRequest request,
            SceneTransitionHandle handle,
            string fromScene,
            Action<SceneCheckpointSaveResult> onCompleted)
        {
            if (_checkpointRequester == null)
            {
                const string missingMessage = "已请求检查点保存，但未配置 ISceneCheckpointRequester。场景切换将继续执行。";
                Warn(missingMessage);
                onCompleted?.Invoke(SceneCheckpointSaveResult.Fail(missingMessage));
                yield break;
            }

            var checkpointRequest = new SceneCheckpointSaveRequest
            {
                RequestId = handle.RequestId,
                Reason = request.Purpose.ToString(),
                SourceSceneName = fromScene,
                TargetSceneName = request.Target?.SceneName,
                Purpose = request.Purpose,
                CreatedUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Task<SceneCheckpointSaveResult> task;
            try
            {
                task = _checkpointRequester.RequestCheckpointSaveAsync(checkpointRequest);
            }
            catch (Exception exception)
            {
                var message = $"发起检查点保存失败：{exception.Message}";
                Warn(message);
                onCompleted?.Invoke(SceneCheckpointSaveResult.Fail(message));
                yield break;
            }

            if (task == null)
            {
                const string nullTaskMessage = "检查点保存请求返回空 Task，场景切换将继续执行。";
                Warn(nullTaskMessage);
                onCompleted?.Invoke(SceneCheckpointSaveResult.Fail(nullTaskMessage));
                yield break;
            }

            while (!task.IsCompleted)
            {
                UpdateLoadingSnapshot(handle, request, SceneLoadStatus.Loading, 0f, SceneLoadErrorCode.None, null);
                yield return null;
            }

            SceneCheckpointSaveResult result;
            if (task.IsFaulted)
            {
                var message = $"检查点保存异常：{task.Exception?.GetBaseException().Message}";
                Warn(message);
                result = SceneCheckpointSaveResult.Fail(message);
            }
            else if (task.IsCanceled)
            {
                const string message = "检查点保存被取消。";
                Warn(message);
                result = SceneCheckpointSaveResult.Fail(message);
            }
            else
            {
                result = task.Result ?? SceneCheckpointSaveResult.Fail("检查点保存返回空结果。");
                if (!result.Succeeded)
                {
                    Warn($"检查点保存失败：{result.Message}");
                }
            }

            onCompleted?.Invoke(result);
        }

        private static void ApplyCheckpointResult(
            SceneTransitionResult transitionResult,
            SceneTransitionRequest request,
            SceneCheckpointSaveResult checkpointResult)
        {
            if (transitionResult == null || request?.Options == null || !request.Options.RequestCheckpointSave)
            {
                return;
            }

            transitionResult.RequestedCheckpointSave = true;
            transitionResult.CheckpointSaveSucceeded = checkpointResult != null && checkpointResult.Succeeded;
            transitionResult.CheckpointSaveSlotId = checkpointResult?.SlotId;
            transitionResult.CheckpointSaveMessage = checkpointResult?.Message;
        }

        private SceneTransitionHandle Fail(string requestId, SceneLoadErrorCode errorCode, string message)
        {
            return SceneTransitionHandle.Failed(string.IsNullOrWhiteSpace(requestId) ? CreateRequestId() : requestId, errorCode, message);
        }

        private void CompleteCancelled(SceneTransitionHandle handle, string message)
        {
            _returnRequestIds.Remove(handle.RequestId);
            handle.Complete(SceneTransitionResult.Failure(handle.RequestId, SceneLoadErrorCode.Cancelled, message, SceneLoadStatus.Cancelled));
        }

        private bool TryConsumeReturnRequest(string requestId)
        {
            if (!_returnRequestIds.Remove(requestId))
            {
                return false;
            }

            if (_returnContexts.Count <= 0)
            {
                return false;
            }

            _returnContexts.RemoveAt(_returnContexts.Count - 1);
            return true;
        }

        private void UpdateLoadingSnapshot(
            SceneTransitionHandle handle,
            SceneTransitionRequest request,
            SceneLoadStatus status,
            float progress,
            SceneLoadErrorCode errorCode,
            string errorMessage)
        {
            _loadingSnapshot = new SceneLoadingSnapshot
            {
                RequestId = handle?.RequestId,
                Status = status,
                TargetSceneName = request?.Target?.SceneName,
                Progress = progress,
                IsLoading = status == SceneLoadStatus.Pending
                            || status == SceneLoadStatus.Loading
                            || status == SceneLoadStatus.Activating
                            || status == SceneLoadStatus.CancelRequested,
                FreezeInputDuringLoad = request?.Options != null && request.Options.FreezeInputDuringLoad,
                ShowLoadingUI = request?.Options != null && request.Options.ShowLoadingUI,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

        private bool TryLoadFallbackScene()
        {
            if (string.IsNullOrWhiteSpace(_fallbackSceneName))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(_fallbackSceneName))
            {
                Warn($"fallback 场景不存在或未加入 Build Settings：{_fallbackSceneName}");
                return false;
            }

            try
            {
                SceneManager.LoadScene(_fallbackSceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                Warn($"fallback 场景加载失败：{exception.Message}");
                return false;
            }
        }

        private static string CreateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private void Warn(string message)
        {
            if (_logWarnings && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[NiumaScene] {message}");
            }
        }
    }
}
