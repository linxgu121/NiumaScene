using System;
using NiumaAudio.Bridge;
using NiumaAudio.Controller;
using NiumaAudio.Data;
using NiumaAudio.Service;
using NiumaScene.Controller;
using NiumaScene.Data;
using NiumaScene.Enum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.AudioBridge
{
    /// <summary>
    /// NiumaScene 到 NiumaAudio 的桥接脚本。
    /// 建议挂在全局 SceneRoot 或 AudioRoot 子物体上，绑定 NiumaSceneController 和 NiumaAudioController。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneAudioBridge : MonoBehaviour
    {
        [Header("控制器绑定")]
        [Tooltip("场景控制器。请拖入 SceneRoot 上的 NiumaSceneController；为空时可自动查找。")]
        [SerializeField] private NiumaSceneController sceneController;

        [Tooltip("音频控制器。请拖入 AudioRoot 上的 NiumaAudioController；为空时可自动查找。")]
        [SerializeField] private NiumaAudioController audioController;

        [Tooltip("未手动绑定 SceneController 时是否自动查找场景中的 NiumaSceneController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindSceneController = true;

        [Tooltip("未手动绑定 AudioController 时是否自动查找场景中的 NiumaAudioController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindAudioController = true;

        [Header("加载过程音效")]
        [Tooltip("场景加载开始时播放的 UI 音效。CueId 填 AudioCueDefinition.CueId；为空则不播放。")]
        [SerializeField] private AudioCueBinding loadStartedCue = new AudioCueBinding
        {
            SourceModule = "NiumaScene",
            OverrideBus = true,
            Bus = AudioBus.UI
        };

        [Tooltip("场景加载完成时播放的 UI 音效。CueId 填 AudioCueDefinition.CueId；为空则不播放。")]
        [SerializeField] private AudioCueBinding loadCompletedCue = new AudioCueBinding
        {
            SourceModule = "NiumaScene",
            OverrideBus = true,
            Bus = AudioBus.UI
        };

        [Tooltip("场景加载失败、取消或回退失败时播放的 UI 音效。CueId 填 AudioCueDefinition.CueId；为空则不播放。")]
        [SerializeField] private AudioCueBinding loadFailedCue = new AudioCueBinding
        {
            SourceModule = "NiumaScene",
            OverrideBus = true,
            Bus = AudioBus.UI
        };

        [Header("场景音乐表")]
        [Tooltip("场景名到 BGM / 环境音的映射。SceneName 填 Build Settings 中的场景名。")]
        [SerializeField] private SceneAudioCueSet[] sceneAudioCues = Array.Empty<SceneAudioCueSet>();

        [Tooltip("环境音频道 ID。同一频道的新环境音会替换旧环境音。")]
        [SerializeField] private string ambientChannelId = "scene_ambient";

        [Tooltip("没有找到场景音频配置时，是否停止当前环境音。")]
        [SerializeField] private bool stopAmbientWhenSceneHasNoConfig;

        [Tooltip("场景重复激活时是否重新播放同一 BGM。通常关闭，避免切回同一场景时音乐从头开始。")]
        [SerializeField] private bool restartBgmIfSameScene;

        [Header("调试")]
        [Tooltip("缺少控制器、CueId 或播放失败时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private string _lastRequestId;
        private SceneLoadStatus _lastStatus = SceneLoadStatus.None;
        private string _lastAppliedSceneName;
        private IAudioCommand _runtimeCommand;

        public AudioOperationResult LastAudioResult { get; private set; }

        public void SetAudioCommand(IAudioCommand command)
        {
            _runtimeCommand = command;
        }

        public void SetSceneController(NiumaSceneController controller)
        {
            sceneController = controller;
        }

        public void SetAudioController(NiumaAudioController controller)
        {
            audioController = controller;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            ApplyAudioForScene(SceneManager.GetActiveScene().name, false);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void LateUpdate()
        {
            if (!ResolveSceneController())
            {
                return;
            }

            var snapshot = sceneController.LoadingSnapshot;
            if (snapshot == null)
            {
                return;
            }

            if (string.Equals(_lastRequestId, snapshot.RequestId, StringComparison.Ordinal)
                && _lastStatus == snapshot.Status)
            {
                return;
            }

            _lastRequestId = snapshot.RequestId;
            _lastStatus = snapshot.Status;
            HandleStatusChanged(snapshot);
        }

        /// <summary>
        /// UnityEvent 调用入口：按当前激活场景立即刷新 BGM 和环境音。
        /// </summary>
        public void RefreshCurrentSceneAudio()
        {
            ApplyAudioForScene(SceneManager.GetActiveScene().name, true);
        }

        private void HandleStatusChanged(SceneLoadingSnapshot snapshot)
        {
            switch (snapshot.Status)
            {
                case SceneLoadStatus.Pending:
                case SceneLoadStatus.Loading:
                    PlayCue(loadStartedCue);
                    break;
                case SceneLoadStatus.Completed:
                case SceneLoadStatus.Skipped:
                    PlayCue(loadCompletedCue);
                    ApplyAudioForScene(ResolveTargetSceneName(snapshot), false);
                    break;
                case SceneLoadStatus.Failed:
                case SceneLoadStatus.Cancelled:
                    PlayCue(loadFailedCue);
                    break;
            }
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            ApplyAudioForScene(newScene.name, false);
        }

        private void ApplyAudioForScene(string sceneName, bool force)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!force && string.Equals(_lastAppliedSceneName, sceneName, StringComparison.Ordinal))
            {
                return;
            }

            _lastAppliedSceneName = sceneName;

            if (!TryResolveCommand(out var command))
            {
                Warn("未找到 NiumaAudioController 或 IAudioCommand，无法切换场景音频。");
                return;
            }

            var cueSet = FindCueSet(sceneName);
            if (cueSet == null)
            {
                if (stopAmbientWhenSceneHasNoConfig && !string.IsNullOrWhiteSpace(ambientChannelId))
                {
                    LastAudioResult = command.StopAmbient(ambientChannelId, 1f);
                    WarnFailure(LastAudioResult);
                }

                return;
            }

            if (cueSet.BgmCue != null && cueSet.BgmCue.HasPlayableKey)
            {
                LastAudioResult = command.PlayBgm(cueSet.BgmCue.ToBgmRequest(restartBgmIfSameScene, "NiumaScene"));
                WarnFailure(LastAudioResult);
            }

            if (cueSet.AmbientCue != null && cueSet.AmbientCue.HasPlayableKey)
            {
                if (string.IsNullOrWhiteSpace(ambientChannelId))
                {
                    Warn("Ambient ChannelId 为空，无法播放场景环境音。");
                }
                else
                {
                    LastAudioResult = command.PlayAmbient(cueSet.AmbientCue.ToAmbientRequest(ambientChannelId, "NiumaScene"));
                    WarnFailure(LastAudioResult);
                }
            }
            else if (stopAmbientWhenSceneHasNoConfig && !string.IsNullOrWhiteSpace(ambientChannelId))
            {
                LastAudioResult = command.StopAmbient(ambientChannelId, 1f);
                WarnFailure(LastAudioResult);
            }
        }

        private void PlayCue(AudioCueBinding cue)
        {
            if (cue == null || !cue.HasPlayableKey)
            {
                return;
            }

            if (!TryResolveCommand(out var command))
            {
                Warn("未找到 NiumaAudioController 或 IAudioCommand，无法播放场景桥接音效。");
                return;
            }

            LastAudioResult = command.PlayCue(cue.ToPlayRequest("NiumaScene"));
            WarnFailure(LastAudioResult);
        }

        private SceneAudioCueSet FindCueSet(string sceneName)
        {
            if (sceneAudioCues == null)
            {
                return null;
            }

            for (var i = 0; i < sceneAudioCues.Length; i++)
            {
                var cueSet = sceneAudioCues[i];
                if (cueSet == null || string.IsNullOrWhiteSpace(cueSet.SceneName))
                {
                    continue;
                }

                if (string.Equals(cueSet.SceneName, sceneName, StringComparison.Ordinal))
                {
                    return cueSet;
                }
            }

            return null;
        }

        private static string ResolveTargetSceneName(SceneLoadingSnapshot snapshot)
        {
            return !string.IsNullOrWhiteSpace(snapshot.TargetSceneName)
                ? snapshot.TargetSceneName
                : SceneManager.GetActiveScene().name;
        }

        private bool ResolveSceneController()
        {
            if (sceneController != null)
            {
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
            return sceneController != null;
        }

        private bool TryResolveCommand(out IAudioCommand command)
        {
            var resolved = AudioBridgeResolver.TryResolveCommand(
                _runtimeCommand,
                null,
                audioController,
                autoFindAudioController,
                out command,
                out var resolvedController);

            if (resolvedController != null)
            {
                audioController = resolvedController;
            }

            return resolved;
        }

        private void WarnFailure(AudioOperationResult result)
        {
            if (result == null || result.Succeeded)
            {
                return;
            }

            Warn($"场景音频播放失败：{result.FailureReason}，{result.Message}");
        }

        private void Warn(string message)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"[NiumaSceneAudioBridge] {message}", this);
            }
        }
    }
}
