using System;
using System.Collections.Generic;
using NiumaScene.Controller;
using NiumaScene.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.Loading
{
    /// <summary>
    /// 场景加载状态桥接层。
    /// 负责把 SceneService 的加载快照推给 Loading UI，并把输入冻结请求转发给各业务模块适配器。
    /// </summary>
    public sealed class SceneLoadingStateBridge : MonoBehaviour
    {
        private const string DefaultBlockReason = "SceneLoading";

        [Header("场景控制器")]
        [Tooltip("NiumaScene 根控制器。正式核心场景建议拖 SceneRoot 上的 NiumaSceneController；为空时可自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("Scene Controller 未绑定时，是否自动查找场景中的 NiumaSceneController。正式核心场景建议手动绑定后关闭。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Header("加载表现")]
        [Tooltip("Loading 接收脚本。UI Toolkit 正式方案拖 SceneLoadingToolkitBridge；旧 UGUI 测试场景才拖 SceneLoadingPanelBridge；为空时只冻结/解冻输入，不显示 Loading UI。")]
        [SerializeField] private MonoBehaviour loadingReceiverProvider;

        [Header("输入冻结")]
        [Tooltip("需要冻结输入的目标脚本列表。玩家/交互系统常驻核心场景时可手动拖 TPCSceneInputBlockTarget / InteractSceneInputBlockTarget；它们在业务场景时请开启自动查找，不要跨场景硬拖。")]
        [SerializeField] private MonoBehaviour[] inputBlockTargetProviders = Array.Empty<MonoBehaviour>();

        [Tooltip("是否自动查找当前已加载场景中的输入冻结适配器。玩家或交互物体在业务场景中时建议开启；核心场景常驻玩家且已手动绑定时可关闭。")]
        [SerializeField] private bool autoFindInputBlockTargets = true;

        [Tooltip("输入冻结原因。适配器应使用该原因只解除本桥接添加的阻塞，避免覆盖对话、死亡、菜单等其他系统的禁用状态。")]
        [SerializeField] private string inputBlockReason = DefaultBlockReason;

        [Tooltip("加载结束或组件禁用时，是否解除本桥接添加的输入冻结。建议开启。")]
        [SerializeField] private bool unblockWhenLoadingEnds = true;

        [Header("调试")]
        [Tooltip("依赖缺失、接口绑定错误或自动查找失败时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly List<ISceneInputBlockTarget> _inputBlockTargets = new List<ISceneInputBlockTarget>(4);
        private ISceneLoadingReceiver _loadingReceiver;
        private bool _isInputBlockedByBridge;
        private LoadingSnapshotSignature _lastSignature;
        private bool _hasSignature;

        private void Awake()
        {
            ResolveReferences(true);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveReferences(false);
            ForceApplySnapshot();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (_isInputBlockedByBridge && unblockWhenLoadingEnds)
            {
                ApplyInputBlocked(false);
            }

            _hasSignature = false;
        }

        private void LateUpdate()
        {
            if (!EnsureController())
            {
                return;
            }

            var snapshot = sceneController.LoadingSnapshot;
            var signature = LoadingSnapshotSignature.From(snapshot);
            if (_hasSignature && signature.Equals(_lastSignature))
            {
                return;
            }

            _lastSignature = signature;
            _hasSignature = true;
            ApplySnapshot(snapshot);
        }

        public void SetSceneController(NiumaSceneController controller)
        {
            sceneController = controller;
            ForceApplySnapshot();
        }

        public void RebuildTargets()
        {
            ResolveReferences(true);
            ForceApplySnapshot();
        }

        private void ForceApplySnapshot()
        {
            _hasSignature = false;
            LateUpdate();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveReferences(false);
            if (_isInputBlockedByBridge)
            {
                ApplyInputBlocked(true);
            }
        }

        private bool EnsureController()
        {
            if (sceneController != null)
            {
                return true;
            }

            if (!autoFindSceneController)
            {
                Warn("未绑定 NiumaSceneController，无法读取加载状态。");
                return false;
            }

            sceneController = FindAnyObjectByType<NiumaSceneController>();
            if (sceneController == null)
            {
                Warn("自动查找 NiumaSceneController 失败，无法读取加载状态。");
            }

            return sceneController != null;
        }

        private void ResolveReferences(bool logInvalid)
        {
            _loadingReceiver = loadingReceiverProvider as ISceneLoadingReceiver;
            if (loadingReceiverProvider != null && _loadingReceiver == null)
            {
                Warn("Loading Receiver 绑定的不是加载表现脚本。UI Toolkit 正式方案请拖 SceneLoadingToolkitBridge；旧 UGUI 测试场景才拖 SceneLoadingPanelBridge。", logInvalid);
            }

            _inputBlockTargets.Clear();
            if (inputBlockTargetProviders != null)
            {
                for (var i = 0; i < inputBlockTargetProviders.Length; i++)
                {
                    var provider = inputBlockTargetProviders[i];
                    if (provider == null)
                    {
                        continue;
                    }

                    if (provider is ISceneInputBlockTarget target)
                    {
                        AddInputBlockTarget(target);
                        continue;
                    }

                    Warn($"Input Block Target Providers[{i}] 绑定的不是输入冻结适配脚本。玩家控制拖 TPCSceneInputBlockTarget，交互输入拖 InteractSceneInputBlockTarget。", logInvalid);
                }
            }

            if (autoFindInputBlockTargets)
            {
                AutoFindInputBlockTargets();
            }
        }

        private void AutoFindInputBlockTargets()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISceneInputBlockTarget target)
                {
                    AddInputBlockTarget(target);
                }
            }
        }

        private void AddInputBlockTarget(ISceneInputBlockTarget target)
        {
            if (target == null)
            {
                return;
            }

            for (var i = 0; i < _inputBlockTargets.Count; i++)
            {
                if (ReferenceEquals(_inputBlockTargets[i], target))
                {
                    return;
                }
            }

            _inputBlockTargets.Add(target);
        }

        private void ApplySnapshot(SceneLoadingSnapshot snapshot)
        {
            var shouldBlockInput = snapshot != null && snapshot.IsLoading && snapshot.FreezeInputDuringLoad;
            if (shouldBlockInput != _isInputBlockedByBridge)
            {
                if (shouldBlockInput || unblockWhenLoadingEnds)
                {
                    ApplyInputBlocked(shouldBlockInput);
                }
            }

            _loadingReceiver?.ApplySceneLoadingSnapshot(snapshot ?? SceneLoadingSnapshot.Empty());
        }

        private void ApplyInputBlocked(bool blocked)
        {
            var reason = string.IsNullOrWhiteSpace(inputBlockReason) ? DefaultBlockReason : inputBlockReason.Trim();
            for (var i = 0; i < _inputBlockTargets.Count; i++)
            {
                _inputBlockTargets[i]?.SetSceneInputBlocked(blocked, reason);
            }

            _isInputBlockedByBridge = blocked;
        }

        private void Warn(string message, bool force = true)
        {
            if (logWarnings && force && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[SceneLoadingStateBridge] {message}", this);
            }
        }

        private readonly struct LoadingSnapshotSignature : IEquatable<LoadingSnapshotSignature>
        {
            private readonly string _requestId;
            private readonly string _targetSceneName;
            private readonly string _errorMessage;
            private readonly int _status;
            private readonly int _errorCode;
            private readonly int _progressPermyriad;
            private readonly bool _isLoading;
            private readonly bool _freezeInput;
            private readonly bool _showLoadingUi;

            private LoadingSnapshotSignature(SceneLoadingSnapshot snapshot)
            {
                _requestId = snapshot?.RequestId;
                _targetSceneName = snapshot?.TargetSceneName;
                _errorMessage = snapshot?.ErrorMessage;
                _status = snapshot != null ? (int)snapshot.Status : 0;
                _errorCode = snapshot != null ? (int)snapshot.ErrorCode : 0;
                _progressPermyriad = snapshot != null ? Mathf.RoundToInt(snapshot.Progress * 10000f) : 0;
                _isLoading = snapshot != null && snapshot.IsLoading;
                _freezeInput = snapshot != null && snapshot.FreezeInputDuringLoad;
                _showLoadingUi = snapshot != null && snapshot.ShowLoadingUI;
            }

            public static LoadingSnapshotSignature From(SceneLoadingSnapshot snapshot)
            {
                return new LoadingSnapshotSignature(snapshot);
            }

            public bool Equals(LoadingSnapshotSignature other)
            {
                return string.Equals(_requestId, other._requestId, StringComparison.Ordinal)
                       && string.Equals(_targetSceneName, other._targetSceneName, StringComparison.Ordinal)
                       && string.Equals(_errorMessage, other._errorMessage, StringComparison.Ordinal)
                       && _status == other._status
                       && _errorCode == other._errorCode
                       && _progressPermyriad == other._progressPermyriad
                       && _isLoading == other._isLoading
                       && _freezeInput == other._freezeInput
                       && _showLoadingUi == other._showLoadingUi;
            }
        }
    }
}
