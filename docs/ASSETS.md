# Asset setup

Each plugin loads two external files from its own `assets` directory:

```text
BepInEx/plugins/<PluginName>/assets/howl_flash.png
BepInEx/plugins/<PluginName>/assets/howl_bgm.ogg
```

The files are deliberately excluded from this repository and its releases. Use assets you have permission to use.

If you own and have subscribed to the Slay the Spire 2 Workshop item `3792512211`, Steam normally stores its local files under:

```text
<SteamLibrary>/steamapps/workshop/content/2868840/3792512211/assets
```

Example local-only copy for `WolfSniperHit`:

```powershell
.\scripts\copy-assets.ps1 `
  -SourceAssetDir "D:\SteamLibrary\steamapps\workshop\content\2868840\3792512211\assets" `
  -GameDir "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish" `
  -Plugin WolfSniperHit
```

Use `-Plugin AlbatrossWolfHit` for the albatross-only mod.
