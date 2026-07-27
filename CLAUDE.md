# DDSurrender

## 项目概述

SCP: Secret Laboratory 的 D级投降插件，基于 EXILED 框架。允许 D级人员通过 `.tx` 指令投降，投降后与 MTF 阵营友好、与混沌敌对，逃离后转生为九尾收容专家。

## 文件结构

```
DDSurrender/
├── DDSurrenderPlugin.cs   # 插件主体：事件处理、状态机、CustomInfo 槽位系统
├── SurrenderCommand.cs    # .tx 指令实现
├── Config.cs              # 所有可配置文本和开关
├── CustomFaction.cs       # 玩家状态枚举（DD_Surrendered / MTF_ContainmentExpert）
├── Properties/
│   └── AssemblyInfo.cs    # 程序集版本（与 Plugin.Version 保持一致）
├── DDSurrender.csproj     # .NET 4.8.1 旧式项目，引用 ExMod.Exiled NuGet 包
├── packages.config        # NuGet 包版本声明
└── using/                 # 游戏原生 DLL（不入 git，需从服务器手动复制）
```

## 构建

1. 将服务器的 `Assembly-CSharp.dll`、`Assembly-CSharp-firstpass.dll`、`Mirror.dll` 等放入 `using/`
2. Visual Studio 2022 打开 `DDSurrender.sln`，NuGet 自动还原 `ExMod.Exiled 9.14.2`
3. 选 `Release` 配置 → 生成
4. 产物：`bin/Release/DDSurrender.dll`

命令行构建（需 VS2022）：
```bash
MSBUILD="D:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" DDSurrender.csproj /p:Configuration=Release /t:Rebuild
```

## 关键设计

### 状态机

玩家状态存储在 `_factionStates: ConcurrentDictionary<UserId, CustomFaction>`：
- 无条目 = 普通 D级
- `DD_Surrendered` = 已投降

黑名单 `_blacklist: HashSet<UserId>` 标记"造反"（攻击过 MTF/科学家的 D级），造反后无法投降。

所有状态在以下时机清除：`OnPlayerLeft` / `OnPlayerDied` / `OnPlayerChangingRole` / `OnPlayerSpawned` / `OnRoundEnded`。

### CustomInfo 槽位系统

`_infoFragments: ConcurrentDictionary<UserId, Dictionary<slotId, text>>` 允许多个来源写入玩家头顶标签而不互相覆盖。

| 槽位 ID | 用途 |
|---------|------|
| 100 | DDSurrender 自身（投降/造反标签） |
| 50 | 预留给其他插件 |

其他插件调用 `SetPlayerInfoFragment(player, slotId, fragment)` 写入自定义槽位。

### 命令注册时机

命令必须在 `WaitingForPlayers` 事件中注册（而非 `OnEnabled`），否则在服务器启动阶段 `CommandProcessor` 尚未就绪。`_surrenderCommand` 字段持有实例引用，`OnDisabled` 时直接注销，避免 reload 后重复注册。

### 伤害逻辑（OnPlayerHurt）

仅在 `Server.FriendlyFire == false` 时介入（每次实时读取，不缓存）：
- 投降 D ↔ 混沌：允许互打（强制友伤标记绕过保护）
- 投降 D ↔ MTF/科学家：阻止伤害
- 未投降 D 攻击 MTF/科学家：加入黑名单，标记造反

## 版本对应

| 插件版本 | EXILED | 说明 |
|----------|--------|------|
| v3.0.0 | 9.14.2+ | 当前版本 |
| v2.5.6 | 9.12.6 | 旧版，不再维护 |
