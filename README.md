# How to Fish — Wolf Meme Mods

[简体中文](README.zh-CN.md)

Two client-side BepInEx mods for **How to Fish** that recreate the wolf-image + `Animals` speed-up meme.

| Mod | Trigger | Version |
| --- | --- | --- |
| `WolfSniperHit` | The local player lands a real ranged hit while holding a sniper rifle | 0.1.1 |
| `AlbatrossWolfHit` | An albatross NPC projectile hits the local player | 0.1.0 |

Both effects restart the audio on every trigger, increase pitch by `1.1x` per consecutive trigger, cap it at `2.0x`, and display a short full-screen shake/fade. They are independent plugins and can be enabled separately.

## Requirements

- How to Fish `1.0.12` (the version used for development)
- [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
- Your own legally obtained image and audio assets

## Installation

1. Install BepInEx in the directory containing `How to Fish.exe`.
2. Download the desired ZIP from [Releases](https://github.com/vinahirilya-hue/HowToFish-Wolf-Meme-Mods/releases).
3. Extract it into the game directory.
4. Put these files in the plugin's `assets` directory:
   - `howl_flash.png`
   - `howl_bgm.ogg`
5. Start the game once to generate the configuration file under `BepInEx/config`.

See [Asset setup](docs/ASSETS.md) for the exact paths and local-copy example.

## Configuration

- `BepInEx/config/local.wjy13.wolfsniperhit.cfg`
- `BepInEx/config/local.wjy13.albatrosswolfhit.cfg`

You can change volume, fade timing, shake strength, speed multiplier, maximum speed, and reset delay. `WolfSniperHit` also supports `OnlyOnKill=true`.

## Building

The game assemblies and BepInEx binaries are not redistributed. Build against your local installation:

```powershell
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish"
```

Or set `HTF_GAME_DIR` and run `dotnet build`:

```powershell
$env:HTF_GAME_DIR = "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish"
dotnet build .\HowToFish.WolfMemeMods.sln
```

## Copyright notice

This repository and its releases do **not** include `Animals`, the wolf image, game assemblies, or files from other Workshop mods. See [Third-party notices](THIRD_PARTY_NOTICES.md).

## License

Source code is released under the [MIT License](LICENSE).
