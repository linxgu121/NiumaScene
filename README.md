# NiumaScene

## 模块定位
NiumaScene 是场景流程模块，负责统一场景加载、返回上下文、出生点恢复、加载状态、输入冻结、检查点保存意图和场景 UI 桥接。

NiumaUI 2.1.0 后，场景模块的正式 UI 接入只推荐 **UI Toolkit**。旧 UGUI `SceneLoadingPanelBridge` 仅作为历史测试脚本保留，不再作为新场景搭建入口。

## 框架设计思路
- `SceneTransitionRequest` 描述去哪、怎么去、是否记录返回上下文。
- `SceneService` 负责仲裁加载请求、Pending 替换、失败 fallback 和结果句柄。
- 返回上下文用栈保存，支持 RPG -> MiniGame -> RPG 等流程。
- 输入冻结、出生点、检查点保存通过接口适配器接入，不直接依赖 TPC、Interact、Save。
- Loading 表现通过 `ISceneLoadingReceiver` 接入。正式项目优先绑定 `SceneLoadingToolkitBridge`，由 NiumaUI Toolkit 显示 Loading View。

## 核心流程
1. 调用 `NiumaSceneController.LoadScene` 或 `SceneButtonAction.LoadConfiguredScene()`。
2. Service 校验请求并按策略压入 ReturnContext。
3. 加载开始时发布 `SceneLoadingSnapshot`，冻结输入。
4. `SceneLoadingStateBridge` 把快照推给 `SceneLoadingToolkitBridge`。
5. `UIToolkitUIManager` 显示 `Loading` View。
6. `SceneManager` 异步加载目标场景。
7. 场景加载完成后查找 `SceneSpawnPoint` 并恢复玩家位置。
8. 解冻输入，关闭 Loading View，完成 `SceneTransitionHandle`。

## 模块用法
- 进入 MiniGame 时 `Purpose` 使用 `MiniGame`，并开启 `PushReturnContext`。
- 返回点 ID 应配置在 NPC 或入口附近的 `SceneSpawnPoint`。
- 同场景传送可使用 `TeleportToSpawnPoint`，不必重新加载场景。
- Unity Button 不建议直接绑定 `NiumaSceneController.LoadScene`，因为它返回 `SceneTransitionHandle`，不适合作为 Inspector 按钮事件。按钮跳转请使用 `SceneButtonAction`。
- 核心场景搭建请先阅读 [核心场景制作指南.md](核心场景制作指南.md)。

## 核心场景挂载速查
推荐结构：

```text
CoreScene
└── BootstrapRoot
    ├── SceneRoot
    │   ├── LoadingRoot
    │   ├── SceneAudioRoot
    │   └── CheckpointRequester
    ├── UIRoot
    │   ├── EventSystem
    │   ├── UIToolkitRoot
    │   ├── UIManager
    │   ├── DataBridges
    │   ├── UIBridges
    │   └── BindingProviders
    ├── AudioRoot
    ├── SaveRoot
    └── GameplayServicesRoot    # 只放未在其它位置挂过的全局服务 Controller
```

### SceneLoadingToolkitBridge
建议挂载位置：`CoreScene/BootstrapRoot/UIRoot/UIBridges/SceneLoadingToolkitBridge`。

用途：把 `SceneLoadingSnapshot` 转成 NiumaUI Toolkit `Loading` View。

绑定步骤：

1. 在 `UIToolkitViewRegistrySO` 中注册 `Loading` View。
2. 在 `UIToolkitViewFactory.Binding Provider Behaviours` 中拖入 Loading 的 BindingProvider。
3. `SceneLoadingToolkitBridge.UI Manager` 拖 `UIRoot/UIManager` 上的 `UIToolkitUIManager`。
4. `SceneLoadingStateBridge.Loading Receiver Provider` 拖 `SceneLoadingToolkitBridge`。

