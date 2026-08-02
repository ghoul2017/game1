# ProductContent 模块

目标读者：人类。

## 定位

ProductContent 是 T6 的最小双产品隔离层。它位于无外部依赖的独立程序集，不复制 `GameplayCore`，也不建立
插件市场；`ProductExpedition` 作为窄组合层单向引用二者，把产品规则接入真实远征。

## 当前 Profile

| ProductId | 内容 | 专属规则 |
| --- | --- | --- |
| `labyrinth` | 中性迷宫占位单位、敌人、资源数和主题色 | 无局内时限 |
| `cosmic_legion_placeholder` | 中性宇宙军团占位单位、敌人、资源数和独立主题 token | 180 秒规则 Tick 时限 |

`ProductCatalog` 是唯一静态目录；`ProductProfile` 聚合规则、主题和内容包，并在构造时复制单位数据，避免产品间
共享可变集合。正式品牌名、角色、美术、音频、授权和渠道信息没有进入共享核心。

`ProductExpeditionFactory` 把 Profile 的队伍、敌人、资源数量、Tick 率和时限组合进唯一规则对象。本地
`LocalCampaignHost` 与服务端 `GameplayCoreExpeditionSimulationFactory` 都消费该工厂；`.csproj` 与 `.asmdef`
只是 .NET/Unity 的编译描述，二者链接同一份物理源码，不存在复制实现。

## 180 秒规则

`ProductRunTimer` 只读取 `ProductRuleProfile.MaximumRunSeconds` 与明确的 Tick 率，不读取墙钟。第二占位 Profile
在 10 Hz 下精确于第 1800 Tick 首次进入过期状态；之后继续推进不会重复触发。首作 Profile 仍推进当前 Tick，
但永不进入过期状态。

`ProductExpeditionRun` 在每局构造时固定 ProductId，是唯一规则时钟；运行中不能切换产品或重置时限。第二 Profile
第 1800 Tick 调用 `GameplayCore` 的产品中立 `TryFail` 入口，产生唯一 Failed 结果并进入现有失败结算；重复推进
不再改变结果，下一局创建新组合并重置为 1800 Tick。首作 Profile 没有时限。

Unity 的 `ProductProfileRuntimeEntry` 只消费内容 ID 和主题，不复制规则时钟。Build Settings index 2 的
`CosmicLegionPlaceholderScene` 把第二 Profile 的主题颜色应用到真实 `ExpeditionWorldView` 相机，并由同一
`LocalCampaignHost` 推进限时规则；新局重建相机时重新绑定主题。

## 验证边界

ProductContent/ProductExpedition 测试当前 `15/15`。两个 Profile 的唯一性、不可变隔离、主题/内容差异、
1799/1800 Tick 边界、唯一失败结算、新局重置、真实场景和服务端同源接线已有自动化。目标运行环境、人工体验
以及正式 IP 角色、美术、音频、授权和渠道内容仍属后续
门禁，见 `TEST-T6-TARGET-01`。
