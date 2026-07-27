# DDSurrender

![Exiled](https://img.shields.io/badge/Exiled-9.14.2+-blue)
![Version](https://img.shields.io/badge/Version-3.0.0-green)
![SCP:SL](https://img.shields.io/badge/SCP%3ASL-14.x-orange)

SCP: Secret Laboratory 服务器的 **D级投降** 插件，基于 EXILED 框架开发。

## 功能

- D级人员可在客户端控制台输入 `.tx` 进行投降
- 投降后的D级人员与混沌分裂者**互为敌对**（可互相攻击），与MTF/科学家/保安视为**友军**（无法互相伤害）
- 未投降的D级人员若攻击MTF/科学家，将被标记为**造反**，无法再投降
- 投降D级逃离后自动转生为**九尾收容专家**
- 仅在**关闭友伤（FriendlyFire=false）**的服务器上生效
- 重复投降有彩蛋 👀
- 提供 API 供其他插件向玩家 CustomInfo 写入标签片段（槽位系统）

## 安装

1. 将 `DDSurrender.dll` 放入服务器的 `Exiled/Plugins` 目录
2. 启动服务器，插件会自动生成配置文件
3. 在 `Exiled/Configs/<port>-config.yml` 中找到 `dd_surrender` 节点进行配置

## 配置项

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `is_enabled` | `true` | 是否启用插件 |
| `debug` | `false` | 调试模式 |
| `su_again` | `⚠️ 重复投降，视为反水...` | 重复投降时的提示（彩蛋） |
| `su_success` | `✅ 投降成功！...` | 投降成功提示 |
| `zaofan` | `❌ 都早饭了还想投降?!...` | 造反后尝试投降的提示 |
| `not_class_d` | `❌ 仅D级人员可投降...` | 非D级使用指令的提示 |
| `failed_caused_unknown` | `❌ 投降失败! 原因未知...` | 未知原因失败提示 |
| `su_dd` | `【已投降】` | 投降状态标签 |
| `zfdd` | `【造反中】` | 造反状态标签 |
| `command_description` | `[DDSurrender] D级投降插件...` | `.help` 中显示的指令描述 |
| `broadcast_for_dd` | `你成为了D级人员...` | D级出生时的广播内容 |

## 指令

| 指令 | 别名 | 使用方 | 说明 |
|------|------|--------|------|
| `.tx` | `.ddsurrender` `.surrender` | 客户端控制台 | D级投降 |

## 开发者 API

其他插件可通过以下方式向玩家 CustomInfo 写入自定义标签片段，与 DDSurrender 的标签共存：

```csharp
// 写入标签（slotId 请使用独特的数字避免冲突，DDSurrender 使用 100）
DDSurrenderPlugin.Instance.SetPlayerInfoFragment(player, slotId: 200, fragment: "【我的标签】");

// 移除标签
DDSurrenderPlugin.Instance.SetPlayerInfoFragment(player, slotId: 200, fragment: null);
```

## 编译

1. 安装 Visual Studio 2022
2. 克隆仓库：
   ```bash
   git clone https://github.com/whystars/DDSurrender.git
   ```
3. 用 Visual Studio 打开 `DDSurrender.sln`
4. NuGet 会自动还原 `ExMod.Exiled` 包
5. 将服务器的 `Assembly-CSharp.dll`、`Assembly-CSharp-firstpass.dll` 等 Unity/游戏 DLL 放入 `using/` 目录
6. 选择 `Release` 配置，生成项目
7. 产物位于 `bin/Release/DDSurrender.dll`

## 版本历史

| 版本 | EXILED | SCP:SL | 说明 |
|------|--------|--------|------|
| v3.0.0 | 9.14.2+ | 14.x | 修复命令注销/事件泄漏/状态管理等问题，清理死代码 |
| v2.5.6 | 9.12.6 | 14.x | EXILED 9.12.6 的最后兼容版本 |

> ⚠️ 如果你的服务器仍在使用 EXILED 9.12.6，请使用 [v2.5.6](https://github.com/whystars/DDSurrender/releases/tag/v2.5.6)。

## 许可证

本项目基于 GPL-3.0 许可证开源，详见 [LICENSE](LICENSE)。
