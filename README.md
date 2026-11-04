# 秘传道场 / HidenDojo

**这是作用于《将军对决 / Shogun Showdown》的游戏 Mod，不是独立游戏。** 当前源码版本为 **0.3.4**，面向 macOS Steam 版，通过 BepInEx 5 / Harmony 接入游戏的原生暂停菜单、战斗与商店逻辑。

HidenDojo is a macOS Steam mod for **Shogun Showdown**. It adds an in-game configuration menu, numeric weapon upgrades, optional economy/turn rules, and a dedicated shop reroll button. This repository contains source code and plugin-only installation helpers, not the game or BepInEx binaries.

- 对应游戏：[Shogun Showdown（Steam，App ID 2084000）](https://store.steampowered.com/app/2084000/Shogun_Showdown/)
- 插件标识：`tong.shogunshowdown.hidendojo`
- 生成文件：`HidenDojo.dll`
- 配置文件：游戏目录下的 `BepInEx/config/tong.shogunshowdown.hidendojo.cfg`

## 能做什么

在游戏的**暂停菜单 → 秘传道场**中调整当前装备。武器按种类去重，设置较多时分页显示。

| 武器档位 | 数值规则 |
| --- | --- |
| 原式 | 原版伤害与冷却 |
| 试炼式 | 伤害 +1，冷却不变 |
| 传承式 | 伤害 +2，冷却 -1 |
| 霸道式 | 伤害 +3，冷却 -3 |

数值强化会刷新当前装备的牌面显示；冷却最低可降至 **0**。实现会记录自己施加的冷却减免，尽量保留游戏原有升级造成的变化。这里仅有数值强化，没有为每种武器增加独立专属招式。

另外提供以下可开关规则：

| 配置项 | 默认值 | 实际作用 |
| --- | --- | --- |
| `Enabled` | `true` | 总开关；控制入口及大多数功能补丁。修改后建议退出并重启游戏核对，不用于撤销已经发生的进度变化 |
| `护命` | `true` | 当前生命大于 1 时，每场战斗第一次受到致命伤害改为保留 1 点生命；不是持续无敌 |
| `万能刷新` | `true` | 商店、武器选择及升级奖励的刷新规则；也是固定商店刷新按钮的开关 |
| `霸道式不耗回合` | `true` | 霸道式武器放入攻击队列时清除本次待触发的敌方回合标记；攻击、队列和冷却逻辑仍执行 |
| `免费消费` | `true` | 免除商店购买、解锁、武器升级和付费服务的金币、骷髅或生命支付；保留原有升级槽位限制 |

**默认全部开启，会显著改变原版经济和回合规则。** 若要恢复奖励界面的收费刷新，需要同时关闭“万能刷新”和“免费消费”：当前源码的奖励刷新策略会在任一项开启时生效。固定商店刷新按钮则单独遵循“万能刷新”。

开始**新游戏**时，各武器强化档位重置为原式；继续现有对局不会按这个新局规则清空档位。

## 怎样操作

- 在暂停菜单打开“秘传道场”。沿用原生菜单导航：上下选择、左右调整、确认键切换，取消键返回。
- 手柄入口按游戏原生导航处理；代码接入了 Xbox **LB / RB** 翻页、**A** 确认和 **B** 返回。
- 商店“出发”按钮上方有固定的 **“刷新商店 · 免费”** 按钮。鼠标可直接点击；沿右侧商品继续向右导航也可选中该按钮。
- 刷新期间暂时禁用再次刷新、购买和离开，完成后恢复交互。
- 这个按钮只刷新**右侧商品货架**，左侧武器升级项目使用原本的刷新入口。空货架分支已在代码中处理，但没有对应的历史实机验证记录。

## 环境与依赖

历史加载记录对应 **macOS Steam 版、BepInEx 5.4.23.2、Unity 2021.3.43f1**。这是已有记录中的环境，不表示所有更新后的游戏版本都经过验证。

运行时需要你自行安装原游戏与适用于该游戏的 **BepInEx 5 Mono** 加载器。当前工程没有声明 BepInEx 6、IL2CPP、Windows、Linux 或 Apple Silicon 原生兼容。

构建需要支持 `net472` 的 .NET SDK，以及 .NET Framework 4.7.2 引用程序集。游戏的 `Managed` 目录和已经安装的 BepInEx `core` 目录提供编译引用。本仓库不附带 Unity、游戏程序集、Harmony 或 BepInEx DLL，也不附带加载器的原版脚本。

## 从源码构建

在仓库根目录执行：

```sh
dotnet build 秘传道场.csproj -c Release
```

默认游戏根目录为当前用户的：

```text
~/Library/Application Support/Steam/steamapps/common/Shogun Showdown
```

如果 Steam 库位于其他位置，指定包含 `ShogunShowdown.app` 与 `BepInEx` 的目录：

```sh
dotnet build 秘传道场.csproj -c Release \
  -p:GamePath="/path/to/steamapps/common/Shogun Showdown"
```

引用程序集由 `GamePath` 推导。请保留 `Private=false`，避免把原游戏或加载器 DLL 复制到发布包。成功构建后，插件位于 `bin/Release/net472/HidenDojo.dll`。

## 安装

1. 自行安装 BepInEx 5 Mono，并先确认它可以通过 Steam 加载此游戏。
2. **完全退出游戏**，构建本 Mod。
3. 在仓库根目录运行：

   ```sh
   bash install.sh
   ```

   非默认游戏位置或自备插件文件可使用：

   ```sh
   STEAM_SHOGUN_ROOT="/path/to/Shogun Showdown" \
     bash install.sh "/path/to/HidenDojo.dll"
   ```

4. 从 Steam 启动游戏，在 BepInEx 日志中查找 `Loading [秘传道场 0.3.4]`，再打开暂停菜单检查入口。

安装脚本只替换 `BepInEx/plugins/HidenDojo.dll`。若已有同名普通文件，先在游戏根目录的 `HidenDojo-backups/` 下创建独立备份，再通过暂存文件替换。脚本检查游戏进程、基本目录和加载器文件；不复制整个加载器，不改写 `Info.plist`、`Assembly-CSharp.dll`、Steam 账户配置或已有 Mod 配置。

如果需要配置 macOS Steam 的加载器启动选项，请按 [BepInEx 官方 Steam 启动说明](https://docs.bepinex.dev/articles/advanced/steam_interop.html)操作。官方说明使用的 macOS 形式是：

```text
"<游戏根目录的绝对路径>/run_bepinex.sh" %command%
```

路径两侧的引号保留，`%command%` 保留为 Steam 占位符。本仓库不提供直接改写某个 Steam 账户配置文件的脚本。

## 配置与卸载

首次成功加载时会生成独立配置。`config/` 下仅提供 `.cfg.example` 示例，安装脚本不会用它覆盖现有设置。个别武器的键由游戏内部武器标识生成，档位为 `0` 至 `3`；关闭游戏后也可手动编辑配置。

要停用插件，先完全退出游戏，再运行：

```sh
bash restore.sh
```

非默认位置同样通过 `STEAM_SHOGUN_ROOT` 指定。此脚本把当前 `HidenDojo.dll` 移到独立备份目录，保留配置、其他 Mod 和 BepInEx。重复执行而插件已移除时不做其他处理。需要恢复旧插件时，退出游戏后把选定备份中的 `HidenDojo.dll` 复制回 `BepInEx/plugins/`，不要把备份目录放进 `plugins` 里。

本源码版的安装与停用脚本不承担旧整合包的加载器恢复工作，也不会自动还原其他版本曾改写的 Steam 启动设置。若之前使用过旧整合包，应依据当时的备份处理，避免覆盖其他 Mod 共用的文件。

## 对存档的影响

本仓库的安装、停用脚本不读取或改写游戏存档。但 Mod 运行后会改变对局、购买和升级结果，并调用游戏原本的刷新与保存流程；这些游戏行为可能进入正常存档。卸载插件不会撤销已经获得的物品、进度或花费变化。准备长期使用前，请自行保留原存档副本。

## 源码与验证

| 文件 | 作用 |
| --- | --- |
| `Plugin.cs` | BepInEx 入口、暂停设置页、武器档位、护命和主要规则补丁 |
| `EconomyAndCooldown.cs` | 冷却减免/序列化处理、免费价格与支付规则 |
| `ShopRefreshButton.cs` | 固定刷新按钮、刷新互斥与商店导航 |
| `秘传道场.csproj` | `net472` 构建及本地游戏/加载器引用 |
| `install.sh` / `restore.sh` | 仅处理本插件 DLL 的安装与停用 |
| `tests/test_installation.py` | 在临时假游戏目录验证文件保护与错误分支 |

不需要游戏即可运行文件操作测试：

```sh
python3 -m unittest discover -s tests -v
```

本次源码整理完成的检查：三份游戏功能 C# 源码与原件逐字节一致；构建 XML 与 15 个引用路径已检查；Shell 语法检查；五项临时目录测试覆盖更新后备份、停用与重复停用、保留其他 Mod/配置/加载器、缺少加载器、运行中拒绝、进程检查失败拒绝以及符号链接目标拒绝。没有在真实 Steam 目录执行安装或停用。

历史 **0.3.4** 记录记载：编译为零警告、零错误；Mac 隔离测试存档中固定按钮可见，连续两次刷新商品改变且金币保持 10，刷新后能离开商店。该记录未覆盖实体 Xbox 手柄操作和空货架场景。

**本次没有重新编译 C# 或启动游戏**，没有据此宣称当前游戏版本的全部功能兼容。实体手柄、空货架、多 Mod 组合和游戏更新后的兼容性仍需针对实际环境验证。

原游戏与加载器的权利归各自权利人。本仓库没有重新分发这些文件；使用对应依赖时请遵守其许可。仓库可见性不替代依赖本身的授权。
