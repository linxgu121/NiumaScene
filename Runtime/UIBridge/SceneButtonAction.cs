using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using UnityEngine;

namespace NiumaScene.UIBridge
{
    /// <summary>
    /// 场景按钮动作桥接。
    /// 用于 Unity Button、对话选项 UnityEvent 等 Inspector 事件，把无返回值按钮事件转发给 NiumaSceneController。
    /// </summary>
    public sealed class SceneButtonAction : MonoBehaviour
    {
        [Header("场景控制器")]
        [Tooltip("场景根控制器。正式场景建议手动绑定 SceneRoot 上的 NiumaSceneController。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("未手动绑定 SceneController 时，是否在场景中自动查找。正式场景建议关闭并手动绑定。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Header("加载目标")]
        [Tooltip("按钮要进入的目标场景名，必须与 Build Settings 中的场景名一致。")]
        [SerializeField] private string targetSceneName;

        [Tooltip("进入目标场景后优先使用的出生点 ID。为空时不指定出生点。")]
        [SerializeField] private string targetSpawnPointId;

        [Tooltip("进入目标场景后是否把玩家放到目标出生点。")]
        [SerializeField] private bool restorePlayerAtTargetSpawnPoint;

        [Tooltip("本次场景切换用途。MiniGame、建筑内部、主线剧情等可用不同 Purpose 方便调试和后续扩展。")]
        [SerializeField] private SceneLoadPurpose purpose = SceneLoadPurpose.None;

        [Header("返回上下文")]
        [Tooltip("是否把当前场景压入返回栈。进入 MiniGame、建筑内部、临时副本时通常开启。")]
        [SerializeField] private bool pushReturnContext;

        [Tooltip("返回时使用的场景名。为空时由场景服务使用当前激活场景名。")]
        [SerializeField] private string returnSceneName;

        [Tooltip("返回时使用的出生点 ID。例如 NPC 面前、小游戏入口、建筑门口。")]
        [SerializeField] private string returnSpawnPointId;

        [Tooltip("压入新返回上下文前是否清空旧返回栈。主菜单进入游戏等顶层流程可开启。")]
        [SerializeField] private bool clearReturnStackBeforePush;

        [Header("加载选项")]
        [Tooltip("加载期间是否冻结玩家输入。")]
        [SerializeField] private bool freezeInputDuringLoad = true;

        [Tooltip("加载期间是否显示 Loading UI。")]
        [SerializeField] private bool showLoadingUI = true;

        [Tooltip("发起切换时是否请求检查点保存。")]
        [SerializeField] private bool requestCheckpointSave;

        [Tooltip("已有 Pending 请求时，是否用本次请求替换旧 Pending。")]
        [SerializeField] private bool replacePendingRequest = true;

        [Header("调试")]
        [Tooltip("配置缺失或场景控制器缺失时输出警告。")]
        [SerializeField] private bool logWarnings = true;

        /// <summary>
        /// 按组件配置加载目标场景。
        /// Unity Button OnClick 推荐绑定该方法。
        /// </summary>
        public void LoadConfiguredScene()
        {
            if (!ResolveSceneController())
            {
                Warn("无法加载场景：未绑定 NiumaSceneController。");
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Warn("无法加载场景：TargetSceneName 为空。");
                return;
            }

            sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = purpose,
                Target = new SceneTransitionTarget
                {
                    SceneName = targetSceneName,
                    RestorePlayerAtSpawnPoint = restorePlayerAtTargetSpawnPoint,
                    PreferredSpawnPointId = targetSpawnPointId
                },
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = pushReturnContext,
                    ReturnSceneName = returnSceneName,
                    ReturnSpawnPointId = returnSpawnPointId,
                    ClearReturnStackBeforePush = clearReturnStackBeforePush
                },
                Options = CreateOptions()
            });
        }

        /// <summary>
        /// 返回上一个场景。
        /// Unity Button OnClick 可用于“退出小游戏”“离开建筑”等按钮。
        /// </summary>
        public void ReturnToPreviousScene()
        {
            if (!ResolveSceneController())
            {
                Warn("无法返回场景：未绑定 NiumaSceneController。");
                return;
            }

            sceneController.ReturnToPreviousScene(CreateOptions());
        }

        /// <summary>
        /// 传送到当前场景内指定出生点。
        /// Unity Button OnClick 可用于同场景传送或调试按钮。
        /// </summary>
        public void TeleportToConfiguredSpawnPoint()
        {
            if (!ResolveSceneController())
            {
                Warn("无法传送：未绑定 NiumaSceneController。");
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSpawnPointId))
            {
                Warn("无法传送：TargetSpawnPointId 为空。");
                return;
            }

            sceneController.TeleportToSpawnPoint(targetSpawnPointId);
        }

        /// <summary>
        /// 清空返回栈。
        /// 一般只在主菜单、重新开始游戏等顶层流程中使用。
        /// </summary>
        public void ClearReturnContexts()
        {
            if (!ResolveSceneController())
            {
                Warn("无法清空返回栈：未绑定 NiumaSceneController。");
                return;
            }

            sceneController.ClearReturnContexts();
        }

        private SceneTransitionOptions CreateOptions()
        {
            return new SceneTransitionOptions
            {
                FreezeInputDuringLoad = freezeInputDuringLoad,
                ShowLoadingUI = showLoadingUI,
                RequestCheckpointSave = requestCheckpointSave,
                ReplacePendingRequest = replacePendingRequest
            };
        }

        private bool ResolveSceneController()
        {
            if (sceneController != null)
                return true;

            if (!autoFindSceneController)
                return false;

#if UNITY_2023_1_OR_NEWER
            sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
            sceneController = FindObjectOfType<NiumaSceneController>();
#endif
            return sceneController != null;
        }

        private void Warn(string message)
        {
            if (logWarnings)
                Debug.LogWarning($"[NiumaScene] {message}", this);
        }
    }
}
