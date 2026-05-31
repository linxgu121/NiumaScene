using NiumaScene.Spawn;
using NiumaTPC.Module;
using UnityEngine;

namespace NiumaScene.TPCBridge
{
    /// <summary>
    /// NiumaTPC 出生点目标适配器。
    /// 将 NiumaScene 的通用传送请求转发给 PlayerModuleController，避免 Scene 模块本体直接依赖 TPC。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TPCSceneSpawnTarget : MonoBehaviour, ISceneSpawnTarget
    {
        [Header("玩家控制器")]
        [Tooltip("玩家模块控制器。为空时会在当前物体、父物体和子物体中自动查找。")]
        [SerializeField] private PlayerModuleController playerController;

        [Tooltip("启用时是否自动查找 PlayerModuleController。正式场景建议手动绑定，避免多玩家或多角色场景找错对象。")]
        [SerializeField] private bool autoResolvePlayerController = true;

        [Header("调试")]
        [Tooltip("缺少 PlayerModuleController 时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private void OnEnable()
        {
            ResolvePlayerController(false);
            SceneSpawnRegistry.RegisterSpawnTarget(this);
        }

        private void OnDisable()
        {
            SceneSpawnRegistry.UnregisterSpawnTarget(this);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (!ResolvePlayerController(true))
            {
                return;
            }

            playerController.Teleport(position, rotation);
        }

        private bool ResolvePlayerController(bool logIfMissing)
        {
            if (playerController != null)
            {
                return true;
            }

            if (!autoResolvePlayerController)
            {
                Warn("未绑定 PlayerModuleController，无法执行场景出生点传送。", logIfMissing);
                return false;
            }

            playerController = GetComponent<PlayerModuleController>()
                               ?? GetComponentInParent<PlayerModuleController>()
                               ?? GetComponentInChildren<PlayerModuleController>(true);

            if (playerController == null)
            {
                Warn("自动查找 PlayerModuleController 失败，无法执行场景出生点传送。", logIfMissing);
            }

            return playerController != null;
        }

        private void Warn(string message, bool shouldLog)
        {
            if (logWarnings && shouldLog && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[TPCSceneSpawnTarget] {message}", this);
            }
        }
    }
}
