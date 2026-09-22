# 《渔力全开》狼性文化 Mod 合集

[English](README.md)

这是两个相互独立的《How to Fish / 渔力全开》客户端 BepInEx Mod，用来实现“闪狼人图 + `Animals` 连续变速”的梗。

| Mod | 触发条件 | 版本 |
| --- | --- | --- |
| `WolfSniperHit` | 本地玩家手持大狙并真正造成远程命中 | 0.1.1 |
| `AlbatrossWolfHit` | 信天翁的 NPC 投射物命中本地玩家 | 0.1.0 |

每次触发都会从头播放音频，连续触发按 `1.1 倍`累计加速，最高 `2 倍`，同时短暂全屏闪图和抖动。两个插件互不依赖，可以单独启用。

## 前置条件

- 《渔力全开》`1.0.12`（开发和验证使用的版本）
- [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
- 由你自己合法取得的图片和音频素材

## 安装

1. 在 `How to Fish.exe` 所在目录安装 BepInEx。
2. 从 [Releases](https://github.com/vinahirilya-hue/HowToFish-Wolf-Meme-Mods/releases) 下载需要的 ZIP。
3. 将 ZIP 解压到游戏目录。
4. 把以下文件放进对应插件的 `assets` 文件夹：
   - `howl_flash.png`
   - `howl_bgm.ogg`
5. 启动一次游戏，配置文件会自动生成在 `BepInEx/config`。

本机素材复制示例和准确目录参见[素材设置说明](docs/ASSETS.zh-CN.md)。

## 配置文件

- `BepInEx/config/local.wjy13.wolfsniperhit.cfg`
- `BepInEx/config/local.wjy13.albatrosswolfhit.cfg`

可调节音量、淡入淡出时间、抖动强度、每次加速倍率、最高倍速和重置间隔。`WolfSniperHit` 还可以设置 `OnlyOnKill=true`，改成仅击杀触发。

## 本地构建

仓库不分发游戏程序集和 BepInEx 二进制文件，请使用自己的本地安装目录构建：

```powershell
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish"
```

或者设置 `HTF_GAME_DIR` 后直接运行 `dotnet build`：

```powershell
$env:HTF_GAME_DIR = "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish"
dotnet build .\HowToFish.WolfMemeMods.sln
```

## 版权说明

本仓库及 Release **不包含** `Animals`、狼人图片、游戏程序集或其他创意工坊 Mod 的文件。详见[第三方声明](THIRD_PARTY_NOTICES.md)。

## 许可证

源码采用 [MIT License](LICENSE)。
