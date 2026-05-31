using System;
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
        public string SceneName;

        /// <summary>加载模式。第一版推荐使用 Single。</summary>
        public LoadSceneMode LoadMode = LoadSceneMode.Single;

        /// <summary>进入目标场景后优先使用的出生点 ID。RestorePlayerAtSpawnPoint 为 false 时忽略。</summary>
        public string PreferredSpawnPointId;

        /// <summary>是否在目标场景加载后把玩家恢复到出生点。</summary>
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
