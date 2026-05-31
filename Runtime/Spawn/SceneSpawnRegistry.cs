using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaScene.Spawn
{
    /// <summary>
    /// 场景出生点注册表。
    /// 第一版同时支持主动注册和 FindObjectsByType 兜底，避免场景初始化时序导致找不到对象。
    /// </summary>
    public static class SceneSpawnRegistry
    {
        private static readonly List<SceneSpawnPoint> SpawnPoints = new List<SceneSpawnPoint>(16);
        private static readonly List<MonoBehaviour> SpawnTargetBehaviours = new List<MonoBehaviour>(4);
        private static readonly List<SceneSpawnPoint> PointBuffer = new List<SceneSpawnPoint>(16);

        public static void RegisterSpawnPoint(SceneSpawnPoint spawnPoint)
        {
            if (spawnPoint == null || SpawnPoints.Contains(spawnPoint))
            {
                return;
            }

            SpawnPoints.Add(spawnPoint);
        }

        public static void UnregisterSpawnPoint(SceneSpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                return;
            }

            SpawnPoints.Remove(spawnPoint);
        }

        public static void RegisterSpawnTarget(MonoBehaviour behaviour)
        {
            if (behaviour == null || behaviour is not ISceneSpawnTarget || SpawnTargetBehaviours.Contains(behaviour))
            {
                return;
            }

            SpawnTargetBehaviours.Add(behaviour);
        }

        public static void UnregisterSpawnTarget(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            SpawnTargetBehaviours.Remove(behaviour);
        }

        public static bool TryFindSpawnPoint(string spawnPointId, out SceneSpawnPoint spawnPoint, out bool usedDefault, out string warning)
        {
            spawnPoint = null;
            usedDefault = false;
            warning = null;

            CollectSpawnPoints(PointBuffer);
            if (!string.IsNullOrWhiteSpace(spawnPointId))
            {
                spawnPoint = FindById(PointBuffer, spawnPointId.Trim(), out var duplicateCount);
                if (spawnPoint != null)
                {
                    if (duplicateCount > 1)
                    {
                        warning = $"当前场景存在重复 SpawnPointId：{spawnPointId}，已使用第一个可用出生点。";
                    }

                    return true;
                }

                warning = $"未找到指定 SpawnPoint：{spawnPointId}，尝试使用默认出生点。";
            }

            spawnPoint = FindDefault(PointBuffer, out var defaultCount);
            if (spawnPoint == null)
            {
                if (string.IsNullOrWhiteSpace(warning))
                {
                    warning = "当前场景没有可用默认 SpawnPoint。";
                }
                else
                {
                    warning += " 当前场景也没有可用默认 SpawnPoint。";
                }

                return false;
            }

            usedDefault = true;
            if (defaultCount > 1)
            {
                var duplicateWarning = "当前场景存在多个默认 SpawnPoint，已使用第一个可用默认点。";
                warning = string.IsNullOrWhiteSpace(warning) ? duplicateWarning : $"{warning} {duplicateWarning}";
            }

            return true;
        }

        public static bool TryFindSpawnTarget(out ISceneSpawnTarget spawnTarget)
        {
            spawnTarget = null;
            PruneSpawnTargets();

            for (var i = 0; i < SpawnTargetBehaviours.Count; i++)
            {
                var behaviour = SpawnTargetBehaviours[i];
                if (IsUsableSpawnTarget(behaviour))
                {
                    spawnTarget = (ISceneSpawnTarget)behaviour;
                    return true;
                }
            }

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (IsUsableSpawnTarget(behaviour))
                {
                    spawnTarget = (ISceneSpawnTarget)behaviour;
                    RegisterSpawnTarget(behaviour);
                    return true;
                }
            }

            return false;
        }

        private static void CollectSpawnPoints(List<SceneSpawnPoint> output)
        {
            output.Clear();
            var activeScene = SceneManager.GetActiveScene();

            for (var i = SpawnPoints.Count - 1; i >= 0; i--)
            {
                var point = SpawnPoints[i];
                if (!IsUsableSpawnPoint(point, activeScene))
                {
                    SpawnPoints.RemoveAt(i);
                    continue;
                }

                AddUnique(output, point);
            }

            var foundPoints = UnityEngine.Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < foundPoints.Length; i++)
            {
                var point = foundPoints[i];
                if (!IsUsableSpawnPoint(point, activeScene))
                {
                    continue;
                }

                AddUnique(output, point);
                RegisterSpawnPoint(point);
            }
        }

        private static bool IsUsableSpawnPoint(SceneSpawnPoint point, Scene activeScene)
        {
            return point != null
                   && point.isActiveAndEnabled
                   && point.gameObject.scene == activeScene;
        }

        private static bool IsUsableSpawnTarget(MonoBehaviour behaviour)
        {
            return behaviour != null
                   && behaviour.isActiveAndEnabled
                   && behaviour is ISceneSpawnTarget;
        }

        private static void AddUnique(List<SceneSpawnPoint> points, SceneSpawnPoint point)
        {
            if (point != null && !points.Contains(point))
            {
                points.Add(point);
            }
        }

        private static SceneSpawnPoint FindById(List<SceneSpawnPoint> points, string spawnPointId, out int matchCount)
        {
            matchCount = 0;
            SceneSpawnPoint first = null;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null || !string.Equals(point.SpawnPointId, spawnPointId, StringComparison.Ordinal))
                {
                    continue;
                }

                matchCount++;
                first ??= point;
            }

            return first;
        }

        private static SceneSpawnPoint FindDefault(List<SceneSpawnPoint> points, out int defaultCount)
        {
            defaultCount = 0;
            SceneSpawnPoint first = null;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null || !point.IsDefault)
                {
                    continue;
                }

                defaultCount++;
                first ??= point;
            }

            return first;
        }

        private static void PruneSpawnTargets()
        {
            for (var i = SpawnTargetBehaviours.Count - 1; i >= 0; i--)
            {
                if (SpawnTargetBehaviours[i] == null)
                {
                    SpawnTargetBehaviours.RemoveAt(i);
                }
            }
        }
    }
}
