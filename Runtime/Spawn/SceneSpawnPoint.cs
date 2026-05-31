using UnityEngine;

namespace NiumaScene.Spawn
{
    /// <summary>
    /// 场景出生点。
    /// 用稳定 ID 标记玩家进入场景、返回场景、复活或同场景传送时应落到的位置。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [Header("出生点")]
        [Tooltip("稳定出生点 ID。会被剧情、返回上下文、存档或传送点引用，确定后不要随意修改。")]
        [SerializeField] private string spawnPointId = "main_default";

        [Tooltip("是否作为当前场景默认出生点。指定 ID 找不到或请求未指定 ID 时，会尝试使用默认出生点。")]
        [SerializeField] private bool isDefault;

        [Header("调试")]
        [Tooltip("是否在 Scene 视图绘制出生点方向。")]
        [SerializeField] private bool drawGizmos = true;

        [Tooltip("出生点 Gizmos 颜色。默认绿色表示可用落点。")]
        [SerializeField] private Color gizmosColor = new Color(0.2f, 0.9f, 0.35f, 0.85f);

        public string SpawnPointId => spawnPointId;
        public bool IsDefault => isDefault;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void OnEnable()
        {
            SceneSpawnRegistry.RegisterSpawnPoint(this);
        }

        private void OnDisable()
        {
            SceneSpawnRegistry.UnregisterSpawnPoint(this);
        }

        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(spawnPointId))
            {
                spawnPointId = spawnPointId.Trim();
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = gizmosColor;
            Gizmos.DrawSphere(transform.position, 0.18f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
    }
}
