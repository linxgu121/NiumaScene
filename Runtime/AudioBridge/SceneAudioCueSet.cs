using System;
using NiumaAudio.Bridge;
using UnityEngine;

namespace NiumaScene.AudioBridge
{
    /// <summary>
    /// 单个场景对应的音频配置。
    /// SceneName 填 Build Settings 中的场景名；BGM 和 Ambient 的 CueId 填 AudioCueDefinition.CueId。
    /// </summary>
    [Serializable]
    public sealed class SceneAudioCueSet
    {
        [Tooltip("场景名。填写 Build Settings 中的场景名，例如 VillageMain、MiniGameLobby。")]
        public string SceneName;

        [Tooltip("进入该场景后播放的 BGM。CueId 填 AudioCueDefinition.CueId；为空则不切换 BGM。")]
        public AudioCueBinding BgmCue = new AudioCueBinding
        {
            SourceModule = "NiumaScene"
        };

        [Tooltip("进入该场景后播放的环境音。CueId 填 AudioCueDefinition.CueId；为空则不切换环境音。")]
        public AudioCueBinding AmbientCue = new AudioCueBinding
        {
            SourceModule = "NiumaScene"
        };
    }
}
