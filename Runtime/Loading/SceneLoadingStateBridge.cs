using System;
using System.Collections.Generic;
using NiumaScene.Controller;
using NiumaScene.Data;
using UnityEngine;

namespace NiumaScene.Loading
{
    /// <summary>
    /// 场景加载状态桥接层。
    /// 负责把 SceneService 的数据快照推给 Loading UI，并把输入冻结请求转发给各业务模块适配器。
    /// </summary>
    public sealed class SceneLoadingStateBridge : MonoBehaviour
    {
        private const string DefaultBlockReason = "SceneLoading";

        [Header("场景控制器")]
        [Tooltip("NiumaScene 根控制器。为空时会在场景中自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("SceneController 未绑定时，是否自动查找场景中的 NiumaSceneController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Header("加载表现")]
        [Tooltip("实现 ISceneLoadingReceiver 的组件，例如 LoadingPanel 桥接脚本。为空时只处理输入冻结。")]
        [SerializeField] private MonoBehaviour loadingReceiverProvider;

        [Header("输入冻结")]
        [Tooltip("实现 ISceneInputBlockTarget 的组件列表，例如 TPCSceneInputBlockTarget、InteractSceneInputBlockTarget。")]
        [SerializeField] private MonoBehaviour[] inputBlockTargetProviders = Array.Empty<MonoBehaviour>();

        [Tooltip("输入冻结原因。适配器应使用该原因只解除自己加上的阻塞。")]
        [SerializeField] private string inputBlockReason = DefaultBlockReason;

        [Tooltip("加载结束或组件禁用时，是否解除本桥接层加上的输入冻结。使用支持 reason 的适配器时建议开启。")]
        [SerializeField] private bool unblockWhenLoadingEnds = true;

        [Header("调试")]
        [Tooltip("依赖缺失或组件未实现接口时是否输出警告。")]
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
            ResolveReferences(false);
            ForceApplySnapshot();
        }

        private void OnDisable()
        {
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

        /// <summary>
        /// 外部手动指定 SceneController。
        /// 常用于 Bootstrap 初始化或测试场景中显式注入。
        /// </summary>
        public void SetSceneController(NiumaSceneController controller)
        {
            sceneController = controller;
            ForceApplySnapshot();
        }

        /// <summary>
        /// 重新解析 Receiver 和输入阻塞目标。
        /// 当运行时动态替换 UI 或输入适配器时调用。
        /// </summary>
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
                Warn("LoadingReceiverProvider 未实现 ISceneLoadingReceiver。", logInvalid);
            }

            _inputBlockTargets.Clear();
            if (inputBlockTargetProviders == null)
            {
                return;
            }

            for (var i = 0; i < inputBlockTargetProviders.Length; i++)
            {
                var provider = inputBlockTargetProviders[i];
                if (provider == null)
                {
                    continue;
                }

                if (provider is ISceneInputBlockTarget target)
                {
                    _inputBlockTargets.Add(target);
                    continue;
                }

                Warn($"InputBlockTargetProviders[{i}] 未实现 ISceneInputBlockTarget。", logInvalid);
            }
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
