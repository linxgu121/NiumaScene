using NiumaScene.Checkpoint;
using NiumaScene.Data;
using NiumaScene.Enum;
using NiumaScene.Service;
using UnityEngine;

namespace NiumaScene.Controller
{
    /// <summary>
    /// NiumaScene 根控制器。
    /// 负责创建 SceneService，并向场景内其他组件提供统一的场景切换入口。
    /// </summary>
    public sealed class NiumaSceneController : MonoBehaviour
    {
        [Header("失败兜底")]
        [Tooltip("系统级 fallback 场景名。LoadFailed 且旧场景不可用时可尝试加载该场景；为空则只记录错误。")]
        [SerializeField] private string fallbackSceneName;

        [Header("返回栈")]
        [Tooltip("返回上下文栈最大深度。正式流程建议保持 8 左右，避免返回链过深难以理解。")]
        [SerializeField] private int maxReturnContextDepth = 8;

        [Tooltip("返回上下文栈满时的默认处理策略。正式剧情、建筑、MiniGame 返回链建议使用 RejectNew。")]
        [SerializeField] private SceneReturnOverflowPolicy defaultReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew;

        [Header("加载行为")]
        [Tooltip("默认是否在场景加载期间冻结输入。第二阶段只保留配置，第四阶段接入具体输入冻结桥接。")]
        [SerializeField] private bool freezeInputDuringLoadByDefault = true;

        [Tooltip("默认场景切换是否显示 Loading UI。入口脚本也可以在 SceneTransitionOptions 中单次覆盖。")]
        [SerializeField] private bool showLoadingUIByDefault = true;

        [Tooltip("是否输出场景模块警告日志。")]
        [SerializeField] private bool logWarnings = true;

        [Header("检查点保存")]
        [Tooltip("检查点保存请求脚本。通常拖 NiumaSceneSaveCheckpointRequester；没有接入 NiumaSave 检查点流程时可留空。")]
        [SerializeField] private MonoBehaviour checkpointRequesterProvider;

        [Tooltip("未手动绑定检查点请求器时，是否自动查找场景中的 ISceneCheckpointRequester。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindCheckpointRequester = true;

        private SceneService _sceneService;
        private ISceneCheckpointRequester _checkpointRequester;

        public ISceneService SceneService
        {
            get
            {
                EnsureInitialized();
                return _sceneService;
            }
        }

        public bool IsInitialized => _sceneService != null;
        public bool IsLoading => _sceneService != null && _sceneService.IsLoading;
        public SceneLoadingSnapshot LoadingSnapshot => _sceneService != null ? _sceneService.LoadingSnapshot : SceneLoadingSnapshot.Empty();

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_sceneService != null)
            {
                return;
            }

            _sceneService = new SceneService(
                this,
                fallbackSceneName,
                maxReturnContextDepth,
                defaultReturnOverflowPolicy,
                freezeInputDuringLoadByDefault,
                logWarnings,
                ResolveCheckpointRequester(false));
        }

        /// <summary>
        /// 设置检查点保存请求器。
        /// 上层启动器或测试场景可用它显式注入保存桥接对象。
        /// </summary>
        public void SetCheckpointRequester(ISceneCheckpointRequester checkpointRequester)
        {
            _checkpointRequester = checkpointRequester;
            _sceneService?.SetCheckpointRequester(checkpointRequester);
        }

        public SceneTransitionHandle LoadScene(SceneTransitionRequest request)
        {
            EnsureInitialized();
            return _sceneService.LoadScene(request);
        }

        public SceneTransitionHandle LoadScene(string sceneName, SceneLoadPurpose purpose = SceneLoadPurpose.None)
        {
            return LoadScene(new SceneTransitionRequest
            {
                Purpose = purpose,
                Target = new SceneTransitionTarget(sceneName),
                Options = CreateDefaultOptions()
            });
        }

        public SceneTransitionHandle ReturnToPreviousScene(SceneTransitionOptions options = null)
        {
            EnsureInitialized();
            return _sceneService.ReturnToPreviousScene(options);
        }

        public SceneTransitionHandle TeleportToSpawnPoint(string spawnPointId)
        {
            EnsureInitialized();
            return _sceneService.TeleportToSpawnPoint(spawnPointId);
        }

        public bool CancelLoad(string requestId)
        {
            EnsureInitialized();
            return _sceneService.CancelLoad(requestId);
        }

        public void ClearReturnContexts()
        {
            EnsureInitialized();
            _sceneService.ClearReturnContexts();
        }

        private SceneTransitionOptions CreateDefaultOptions()
        {
            return new SceneTransitionOptions
            {
                FreezeInputDuringLoad = freezeInputDuringLoadByDefault,
                ShowLoadingUI = showLoadingUIByDefault,
                ReplacePendingRequest = true,
                ReturnOverflowPolicy = defaultReturnOverflowPolicy
            };
        }

        private ISceneCheckpointRequester ResolveCheckpointRequester(bool warn)
        {
            if (_checkpointRequester != null)
            {
                return _checkpointRequester;
            }

            if (checkpointRequesterProvider != null)
            {
                _checkpointRequester = checkpointRequesterProvider as ISceneCheckpointRequester;
                if (_checkpointRequester == null && warn && logWarnings)
                {
                    Debug.LogWarning("[NiumaSceneController] CheckpointRequester 绑定的不是检查点保存请求脚本，请拖 NiumaSceneSaveCheckpointRequester；未接入检查点保存时可留空。", this);
                }

                return _checkpointRequester;
            }

            if (!autoFindCheckpointRequester)
            {
                return null;
            }

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISceneCheckpointRequester requester)
                {
                    checkpointRequesterProvider = behaviours[i];
                    _checkpointRequester = requester;
                    return _checkpointRequester;
                }
            }

            return null;
        }
    }
}
