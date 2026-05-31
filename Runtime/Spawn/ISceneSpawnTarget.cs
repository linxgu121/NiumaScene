using UnityEngine;

namespace NiumaScene.Spawn
{
    /// <summary>
    /// 场景出生点目标。
    /// 玩家控制器、相机根或其他可传送对象通过适配器实现该接口，避免 NiumaScene 直接依赖 TPC。
    /// </summary>
    public interface ISceneSpawnTarget
    {
        /// <summary>
        /// 传送到指定位置和旋转。
        /// 实现层应自行处理 CharacterController / Rigidbody / 输入状态等细节。
        /// </summary>
        void TeleportTo(Vector3 position, Quaternion rotation);
    }
}
