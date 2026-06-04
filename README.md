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
- Unity Button 不建议直接绑定 `NiumaSceneController.LoadScene`，因为它返回 `SceneTransitionHandle`，不适合作为 Inspector 按钮事件。按钮跳转请使用 `SceneButtonAction`。

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

### 按钮跳转场景
推荐把每个“跳转按钮”对应的场景行为独立成一个物体或挂在按钮自身，方便 UI 策划在 Inspector 中配置。

推荐层级：

```text
UIRoot
└── Windows
    └── MiniGameEntrancePanel
        ├── StartButton
        │   └── SceneButtonAction
        └── ExitButton
            └── SceneButtonAction
```

`StartButton` 进入小游戏场景：

1. 在 `StartButton` 或同级子物体上挂 `SceneButtonAction`。
2. `Scene Controller` 绑定全局 `SceneRoot` 上的 `NiumaSceneController`。
3. `Target Scene Name` 填小游戏开始场景名，例如 `MiniGameLobbyScene`。
4. `Purpose` 选择 `MiniGame`。
5. 勾选 `Push Return Context`，表示进入小游戏前记录当前 RPG 场景。
6. `Return Spawn Point Id` 填返回点，例如 `npc_minigame_return`。
7. 勾选 `Freeze Input During Load` 和 `Show Loading UI`。
8. 在 Button 的 `OnClick()` 中添加该 `SceneButtonAction`。
9. 函数选择 `SceneButtonAction.LoadConfiguredScene()`。

`ExitButton` 返回外部游戏：

1. 在 `ExitButton` 上挂 `SceneButtonAction`。
2. `Scene Controller` 绑定全局 `NiumaSceneController`。
3. 在 Button 的 `OnClick()` 中选择 `SceneButtonAction.ReturnToPreviousScene()`。

同场景传送：

1. 在按钮上挂 `SceneButtonAction`。
2. `Target Spawn Point Id` 填目标出生点 ID。
3. Button `OnClick()` 选择 `SceneButtonAction.TeleportToConfiguredSpawnPoint()`。

### 对话选项触发场景跳转
对话选项有两种事件：

- `DialogueChoiceButtonBinding.onChoiceClicked`：只建议做音效、动画、按钮反馈。
- 对话系统的 `ChoiceId`：负责推进 Gal 分支、任务、剧情、存档等业务。

如果某个对话选项要“进入你画我猜”，推荐做法是：

1. `DialogueAsset` 中配置一个稳定选项 ID，例如 `enter_draw_guess`。
2. Gal / Story / 交互桥接层根据该 `ChoiceId` 调用场景跳转。
3. 场景跳转仍使用 `NiumaSceneController` 或封装好的 `SceneButtonAction.LoadConfiguredScene()`。

不推荐直接在 `onChoiceClicked` 里跳场景，因为这样可能绕过 Gal 的选择记录、任务推进和存档事实。`onChoiceClicked` 可以播放点击音效或关闭按钮动画，但业务跳转应由 ChoiceId 对应的桥接逻辑触发。

## 协作边界
Scene 不直接保存文件、不直接控制玩家内部逻辑，只通过接口发出冻结、出生点、检查点意图。


