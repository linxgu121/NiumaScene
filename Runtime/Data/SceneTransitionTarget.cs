using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.Data
{
    /// <summary>
    /// 场景切换目标。
    /// 只描述“去哪里”和“进入目标场景后是否恢复出生点”，不描述返回逻辑。
    /// </summary>
    [Serializable]
    public sealed class SceneTransitionTarget
    {
        /// <summary>目标场景名。第一版使用 Build Settings 中的场景名称。</summary>
        [Tooltip("目标场景名（必须与 Build Settings 中的场景名一致，例如 RPG_Village、MiniGame_Start）。")]
        public string SceneName;

        /// <summary>加载模式。第一版推荐使用 Single。</summary>
        [Tooltip("加载模式。Single（主场景切换，会卸载旧业务场景，RPG↔MiniGame 推荐）；Additive（叠加场景，适合常驻核心场景/子场景；第一版服务会规整为 Single）。")]
        public LoadSceneMode LoadMode = LoadSceneMode.Single;

        /// <summary>进入目标场景后优先使用的出生点 ID。RestorePlayerAtSpawnPoint 为 false 时忽略。</summary>
        [Tooltip("目标出生点 ID（进入目标场景后把玩家放到这里，例如 village_gate、npc_minigame_return；未勾选恢复出生点时忽略）。")]
        public string PreferredSpawnPointId;

        /// <summary>是否在目标场景加载后把玩家恢复到出生点。</summary>
        [Tooltip("进入目标场景后是否移动玩家到出生点（进入 RPG/建筑/传送点常用；纯 UI 场景可关闭）。")]
        public bool RestorePlayerAtSpawnPoint;

        public SceneTransitionTarget()
        {
        }

        public SceneTransitionTarget(string sceneName)
        {
            SceneName = sceneName;
            LoadMode = LoadSceneMode.Single;
        }
    }
}
