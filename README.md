# NiumaScene

## 模块定位
NiumaScene 是场景流程模块，负责统一场景加载、返回上下文、出生点恢复、加载状态、输入冻结、检查点保存意图和场景 UI 桥接。

## 框架设计思路
- SceneTransitionRequest 描述去哪、怎么去、是否记录返回上下文。
- SceneService 负责仲裁加载请求、Pending 替换、失败 fallback 和结果句柄。
- 返回上下文用栈保存，支持 RPG -> MiniGame -> RPG 等流程。
- 输入冻结、出生点、检查点保存通过接口适配器接入，不直接依赖 TPC、Interact、Save。

## 核心流程
1. 调用 NiumaSceneController.LoadScene。
2. Service 校验请求并按策略压入 ReturnContext。
3. 加载开始时发布 LoadingSnapshot，冻结输入。
4. SceneManager 异步加载目标场景。
5. 场景加载完成后查找 SpawnPoint 并恢复玩家位置。
6. 解冻输入，完成 SceneTransitionHandle。
7. ReturnToPreviousScene 弹出上下文并回到来源场景。

## 模块用法
- 进入 MiniGame 时 Purpose 使用 MiniGame，并开启 PushReturnContext。
- 返回点 ID 应配置在 NPC 或入口附近的 SceneSpawnPoint。
- 同场景传送可使用 TeleportToSpawnPoint，不必重新加载场景。

## 场景使用方法
推荐放置方式：`SceneRoot` 一个全局场景流程物体承载加载服务、Loading UI、输入冻结和检查点桥接。

- `SceneRoot`：挂 `NiumaSceneController`，配置 fallbackSceneName、返回栈深度、默认加载选项。
- `SceneRoot/Bootstrapper`：首场景需要自动进入主场景时挂 `NiumaGameBootstrapper`。
- `SceneRoot/Loading`：挂 `SceneLoadingStateBridge` 和 `SceneLoadingPanelBridge`，绑定 Loading UI Receiver。
- `SceneRoot/CheckpointRequester`：挂 `NiumaSceneSaveCheckpointRequester`，绑定 NiumaSaveController。
- `PlayerRoot/SceneBridge`：挂 `TPCSceneSpawnTarget` 和 `TPCSceneInputBlockTarget`。
- `PlayerRoot/InteractionRoot/SceneBridge`：挂 `InteractSceneInputBlockTarget`，加载期间冻结交互。
- `SpawnPoint_xxx`：每个可返回/传送位置挂 `SceneSpawnPoint`，填写稳定 SpawnPointId。
- 非玩家对象需要被场景传送时，可挂 `TransformSceneSpawnTarget`。
- MiniGame、建筑、传送门物体只负责调用 LoadScene，不要自己直接 SceneManager.LoadScene。

## 协作边界
Scene 不直接保存文件、不直接控制玩家内部逻辑，只通过接口发出冻结、出生点、检查点意图。


