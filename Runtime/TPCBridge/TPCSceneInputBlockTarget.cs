using NiumaScene.Loading;
using NiumaTPC.Module;
using UnityEngine;

namespace NiumaScene.TPCBridge
{
    /// <summary>
    /// TPC 输入冻结适配器。
    /// 只响应 NiumaScene 的加载阻塞请求，不直接参与场景切换逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TPCSceneInputBlockTarget : MonoBehaviour, ISceneInputBlockTarget
    {
        [Header("玩家控制器")]
        [Tooltip("玩家模块控制器。为空时会在当前物体、父物体和子物体中自动查找。")]
        [SerializeField] private PlayerModuleController playerController;

        [Tooltip("启用时是否自动查找 PlayerModuleController。正式场景建议手动绑定，避免多玩家或多角色场景找错对象。")]
        [SerializeField] private bool autoResolvePlayerController = true;

        [Header("调试")]
        [Tooltip("缺少 PlayerModuleController 时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private void Awake()
        {
            ResolvePlayerController(false);
        }

        public void SetSceneInputBlocked(bool blocked, string reason)
        {
            if (!ResolvePlayerController(true))
            {
                return;
            }

            if (blocked)
            {
                playerController.DisableControl(reason);
            }
            else
            {
                playerController.EnableControl(reason);
            }
        }

        private bool ResolvePlayerController(bool logIfMissing)
        {
            if (playerController != null)
            {
                return true;
            }

            if (!autoResolvePlayerController)
            {
                Warn("未绑定 PlayerModuleController，无法处理场景加载输入冻结。", logIfMissing);
                return false;
            }

            playerController = GetComponent<PlayerModuleController>()
                               ?? GetComponentInParent<PlayerModuleController>()
                               ?? GetComponentInChildren<PlayerModuleController>(true);

            if (playerController == null)
            {
                Warn("自动查找 PlayerModuleController 失败，无法处理场景加载输入冻结。", logIfMissing);
            }

            return playerController != null;
        }

        private void Warn(string message, bool shouldLog)
        {
            if (logWarnings && shouldLog && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[TPCSceneInputBlockTarget] {message}", this);
            }
        }
    }
}
