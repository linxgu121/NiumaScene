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

        [Tooltip("是否输出场景模块警告日志。")]
        [SerializeField] private bool logWarnings = true;

        private SceneService _sceneService;

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
                logWarnings);
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
                ReplacePendingRequest = true,
                ReturnOverflowPolicy = defaultReturnOverflowPolicy
            };
        }
    }
}
