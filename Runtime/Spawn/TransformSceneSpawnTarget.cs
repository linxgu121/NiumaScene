using UnityEngine;

namespace NiumaScene.Spawn
{
    /// <summary>
    /// 通用出生点目标。
    /// 适合第一版直接挂在玩家根物体上；如果项目使用 TPC，可改用 TPCSceneSpawnTarget 桥接脚本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransformSceneSpawnTarget : MonoBehaviour, ISceneSpawnTarget
    {
        [Header("目标")]
        [Tooltip("实际被传送的 Transform。为空时使用当前 GameObject 的 Transform。")]
        [SerializeField] private Transform targetTransform;

        [Header("物理处理")]
        [Tooltip("传送时是否临时关闭 CharacterController，避免 SetPositionAndRotation 被控制器阻挡。")]
        [SerializeField] private bool disableCharacterControllerDuringTeleport = true;

        [Tooltip("传送时是否清理 Rigidbody 速度，避免落点后被上一帧物理速度带走。")]
        [SerializeField] private bool clearRigidbodyVelocity = true;

        [Tooltip("传送完成后是否调用 Physics.SyncTransforms，确保物理查询立即看到新位置。")]
        [SerializeField] private bool syncPhysicsTransforms = true;

        private void OnEnable()
        {
            SceneSpawnRegistry.RegisterSpawnTarget(this);
        }

        private void OnDisable()
        {
            SceneSpawnRegistry.UnregisterSpawnTarget(this);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            var target = targetTransform != null ? targetTransform : transform;
            if (target == null)
            {
                return;
            }

            var characterController = disableCharacterControllerDuringTeleport
                ? target.GetComponent<CharacterController>()
                : null;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            try
            {
                var rigidbody = target.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    ApplyRigidbodyTeleport(rigidbody, position, rotation);
                }
                else
                {
                    target.SetPositionAndRotation(position, rotation);
                }
            }
            finally
            {
                if (characterController != null)
                {
                    characterController.enabled = true;
                }
            }

            if (syncPhysicsTransforms)
            {
                Physics.SyncTransforms();
            }
        }

        private void ApplyRigidbodyTeleport(Rigidbody rigidbody, Vector3 position, Quaternion rotation)
        {
            if (clearRigidbodyVelocity && !rigidbody.isKinematic)
            {
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.Sleep();
            }

            rigidbody.position = position;
            rigidbody.rotation = rotation;
        }
    }
}
