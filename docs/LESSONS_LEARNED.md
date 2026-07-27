# 经验总结：可复用到其他 SCP:SL 插件项目

这份是从 DDSurrender v3.0.0 这轮升级/审查/发布里提炼出的通用经验，不是 DDSurrender 专属的（那部分在 `HANDOFF.md`）。可以直接复制到你其他插件项目的 CLAUDE.md 或团队规范里。

## 1. "审查确认没问题" ≠ "能编译"

这轮最大的教训：有一次会话把代码审查里发现的问题都改完、提交、推送了，但从头到尾没跑过一次真实编译。结果里面藏着一个真正的编译错误（EXILED 9.14.2 把 `WaitingForPlayers` 从标准 C# event 改成了自定义 `Event` 类，`+=`/`-=` 直接编译不过），一直没被发现，直到这轮真的执行 `MSBuild` 才暴露。

**规则**：任何代码改动，不管审查多仔细，落地前必须跑一次真实构建（`MSBuild ... /t:Rebuild`），看到 `0 个错误` 才算数。不要因为"逻辑上应该没问题"就跳过这一步，尤其是刚做完框架版本升级之后。

## 2. 升级依赖大版本号时，重点检查事件/委托的 API 形态有没有变

EXILED 从某个版本开始，把部分 `Handlers.*` 的事件从"标准 C# event（`+=`/`-=`）"改成了自定义 `Event<T>` / `Event` 特性类（需要 `.Subscribe()` / `.Unsubscribe()`，委托类型也变成 `CustomEventHandler` 或 `CustomEventHandler<T>`）。这种改动编译器会直接报错（不是运行时才炸），但如果你是从旧代码"复制粘贴"或凭记忆写的，很容易连着错误一起复制过去。

**规则**：升级 EXILED（或任何插件框架）大版本时：
- 别只看 changelog 里写的"新增/移除 API"，实际去过一遍你用到的每个 `Handlers.*` 事件的签名（IDE 跳转定义，或反射 `GetType()`/`GetMembers()` 确认）。
- 编译报 `CS0019: 运算符"+="无法应用于"Event"和"XXX"类型` 这种错，基本就是这个模式，直接查该事件的 `Subscribe`/`Unsubscribe` 方法签名改就行。

## 3. 不要把第三方/游戏专有 DLL 提交进 git

`bin/`、`obj/`、`.vs/`、`packages/`（NuGet 还原产物）、`using/`（从游戏服务器复制出来的 `Assembly-CSharp.dll`、`Mirror.dll` 等 Northwood/Unity 专有文件）如果不加 `.gitignore`，很容易在项目早期就被整个提交进去，之后越滚越大。

**规则**：新建插件项目第一次 commit 前，先加好 `.gitignore`：
```
bin/
obj/
.vs/
packages/
using/
*.user
*.suo
*.cache
```
并在 README/CLAUDE.md 里写清楚这些目录怎么重新生成（NuGet 还原、从服务器手动复制哪些 DLL）。这样仓库体积小，也避免分发游戏商专有文件带来的版权风险。

## 4. 玩家状态缓存要审计"在哪些时机清理"，而不是只看"在哪里写入"

这轮修的几个内存泄漏（`_infoFragments` 永不清理、`_blacklist` 用 `List` 无限重复累加）都是同一类问题：为单个玩家维护的字典/集合，只关注了"什么时候写入"，没有系统性检查"这个玩家离开/死亡/换角色/回合结束时，这份数据有没有被清掉"。

**规则**：任何 `ConcurrentDictionary<UserId, T>` 或类似的按玩家 key 的缓存，写完之后拉一张表，把 `OnPlayerLeft` / `OnPlayerDied` / `OnPlayerChangingRole` / `OnPlayerSpawned` / `OnRoundEnded` 这几个生命周期事件都列出来，逐个确认这份缓存要不要在这个时机清理。别漏掉某个字典。

