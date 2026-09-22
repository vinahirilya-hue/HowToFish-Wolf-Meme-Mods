# 素材设置

每个插件都会从自己的 `assets` 目录读取两个外部文件：

```text
BepInEx/plugins/<插件名>/assets/howl_flash.png
BepInEx/plugins/<插件名>/assets/howl_bgm.ogg
```

出于版权考虑，这两个文件不会放进 GitHub 仓库或 Release。请使用你有权使用的素材。

如果你已拥有并订阅《杀戮尖塔 2》创意工坊项目 `3792512211`，Steam 通常会把它的本地文件保存到：

```text
<Steam 库>/steamapps/workshop/content/2868840/3792512211/assets
```

将本机素材复制给 `WolfSniperHit` 的示例：

```powershell
.\scripts\copy-assets.ps1 `
  -SourceAssetDir "D:\SteamLibrary\steamapps\workshop\content\2868840\3792512211\assets" `
  -GameDir "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish" `
  -Plugin WolfSniperHit
```

如果要复制给信天翁专用版，将参数改为 `-Plugin AlbatrossWolfHit`。
