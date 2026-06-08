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
- 核心场景搭建请先阅读 [核心场景制作指南.md](核心场景制作指南.md)，其中包含脚本挂载位置、Inspector 字段填写方式、可否留空和不填后果。全局控制器应放核心场景常驻，业务场景只放场景内容。

### 场景切换配置速查
- `Purpose`：选择本次切换用途。`MiniGame`（RPG 进入小游戏）、`EnterBuilding`（室外进室内）、`ExitBuilding`（室内回室外）、`Teleport`（传送点/地图跳转）、`Respawn`（死亡回检查点）、`Return`（返回上一场景）、`Debug`（测试按钮）。
- `LoadMode`：`Single`（主场景切换，会卸载旧业务场景，RPG 和 MiniGame 往返推荐）；`Additive`（叠加场景，适合常驻 Core/Bootstrap 或子场景；第一版服务会规整为 Single）。
- `PushReturnContext`：开启（进入 MiniGame、建筑内部、临时副本，之后需要返回）；关闭（主菜单进入游戏、单向传送、纯测试场景）。
- `ReturnSceneName`：为空（自动记录当前激活场景）；填写（强制返回指定场景，适合特殊入口）。
- `ReturnSpawnPointId`：填写返回点（NPC 面前、建筑门口、小游戏入口），目标场景中必须有同 ID 的 `SceneSpawnPoint`。
- `ClearReturnStackBeforePush`：开启（从主菜单重新开始游戏，清掉旧历史）；关闭（普通进建筑、进 MiniGame）。
- `FreezeInputDuringLoad`：开启（正式跨场景切换，防止玩家继续移动/交互）；关闭（开发调试或不影响操作的 UI 流程）。
- `ShowLoadingUI`：开启（跨场景加载需要过渡）；关闭（同场景传送或极短加载）。

## 场景使用方法
推荐放置方式：`SceneRoot` 一个全局场景流程物体承载加载服务、Loading UI、输入冻结和检查点桥接。

## 场景挂载与 Inspector 配置
NiumaScene 的完整核心场景搭建流程见 [核心场景制作指南.md](核心场景制作指南.md)。README 中保留常用脚本速查，方便策划和关卡同学直接照着 Inspector 配置。

### NiumaGameBootstrapper
建议挂载位置：`CoreScene/BootstrapRoot`。

用途：让核心场景常驻，并在启动后进入第一个正式业务场景。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Bootstrap Root` | 拖 `BootstrapRoot` 自己 | 可以 | 为空时使用当前 GameObject |
| `Dont Destroy On Load` | 正式场景开启 | 不建议关闭 | 关闭后切场景会卸载核心服务，返回栈丢失 |
| `Destroy Duplicate Bootstrap` | 开启 | 不建议关闭 | 重复进入 CoreScene 时可能出现两套全局服务 |
| `First Scene Name` | 填第一个业务场景名，例如 `RPG_Village` | 可以 | 为空时启动后停留在 CoreScene |
| `Load First Scene On Start` | 需要自动进游戏时开启 | 可以 | 关闭后要手动调用加载首场景 |
| `Clear Return Stack Before First Scene` | 正式启动流程开启 | 可以 | 关闭后调试残留返回栈可能影响返回 |
| `Show Loading UI On First Scene` | 有 Loading UI 时开启 | 可以 | 关闭后首场景加载不显示 Loading |
| `Scene Controller` | 拖 `SceneRoot` 上的 `NiumaSceneController` | 不建议 | 为空会自动从 BootstrapRoot 子物体查找，层级复杂时可能找不到 |

### NiumaSceneController
建议挂载位置：`CoreScene/BootstrapRoot/SceneRoot`。

用途：全局唯一场景切换服务，负责加载、返回栈、出生点恢复和检查点保存意图。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Fallback Scene Name` | 填安全兜底场景，例如 `RPG_Village` 或 `MainMenu` | 可以 | 加载失败时只记录错误，不会自动回兜底场景 |
| `Max Return Context Depth` | 建议 8 | 不可以 | 太小会导致嵌套返回不足，太大不易维护 |
| `Default Return Overflow Policy` | 建议 `RejectNew` | 不可以 | 策略不合适时可能丢返回链 |
| `Freeze Input During Load By Default` | 正式跨场景加载开启 | 可以 | 关闭后切场景期间玩家可能继续移动 |
| `Show Loading UI By Default` | 有 Loading UI 时开启 | 可以 | 关闭后默认不显示 Loading |
| `Checkpoint Requester Provider` | 拖 `NiumaSceneSaveCheckpointRequester` | 可以 | 留空时请求检查点保存不会真正写档 |
| `Auto Find Checkpoint Requester` | 测试可开，正式建议手动绑定 | 可以 | 关闭且未绑定时检查点保存不可用 |

### SceneLoadingStateBridge
建议挂载位置：`CoreScene/BootstrapRoot/SceneRoot/LoadingRoot`。

