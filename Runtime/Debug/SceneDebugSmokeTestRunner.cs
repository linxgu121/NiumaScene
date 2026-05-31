using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using NiumaScene.Service;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.Debugging
{
    /// <summary>
    /// NiumaScene 快速自检入口。
    /// 该脚本只覆盖不会真实切换场景的安全用例；MiniGame 进入 / 返回等流程仍需要按文档手动验证。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneDebugSmokeTestRunner : MonoBehaviour
    {
        [Header("测试依赖")]
        [Tooltip("NiumaScene 根控制器。为空时会自动查找场景中的 NiumaSceneController。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("未手动绑定 SceneController 时，是否自动查找。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Header("测试数据")]
        [Tooltip("用于测试“场景不存在”的虚假场景名。不要加入 Build Settings。")]
        [SerializeField] private string missingSceneName = "__NiumaScene_Missing_For_Test__";

        [Header("日志")]
        [Tooltip("是否输出每个通过项。关闭后只输出失败和最终汇总。")]
        [SerializeField] private bool logPassedItems = true;

        [Tooltip("是否在测试结束后清空返回上下文栈，避免调试数据残留影响正式流程。")]
        [SerializeField] private bool clearReturnContextsAfterTest = true;

        private int _passed;
        private int _failed;

        [ContextMenu("运行 NiumaScene 快速自检")]
        public void RunSmokeTests()
        {
            _passed = 0;
            _failed = 0;

            if (!ResolveSceneController())
            {
                Debug.LogError("[NiumaSceneSmokeTest] 失败：未找到 NiumaSceneController。", this);
                return;
            }

            var service = sceneController.SceneService;
            sceneController.ClearReturnContexts();

            TestReturnWithoutContext(service);
            TestInvalidRequests(service);
            TestMissingScene(service);
            TestSameSceneSkip(service);
            TestCancelUnknownRequest(service);

            if (clearReturnContextsAfterTest)
            {
                sceneController.ClearReturnContexts();
            }

            if (_failed > 0)
            {
                Debug.LogError($"[NiumaSceneSmokeTest] 完成：通过 {_passed}，失败 {_failed}。", this);
            }
            else
            {
                Debug.Log($"[NiumaSceneSmokeTest] 全部通过：{_passed} 项。", this);
            }
        }

        private void TestReturnWithoutContext(ISceneService service)
        {
            var handle = service.ReturnToPreviousScene();
            ExpectFailure("返回上下文为空时失败", handle, SceneLoadErrorCode.ReturnContextMissing);
        }

        private void TestInvalidRequests(ISceneService service)
        {
            ExpectFailure("空请求失败", service.LoadScene(null), SceneLoadErrorCode.InvalidRequest);

            ExpectFailure(
                "缺少 Target 失败",
                service.LoadScene(new SceneTransitionRequest()),
                SceneLoadErrorCode.InvalidRequest);

            ExpectFailure(
                "空场景名失败",
                service.LoadScene(new SceneTransitionRequest
                {
                    Target = new SceneTransitionTarget(" ")
                }),
                SceneLoadErrorCode.EmptySceneName);

            ExpectFailure(
                "PushReturnContext=false 时不允许填写返回点",
                service.LoadScene(new SceneTransitionRequest
                {
                    Target = new SceneTransitionTarget(SceneManager.GetActiveScene().name),
                    ReturnPolicy = new SceneReturnPolicy
                    {
                        PushReturnContext = false,
                        ReturnSpawnPointId = "invalid_return_point"
                    }
                }),
                SceneLoadErrorCode.InvalidRequest);
        }

        private void TestMissingScene(ISceneService service)
        {
            var sceneName = string.IsNullOrWhiteSpace(missingSceneName)
                ? "__NiumaScene_Missing_For_Test__"
                : missingSceneName.Trim();

            ExpectFailure(
                "场景名不存在时失败且不切换",
                service.LoadScene(new SceneTransitionRequest
                {
                    Target = new SceneTransitionTarget(sceneName),
                    Options = QuietOptions()
                }),
                SceneLoadErrorCode.SceneNotFound);
        }

        private void TestSameSceneSkip(ISceneService service)
        {
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(activeSceneName))
            {
                Pass("同场景跳过测试已跳过：当前场景未保存或无名称。");
                return;
            }

            var beforeCount = service.ReturnContextCount;
            var handle = service.LoadScene(new SceneTransitionRequest
            {
                Purpose = SceneLoadPurpose.Debug,
                Target = new SceneTransitionTarget(activeSceneName),
                ReturnPolicy = new SceneReturnPolicy
                {
                    PushReturnContext = true,
                    ReturnSpawnPointId = "debug_return"
                },
                Options = QuietOptions()
            });

            var ok = handle != null
                     && handle.IsDone
                     && handle.Result != null
                     && handle.Result.Succeeded
                     && handle.Status == SceneLoadStatus.Skipped
                     && !handle.Result.PushedReturnContext
                     && service.ReturnContextCount == beforeCount;

            Check(ok, "目标为当前场景时跳过重复加载且不压入返回上下文", Describe(handle));
        }

        private void TestCancelUnknownRequest(ISceneService service)
        {
            Check(!service.CancelLoad("__missing_request_id__"), "取消未知 RequestId 返回 false", null);
        }

        private static SceneTransitionOptions QuietOptions()
        {
            return new SceneTransitionOptions
            {
                FreezeInputDuringLoad = false,
                ShowLoadingUI = false,
                ReplacePendingRequest = true,
                ReturnOverflowPolicy = SceneReturnOverflowPolicy.RejectNew
            };
        }

        private void ExpectFailure(string label, SceneTransitionHandle handle, SceneLoadErrorCode expectedError)
        {
            var ok = handle != null
                     && handle.IsDone
                     && handle.Result != null
                     && !handle.Result.Succeeded
                     && handle.ErrorCode == expectedError;

            Check(ok, label, $"{Describe(handle)}，期望错误={expectedError}");
        }

        private void Check(bool passed, string label, string detail)
        {
            if (passed)
            {
                Pass(label);
                return;
            }

            _failed++;
            Debug.LogError($"[NiumaSceneSmokeTest] 失败：{label}。{detail}", this);
        }

        private void Pass(string label)
        {
            _passed++;
            if (logPassedItems)
            {
                Debug.Log($"[NiumaSceneSmokeTest] 通过：{label}", this);
            }
        }

        private bool ResolveSceneController()
        {
            if (sceneController != null)
            {
                sceneController.EnsureInitialized();
                return true;
            }

            if (!autoFindSceneController)
            {
                return false;
            }

#if UNITY_2023_1_OR_NEWER
            sceneController = FindFirstObjectByType<NiumaSceneController>();
#else
            sceneController = FindObjectOfType<NiumaSceneController>();
#endif

            if (sceneController != null)
            {
                sceneController.EnsureInitialized();
            }

            return sceneController != null;
        }

        private static string Describe(SceneTransitionHandle handle)
        {
            if (handle == null)
            {
                return "Handle=null";
            }

            var result = handle.Result;
            return result == null
                ? $"Status={handle.Status}, Error={handle.ErrorCode}, Result=null"
                : $"Status={handle.Status}, Error={handle.ErrorCode}, ResultError={result.ErrorCode}, Message={result.ErrorMessage}";
        }
    }
}
