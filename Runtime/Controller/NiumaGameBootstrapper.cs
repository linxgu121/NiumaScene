using NiumaScene.Data;
using NiumaScene.Enum;
using UnityEngine;

namespace NiumaScene.Controller
{
    /// <summary>
    /// 全局 Bootstrap 启动脚本。
    /// 挂在 BootstrapRoot 上，负责常驻生命周期、防重复和首场景加载。
    /// </summary>
    public sealed class NiumaGameBootstrapper : MonoBehaviour
    {
        private static NiumaGameBootstrapper _instance;

        [Header("常驻根")]
        [Tooltip("需要 DontDestroyOnLoad 的根物体。为空时使用当前 GameObject。")]
        [SerializeField] private GameObject bootstrapRoot;

        [Tooltip("是否让 BootstrapRoot 跨场景常驻。")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Tooltip("检测到重复 Bootstrap 时，是否销毁新的 BootstrapRoot。")]
        [SerializeField] private bool destroyDuplicateBootstrap = true;

        [Header("场景入口")]
        [Tooltip("启动后自动加载的第一个正式游戏场景。为空则不自动加载。")]
        [SerializeField] private string firstSceneName;

        [Tooltip("是否在 Start 时自动加载 firstSceneName。")]
        [SerializeField] private bool loadFirstSceneOnStart = true;

        [Tooltip("加载首场景时是否清空返回上下文栈。")]
        [SerializeField] private bool clearReturnStackBeforeFirstScene = true;

        [Header("引用")]
        [Tooltip("NiumaScene 根控制器。为空时会在 BootstrapRoot 子物体中自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Header("调试")]
        [Tooltip("是否输出 Bootstrap 警告日志。")]
        [SerializeField] private bool logWarnings = true;

        public static NiumaGameBootstrapper Instance => _instance;
        public NiumaSceneController SceneController => sceneController;

        private void Awake()
        {
            if (!InitializeSingleton())
            {
                return;
            }

            ResolveRoot();
            ResolveSceneController();

            if (dontDestroyOnLoad && bootstrapRoot != null)
            {
                DontDestroyOnLoad(bootstrapRoot);
            }
        }

        private void Start()
        {
            if (_instance != this || !loadFirstSceneOnStart || string.IsNullOrWhiteSpace(firstSceneName))
            {
                return;
            }

            if (!ResolveSceneController())
            {
                Warn("未找到 NiumaSceneController，无法自动加载首场景。");
                return;
            }

            if (clearReturnStackBeforeFirstScene)
            {
                sceneController.ClearReturnContexts();
            }

            sceneController.LoadScene(new SceneTransitionRequest
            {
                Purpose = SceneLoadPurpose.Bootstrap,
                Target = new SceneTransitionTarget(firstSceneName),
                ReturnPolicy = null,
                Options = new SceneTransitionOptions
                {
                    FreezeInputDuringLoad = true,
                    ReplacePendingRequest = true,
                    ReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew
                }
            });
        }

        private bool InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = this;
                return true;
            }

            if (_instance == this)
            {
                return true;
            }

            if (destroyDuplicateBootstrap)
            {
                ResolveRoot();
                Destroy(bootstrapRoot != null ? bootstrapRoot : gameObject);
            }
            else
            {
                enabled = false;
            }

            return false;
        }

        private void ResolveRoot()
        {
            if (bootstrapRoot == null)
            {
                bootstrapRoot = gameObject;
            }
        }

        private bool ResolveSceneController()
        {
            if (sceneController != null)
            {
                sceneController.EnsureInitialized();
                return true;
            }

            ResolveRoot();
            sceneController = bootstrapRoot != null
                ? bootstrapRoot.GetComponentInChildren<NiumaSceneController>(true)
                : GetComponentInChildren<NiumaSceneController>(true);

            if (sceneController != null)
            {
                sceneController.EnsureInitialized();
            }

            return sceneController != null;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[NiumaGameBootstrapper] {message}", this);
            }
        }
    }
}