用途：读取加载状态，推送 Loading UI，并冻结玩家/交互输入。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Scene Controller` | 拖 `NiumaSceneController` | 不建议 | 自动查找失败时 Loading 和输入冻结都不工作 |
| `Loading Receiver Provider` | 拖 `SceneLoadingPanelBridge` 或自制 LoadingPanel 脚本 | 可以 | 留空时只冻结输入，不显示 Loading UI |
| `Input Block Target Providers` | 玩家拖 `TPCSceneInputBlockTarget`，交互拖 `InteractSceneInputBlockTarget` | 可以 | 留空时加载期间不会冻结对应输入 |
| `Input Block Reason` | 默认 `SceneLoading` 即可 | 不建议为空 | 为空会让解除阻塞难以追踪来源 |
| `Unblock When Loading Ends` | 建议开启 | 可以 | 关闭后加载结束不会自动解除本桥接加的冻结 |

### SceneLoadingPanelBridge
建议挂载位置：`CoreScene/BootstrapRoot/UIRoot/Canvas_Global/LoadingPanel`。

用途：显示加载面板、进度条、目标场景名和错误信息。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Panel Root` | 拖 Loading 面板根节点 | 可以 | 为空时使用当前物体 |
| `Canvas Group` | 拖面板上的 `CanvasGroup` | 不建议 | 找不到时淡入淡出和射线控制不稳定 |
| `Progress Fill` | 拖进度条填充 RectTransform | 可以 | 留空时不显示进度条 |
| `Status / Target / Progress / Error Text` | 拖对应 TMP 文本 | 可以 | 留空时对应文字不显示 |
| `Fade In / Fade Out Duration` | 建议 0.15 / 0.2 | 可以为 0 | 为 0 时立即显示/隐藏 |
| `Use Unscaled Time` | 建议开启 | 可以 | 关闭后 TimeScale=0 时 UI 动画可能不动 |
| `Disable Root When Hidden` | 建议开启 | 可以 | 关闭后透明面板可能仍参与射线或布局 |
| `Block Raycasts When Visible` | 建议开启 | 可以 | 关闭后 Loading 期间可能点到后面 UI |

### NiumaSceneSaveCheckpointRequester
建议挂载位置：`CoreScene/BootstrapRoot/SceneRoot/CheckpointRequester`。

用途：把场景切换中的检查点保存意图转发给 `NiumaSaveController`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Save Controller` | 拖 `SaveRoot` 上的 `NiumaSaveController` | 不建议 | 自动查找失败时检查点保存失败，但切场景仍可继续 |
| `Auto Find Save Controller` | 测试可开，正式建议手动绑定 | 可以 | 关闭且未绑定时无法保存检查点 |
| `Checkpoint Display Name Prefix` | 填检查点名前缀，例如 `进入小游戏前` | 可以 | 为空时使用默认 `Scene Checkpoint` |
| `Write Mode` | 第一版建议 `LocalOnly` | 不可以 | 配错可能让切场景被云同步阻塞 |

### SceneAudioBridge
建议挂载位置：`CoreScene/BootstrapRoot/SceneRoot/SceneAudioRoot`。

用途：根据场景和加载状态播放 BGM、环境音、加载开始/完成/失败音效。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Scene Controller` | 拖 `NiumaSceneController` | 不建议 | 找不到时不响应加载状态音效 |
| `Audio Controller` | 拖 `NiumaAudioController` | 不建议 | 找不到时不播放音频 |
| `Load Started / Completed / Failed Cue` | 填 `AudioCueDefinition.CueId` | 可以 | 留空时对应流程不播放音效 |
| `Scene Audio Cues` | 每项填 SceneName、BGM Cue、Ambient Cue | 可以 | 留空时不按场景切音乐/环境音 |
| `Ambient Channel Id` | 建议 `scene_ambient` | 不建议为空 | 为空时环境音无法播放 |
| `Stop Ambient When Scene Has No Config` | 按需求开启 | 可以 | 关闭后未配置场景会保留旧环境音 |
| `Restart Bgm If Same Scene` | 通常关闭 | 可以 | 开启后重复激活同场景会重播 BGM |

### SceneSpawnPoint
建议挂载位置：业务场景中的 `SpawnPoint_xxx` 物体。

用途：标记玩家进入、返回、复活或传送时的落点。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Spawn Point Id` | 填稳定 ID，例如 `main_default`、`npc_minigame_return` | 不可以 | 返回/传送无法找到正确落点 |
| `Is Default` | 每个场景建议只勾一个默认点 | 可以 | 没有指定点且无默认点时，恢复位置可能失败 |
| `Draw Gizmos` | 建议开启 | 可以 | Scene 视图看不到落点方向 |
| `Gizmos Color` | 默认绿色即可 | 可以 | 只影响 Scene 视图显示 |

### SceneButtonAction
建议挂载位置：业务场景 UI 按钮、门、传送点或对话入口物体。

用途：给 Unity Button / UnityEvent 使用，把无返回值按钮事件转发给 `NiumaSceneController`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Scene Controller` | 拖核心场景 `NiumaSceneController` | 不建议 | 自动查找失败时按钮无效 |
| `Target Scene Name` | 填 Build Settings 中场景名 | 加载场景时不可以 | `LoadConfiguredScene()` 不执行 |
| `Target Spawn Point Id` | 填目标出生点 ID | 可以 | 留空时走目标场景默认点 |
| `Restore Player At Target Spawn Point` | 需要进场定位时开启 | 可以 | 关闭后 `Target Spawn Point Id` 被忽略 |
| `Purpose` | 选 `MiniGame` / `EnterBuilding` / `Teleport` 等 | 可以 | `None` 按普通切换处理 |
| `Push Return Context` | 需要返回时开启 | 可以 | 关闭后 `ReturnToPreviousScene()` 找不到本次来源 |
| `Return Spawn Point Id` | 填返回点 ID，例如 `npc_minigame_return` | 建议填写 | 为空时返回后可能走默认点 |
| `Clear Return Stack Before Push` | 主菜单进游戏可开 | 可以 | 普通入口误开会清掉旧返回链 |
| `Freeze Input During Load` | 正式切场景建议开启 | 可以 | 关闭后加载中玩家可能继续操作 |
| `Show Loading UI` | 跨场景加载建议开启 | 可以 | 关闭后不显示 Loading 面板 |
| `Request Checkpoint Save` | 进入小游戏/副本/剧情节点可开 | 可以 | 关闭后不请求检查点保存 |

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


