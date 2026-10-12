# Forest Gammeltroll

A [Valheim](https://www.valheimgame.com/) mod that adds a rare, moss-covered cousin of the Deep North's Gammeltroll to the Black Forest. Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [Jötunn](https://github.com/Valheim-Modding/Jotunn).

- A share of open-world Black Forest Troll spawns (25% by default) become a Forest Gammeltroll. Troll caves and raids are untouched.
- 1500 health and roughly 2–2.75× a Troll's damage, with the Gammeltroll's reach, ground slams and throw attack.
- A Black Forest look: moss instead of snow, a lichen beard, and green firs on its back. The textures are generated at runtime from Valheim's own art, so the mod ships no game assets.
- Turns to stone on death, leaving a rock that can be mined for Stone and Copper Ore with any pickaxe.
- Everything gameplay-related is configurable and server-synced.

See the [mod README](ForestGammeltroll/README.md) for the full feature list, configuration reference and installation instructions. That file is also the Thunderstore page.

## Building from source

### Requirements

- Valheim with [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) installed (directly or through a mod manager profile)
- Visual Studio 2022 or the .NET SDK on Windows (the project targets .NET Framework 4.8)

### Setup

Create an `Environment.props` file in the repository root. It's gitignored, since the paths are machine-specific:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- Your Valheim install -->
    <VALHEIM_INSTALL>C:\Program Files (x86)\Steam\steamapps\common\Valheim</VALHEIM_INSTALL>
    <!-- The BepInEx folder to build against: the game's, or a mod manager profile's -->
    <BEPINEX_PATH>$(VALHEIM_INSTALL)\BepInEx</BEPINEX_PATH>
    <!-- Where Debug builds are deployed -->
    <MOD_DEPLOYPATH>$(BEPINEX_PATH)\plugins</MOD_DEPLOYPATH>
  </PropertyGroup>
</Project>
```

### Build

```bash
dotnet build ForestGammeltroll.sln -c Debug
```

A **Debug** build copies the DLL (with debug symbols) to `$(MOD_DEPLOYPATH)/ForestGammeltroll/`, ready to test in game.

```bash
dotnet build ForestGammeltroll.sln -c Release
```

A **Release** build packages `ForestGammeltroll/bin/Release/net48/ForestGammeltroll.zip` for Thunderstore. The zip contains the DLL plus `manifest.json`, `icon.png` and the mod README from `ForestGammeltroll/Package/`.

### Testing spawns

With `devcommands` enabled in game, `fgt_testspawn [count]` runs regular Troll spawns through the open-world spawn path in front of you. It reports each spawn's biome and whether it became a Forest Gammeltroll. Stand in the Black Forest when you use it.

## Project layout

| Path | Purpose |
|---|---|
| `ForestGammeltroll/ForestGammeltroll.cs` | Plugin entry point and configuration |
| `ForestGammeltroll/ForestTrollCreature.cs` | Clones the Gammeltroll (`TrollFrost`) and applies stats, AI and death behaviour |
| `ForestGammeltroll/PetrifiedRemains.cs` | The petrified statue and the copper-bearing rock it breaks into |
| `ForestGammeltroll/ForestVisuals.cs` | Runtime-generated moss skin, green back firs, copper veins and effect swaps |
| `ForestGammeltroll/SpawnPatches.cs` | Swaps open-world Black Forest Troll spawns |
| `ForestGammeltroll/DamagePatches.cs` | Scales the creature's damage, including spawned shockwaves and projectiles |
| `ForestGammeltroll/StarLevelSystemCompat.cs` | Optional Star Level System compatibility (no star tinting) |
| `ForestGammeltroll/TestSpawnCommand.cs` | The `fgt_testspawn` debug command |
| `ForestGammeltroll/PrefabDumper.cs` | Debug tool that dumps the Gammeltroll and Troll prefabs to a text file (`Debug.DumpPrefabs`) |
| `ForestGammeltroll/Package/` | Thunderstore manifest, icon and README |
| `scripts/` | Post-build deploy and packaging scripts |
| `ForestGammeltrollUnity/` | Unity project from the mod template, for authoring asset bundles (not currently used) |

## License

See [LICENSE](LICENSE).