### SceneLoadingPanelBridge（Legacy）
旧 UGUI/TMP Loading 面板桥接。2.1.0 新场景不推荐使用。已有测试场景如果临时保留，不要和 `SceneLoadingToolkitBridge` 同时绑定。

## UI Toolkit 相关场景约定
- `UIRoot/UIManager` 是物体名，上面挂 `UIToolkitUIManager` 和 `UIToolkitViewFactory`，不要再寻找旧 UGUI `UIManager` 脚本。
- `UIRoot/UIToolkitRoot` 放各层 `UIDocument`。
- `UIRoot/DataBridges` 放业务数据桥接和命令入口。部分现有脚本名仍是 `XxxUIViewBridge`，但它们不是旧 UGUI 绑定，而是把模块 Service 的 ViewData 转成 UIUpdate。
- `UIRoot/UIBridges` 放 `SceneLoadingToolkitBridge`、`InteractionPromptToolkitSink`、`NiumaGalToolkitDialogueViewBridge` 和各模块 `XxxToolkitReceiver`，只负责打开或刷新 Toolkit View。
- `UIRoot/BindingProviders` 放各模块 `XxxToolkitBindingProvider`，并拖到 `UIToolkitViewFactory.Binding Provider Behaviours`。添加组件时搜索脚本类名，不要按文件名找；例如 `GrowthToolkitBindingProvider` 在 `GrowthToolkitBridge.cs` 中，`EffectToolkitBindingProvider` 在 `EffectToolkitBridge.cs` 中。
- `UIToolkitViewRegistrySO` 统一注册 ViewId、LayerId、BindingProviderId、InputPolicy。
- `XxxUIViewBridge` 是历史命名，现在应理解为“业务数据桥接/命令入口”。它通常放在 `UIRoot/DataBridges`，Receiver Provider 拖同模块 `XxxToolkitReceiver`，不要把它当旧 UGUI 面板脚本挂到按钮或图片文字上。
- `GameplayServicesRoot` 只放全局常驻且没有在其它位置挂过的服务 Controller。同一个模块 Controller 不要在核心场景和业务场景各挂一份；Controller 不是单例，重复挂载会覆盖 `GameContext` 注册并让 UI、存档、桥接拿到不同实例。

## 常见错误
### ReturnContextMissing
入口没有开启 `PushReturnContext`，或 `NiumaSceneController` 没放核心场景常驻。

### Loading 不显示
- `SceneLoadingStateBridge.Loading Receiver Provider` 没拖 `SceneLoadingToolkitBridge`。
- `SceneLoadingToolkitBridge.UI Manager` 没绑定 `UIToolkitUIManager`。
- `UIToolkitViewRegistrySO` 没注册 `Loading` View。
- `UIToolkitViewFactory.Layer Roots` 没配置 Loading 层的 `UIDocument`。

### 业务面板打开但没有内容
- 对应 `XxxToolkitBindingProvider` 没拖进 `UIToolkitViewFactory.Binding Provider Behaviours`。
- 对应 `XxxUIViewBridge` 的 Receiver Provider 没拖同模块 `XxxToolkitReceiver`。
- 同模块 Controller 重复挂载，DataBridge 和 SaveAdapter 绑定到了不同实例。

## 协作边界
Scene 不直接保存文件、不直接控制玩家内部逻辑，只通过接口发出冻结、出生点、检查点和 Loading 表现意图。

## 配置资产粒度基准

NiumaScene 当前优先使用场景组件配置，不建议为每个场景额外制作业务资产。

- 场景名、出生点 ID、返回栈、加载状态由 `NiumaSceneController` 和场景内 SpawnPoint / Bridge 脚本管理。
- 传送门、场景入口等通常是场景实例脚本，填写目标场景名和出生点 ID。
- 如果后期需要场景目录、章节地图或加载规则复用，可以再新增 Scene Catalog 资产；第一版不要做全局巨表。

场景切换中的临时请求、返回上下文和加载状态都是运行时数据，不进入配置资产。
