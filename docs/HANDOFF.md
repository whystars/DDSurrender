# 交接文档

写给下一个接手这个项目的 agent（或未来的你）。先看这份，再看 `CLAUDE.md`（架构设计）和 `README.md`（用户向说明）。

## 当前状态（2026-07-27）

- **已发布 v3.0.0**，tag 和 GitHub Release 都已推送，DLL 已附加到 Release：
  https://github.com/whystars/DDSurrender/releases/tag/v3.0.0
- 依赖：`ExMod.Exiled 9.14.2`（NuGet 包名，实际是 EXILED）
- 本地构建已验证通过（MSBuild Release，0 错误），**没有在真实 SCP:SL 服务器上做过运行时验证**。
- `main` 分支比 v3.0.0 tag 多 1 个提交（`fb8addb`），是构建时才发现的编译错误修复，已经推送。tag 指向的是这次修复之前的提交 —— 如果要重新打包 Release DLL，**以 `main` 最新 commit 为准**，不要用 tag 那次的构建产物。

## 这轮做了什么

1. 本地代码此前已升级到 EXILED 9.14.2（旧 Release 停在 v2.5.6 / EXILED 9.12.6），但从没验证过能编译。
2. 审查发现并修复了 7 类问题：命令注销失效、`WaitingForPlayers` 用匿名 lambda 无法注销、`RequiredExiledVersion` 写着 9.6.3 却按 9.14.2 编译、`_blacklist` 用 `List` 会无限重复累加、`_infoFragments` 永不清理、`FriendlyFire` 只在构造时读一次。详见 commit `03c1d95`。
3. **实际跑 MSBuild 构建时才发现**：上面第 2 步里把 `WaitingForPlayers` 从匿名 lambda 改成具名委托时，用的是 `System.Action` + `+=`/`-=`。但 EXILED 9.14.2 把 `Server.WaitingForPlayers` 从标准 C# event 改成了自定义 `Event` 特性类，不支持 `+=`/`-=`，必须用 `.Subscribe()`/`.Unsubscribe()`，委托类型也要换成 `Exiled.Events.Features.CustomEventHandler`。修复在 commit `fb8addb`。
4. 仓库清理：`bin/`、`obj/`、`.vs/`、`packages/`、`using/` 移出版本控制，加了 `.gitignore`（这些目录之前把 Northwood/Unity 的专有 DLL 也提交进去了）。

## 本机环境的几个坑（省得你重新踩一遍）

- **Git 走代理**：这台机器上 git 不会自动读系统代理，得手动配置：
  ```bash
  git config --global http.proxy http://127.0.0.1:5502
  git config --global https.proxy http://127.0.0.1:5502
  ```
  端口 `5502` 是用户的"隐云"客户端本地代理端口，如果用户换了代理客户端，端口可能变，连不上 GitHub 时先检查这个。
- **Git Bash 会吞掉 MSBuild 的参数**：`git bash` 环境下直接跑 `MSBuild.exe DDSurrender.csproj /p:Configuration=Release` 会被 MSYS2 的路径转换层把 `/p:...` 之类的开头 `/` 当成 Unix 根路径处理，导致参数丢失或异常。解决办法是加环境变量禁用转换：
  ```bash
  MSYS2_ARG_CONV_EXCL="*" "$MSBUILD" DDSurrender.csproj -p:Configuration=Release -t:Rebuild
  ```
  注意用 `-p:`/`-t:` 单杠形式更安全，双重保险。
- **`gh` CLI 认证方式**：这台机器上 `gh auth status` 显示 keyring 登录已失效，但 `GITHUB_TOKEN` 环境变量是有效的，`gh` 会自动优先用它，实际是可用的，不用重新 `gh auth login`。
- **构建依赖目录不在 git 里**：`packages/`（NuGet 还原的 EXILED 包）和 `using/`（游戏服务器目录复制出来的 `Assembly-CSharp.dll`、`Mirror.dll` 等）都被 `.gitignore` 排除了，本机上它们还在磁盘上、构建能跑，但如果换一台机器或 clone 一份新仓库，这两个目录要重新准备（NuGet 还原 + 从服务器手动复制），具体步骤见 `README.md` / `CLAUDE.md` 的构建章节。

## 待验证 / 可能的后续工作

- **没有真实服务器验证**。建议接手时先在测试服跑一遍：`.tx` 投降流程、友伤规则（投降 D 对 MTF/混沌）、逃生转生九尾收容专家、插件 reload 后命令不重复注册。
- 构建日志里有 9 条 `MSB3270` 警告（`MSIL` 项目引用 `AMD64` 的 EXILED 程序集，架构不匹配）。目前不影响构建结果，但如果以后真机测试出现奇怪的加载期异常，先看这个警告是不是元凶。
- `LICENSE`、`README.md` 是这次会话之前已有的，没有改动。

## 关键文件速查

| 文件 | 作用 |
|---|---|
| `DDSurrenderPlugin.cs` | 主体，事件订阅/退订、状态机、CustomInfo 槽位 |
| `SurrenderCommand.cs` | `.tx` 指令 |
| `Config.cs` | 配置项 |
| `CustomFaction.cs` | 状态枚举 |
| `CLAUDE.md` | 架构设计说明（状态机、CustomInfo 槽位系统、命令注册时机、伤害逻辑），改代码前先看这个 |