## 5. 用容器语义匹配实际用途：去重/存在性判断用 `HashSet`，不要用 `List`

`List<T>.Add` 在"记录某个状态是否发生过"这种场景下很容易被误用（同一个 key 反复 add，导致列表无限增长，即使逻辑上只是想要一个 flag）。

**规则**：语义是"这个东西发生过 / 存在"就用 `HashSet<T>`（自动去重、O(1) 查找），只有真的需要保留顺序或重复项时才用 `List<T>`。

## 6. 运行时会变的服务器配置，不要在构造函数/OnEnabled 里缓存一次

比如 `Server.FriendlyFire` 这类可以被管理员实时切换的全局开关，如果在插件启用时读一次存进字段，后面管理员改了开关，插件行为就跟实际服务器状态脱节了。

**规则**：这种"运行时可变的全局状态"，该在每次真正用到的地方（比如 `OnPlayerHurt` 里）实时读取，不要缓存。缓存的前提必须是"这个值在插件生命周期内不会变"。

## 7. manifest/声明的依赖版本号要和实际引用的包版本对齐

`RequiredExiledVersion`（或者其他框架的等价字段）这种"声明我需要哪个版本"的字段，很容易在升级 NuGet 包之后忘记同步改，导致声明和实际编译引用的版本不一致——轻则误导排障，重则让 loader 版本校验行为异常。

**规则**：升级依赖包版本后，搜一下项目里所有写死版本号的地方（`RequiredExiledVersion`、`AssemblyInfo.cs` 里的 `AssemblyVersion`、README 里的兼容表），一次性同步。

## 8. 命令注册要在框架就绪的时机做，注销要能精确对应

命令处理器（`CommandProcessor` 之类）在服务器刚启动、插件 `OnEnabled` 阶段可能还没初始化好，得等到框架明确广播"准备好了"的事件（比如 EXILED 的 `WaitingForPlayers`）才注册命令；对应的注销也要在 `OnDisabled` 里用同一个具名引用去做，不能用匿名 lambda（匿名委托无法用来 `-=`/`Unsubscribe` 精确移除,会导致 reload 后处理器堆积或残留）。

**规则**：任何"注册/注销"成对出现的操作（事件订阅、命令注册、Harmony patch），都要保留一个字段持有具体的委托/实例引用，`OnDisabled` 里用这个引用做逆操作，别用匿名函数图省事。

## 9. 会话中断后恢复时，用命令核实状态，别凭记忆认为"应该已经做完了"

这轮有一次会话因为代理问题中断，恢复后没有直接假设"之前的修复肯定都对"，而是重新跑了 `git diff` 对比 origin、重新跑了一次构建，才发现藏着的编译错误。如果当时直接信任"审查已确认修复"就直接发布，发出去的 Release 就是个编译不过的版本。

**规则**：任何长会话、多次中断恢复的任务，恢复后先用 `git status` / `git diff` / 重新跑一次构建或测试来核实真实状态，不要依赖对话记忆里"我之前说已经修好了"。

---

## 快速检查清单（新插件 / 升级依赖时过一遍）

- [ ] `.gitignore` 排除 `bin/ obj/ .vs/ packages/ using/`
- [ ] 升级框架大版本后，跑一次真实 `Release` 构建，`0 个错误`才算完
- [ ] 检查所有 `Handlers.*` 事件订阅点，确认事件类型（标准 event vs 自定义 `Event` 类）没变
- [ ] 玩家状态缓存（字典/集合）在 Left/Died/ChangingRole/Spawned/RoundEnded 都有对应清理
- [ ] 存在性判断类容器用 `HashSet` 不用 `List`
- [ ] 运行时可变的服务器配置不缓存，用时实时读
- [ ] manifest 声明的版本号和实际依赖包版本一致
- [ ] 事件/命令注册注销用具名引用，不用匿名 lambda
- [ ] 发布前跑构建产物时间戳确认是"刚编译的"，不是旧缓存
