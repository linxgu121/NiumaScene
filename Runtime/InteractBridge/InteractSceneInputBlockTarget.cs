using System.Collections.Generic;
using NiumaInteract.Core;
using NiumaScene.Loading;
using UnityEngine;

namespace NiumaScene.InteractBridge
{
    /// <summary>
    /// 交互模块输入冻结适配器。
    /// 通过 reason 集合避免本桥接层解除输入时覆盖自己仍持有的其他场景加载阻塞。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractSceneInputBlockTarget : MonoBehaviour, ISceneInputBlockTarget
    {
        [Header("交互控制器")]
        [Tooltip("NiumaInteract 根控制器。为空时会在当前物体、父物体和子物体中自动查找。")]
        [SerializeField] private NiumaInteractionController interactionController;

        [Tooltip("启用时是否自动查找 NiumaInteractionController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoResolveInteractionController = true;

        [Tooltip("阻塞输入时是否清空已缓存的按住输入，避免场景切换结束后误触发交互。")]
        [SerializeField] private bool clearBufferedInputOnBlock = true;

        [Header("调试")]
        [Tooltip("缺少 NiumaInteractionController 时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly HashSet<string> _blockReasons = new HashSet<string>();

        private void Awake()
        {
            ResolveInteractionController(false);
        }

        public void SetSceneInputBlocked(bool blocked, string reason)
        {
            if (!ResolveInteractionController(true))
            {
                return;
            }

            var safeReason = string.IsNullOrWhiteSpace(reason) ? "SceneLoading" : reason;
            if (blocked)
            {
                _blockReasons.Add(safeReason);
                interactionController.SetInputBlocked(true, clearBufferedInputOnBlock);
                return;
            }

            _blockReasons.Remove(safeReason);
            if (_blockReasons.Count == 0)
            {
                interactionController.SetInputBlocked(false, true);
            }
        }

        private bool ResolveInteractionController(bool logIfMissing)
        {
            if (interactionController != null)
            {
                return true;
            }

            if (!autoResolveInteractionController)
            {
                Warn("未绑定 NiumaInteractionController，无法处理场景加载交互冻结。", logIfMissing);
                return false;
            }

            interactionController = GetComponent<NiumaInteractionController>()
                                    ?? GetComponentInParent<NiumaInteractionController>()
                                    ?? GetComponentInChildren<NiumaInteractionController>(true);

            if (interactionController == null)
            {
                Warn("自动查找 NiumaInteractionController 失败，无法处理场景加载交互冻结。", logIfMissing);
            }

            return interactionController != null;
        }

        private void Warn(string message, bool shouldLog)
        {
            if (logWarnings && shouldLog && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[InteractSceneInputBlockTarget] {message}", this);
            }
        }
    }
}
