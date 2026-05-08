# 去掉冗余 LoadAsync 调用

## 概述

玩家上线时通过 `PlayerOnlineEvent` 已加载所有 PlayerEntity 级 Component 数据到内存。后续 Handler 中重复调用 `LoadBagAsync`/`LoadAttributeAsync` 会再次查 MongoDB，浪费 IO 且逻辑语义错误——应该用内存数据。同时 `LoadBagAsync`/`LoadAttributeAsync` 方法本身也可以删除，加载逻辑内联到 `OnPlayerOnline`。

## 修改文件

### server/Game/Components/Bag/RoleBagSystem.cs: RoleBagSystem
- 删除方法：`LoadBagAsync` — 加载逻辑内联到 `OnPlayerOnline`
- 保留方法：`SaveBagAsync`

### server/Game/Components/Bag/RoleBagHandlers.cs: RoleBagHandlers
- 修改方法：`OnPlayerOnline` — 内联加载逻辑（从 `LoadBagAsync` 搬过来）：`EntityRepo.LoadAsync<RoleBagComponent>` → 拷贝到 `bagComp` → `RebuildIndex`
- 修改方法：`QueryBagHandler` — 去掉 `var bag = await player.RoleBag.LoadBagAsync(player.Id);`，后续 `bag.xxx` 改为 `player.RoleBag.xxx`
- 修改方法：`BagTidyHandler` — 同上
- 修改方法：`BagSwapHandler` — 同上
- 修改方法：`BagUseItemHandler` — 同上

### server/Game/Components/Player/RoleAttributeSystem.cs: RoleAttributeSystem
- 删除方法：`LoadAttributeAsync` — 加载逻辑内联到 `OnPlayerOnline`
- 保留方法：`SaveAttributeAsync`

### server/Game/Components/Player/PlayerHandlers.cs: PlayerHandlers
- 修改方法：`OnPlayerOnline` — 内联加载逻辑（从 `LoadAttributeAsync` 搬过来）：`EntityRepo.LoadAsync<RoleAttributeComponent>` → 拷贝到 `attrComp`

### server/Game/Components/Mail/RoleMailSystem.cs: RoleMailSystem
- 修改方法：`ClaimMailsAttachments` — 去掉 `var bag = await player.RoleBag.LoadBagAsync(self.Id);`，后续 `bag.xxx` 改为 `player.RoleBag.xxx`

### server/Game/Components/GameTask/RoleTaskSystem.cs: RoleTaskSystem
- 修改方法：`SubmitTask` — 去掉 `var bag = await player.RoleBag.LoadBagAsync(roleId);`，后续 `bag.xxx` 改为 `player.RoleBag.xxx`

### server/Game/Components/Monster/MonsterSystem.cs: MonsterSystem
- 修改方法：`GrantItemToPlayer` — 去掉 `var bag = await player.RoleBag.LoadBagAsync(roleId);`，后续 `bag.xxx` 改为 `player.RoleBag.xxx`

## 验证

- [ ] 服务端 `dotnet build server/Game` 编译通过
- [ ] 项目中不再有 `LoadBagAsync` / `LoadAttributeAsync` 调用和方法定义
- [ ] `OnPlayerOnline` 内联的加载逻辑编译正确
