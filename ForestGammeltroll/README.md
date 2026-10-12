# Forest Gammeltroll

Something old has taken root in the Black Forest.

**Forest Gammeltroll** adds a rare, moss-covered cousin of the Deep North's Gammeltroll to the Black Forest. Instead of snow and frost, it carries the forest on its back: mossy stone skin, a hanging beard of lichen, and green firs growing from its shoulders. When it falls, it turns to stone, and the petrified remains are laced with copper.

## Features

- **A new Black Forest mini-boss.** Whenever a regular Troll spawns in the open Black Forest, there's a chance (25% by default) it's a Forest Gammeltroll instead.
  - Only open-world spawns are affected. Troll caves and raids are left untouched.
  - In multiplayer, the roll happens once on the machine that owns the area, so you'll never get duplicates.
- **Tougher than a Troll, gentler than its northern kin.** 1500 health and roughly 2–2.75× a Troll's damage. It keeps the Gammeltroll's huge reach, ground slams and throw attack, so keep your distance and your stamina.
- **A Black Forest look.** Snow becomes moss, the frosty beard becomes lichen, and the snowy firs on its back become green. Every texture is generated in-game from Valheim's own art, so the download stays tiny.
- **Turns to stone on death.** Like the Gammeltroll, it petrifies into a statue. Break it, then mine the rock for **Stone and Copper Ore**. Any pickaxe works, from the Antler Pickaxe up.
- **Stars without the paint job.** Starred Forest Gammeltrolls keep their natural colours, including with Star Level System installed.
- **Fully configurable.** Spawn chance, health, damage, size, AI ranges and copper yield can all be changed live with ConfigurationManager, with no restart needed.

## Configuration

Settings live in `BepInEx/config/com.voxxsidus.forestgammeltroll.cfg` and can be edited in-game with [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/). Gameplay settings are server-synced: on a dedicated server, the server's values apply to everyone.

| Section | Setting | Default | Description |
|---|---|---|---|
| Spawning | SpawnChance | 25 | Percent chance an open-world Black Forest Troll becomes a Forest Gammeltroll |
| Spawning | AllowStars | true | Whether it can spawn with stars, using the Troll's normal level-up chances |
| Stats | Health | 1500 | Base health (Troll: 600, Gammeltroll: 3000) |
| Stats | DamageMultiplier | 0.75 | Multiplier on the Gammeltroll's damage, applied to every hit |
| Stats | ModelScale | 1.0 | Size relative to the Gammeltroll (1.0 ≈ 4× a Troll) |
| AI | ViewRange | 50 | How far it can see you (Troll: 30, Gammeltroll: 300) |
| AI | AlertRange | 30 | Radius in which it notices you |
| AI | MaxChaseDistance | 200 | How far from home it will chase you |
| Petrify | CopperOreWeight | 0.5 | Copper Ore vs Stone ratio when mining the remains (0.5 ≈ one third copper) |
| Visual | MossTint | green | Colour of the moss (client-side) |
| Visual | SnowToMossThreshold | 0.48 | How much of the old snow becomes moss; lower means more moss (client-side) |

### How often will I see one?

At the default 25%, each new patch of Black Forest you explore has about a 1 in 84 chance of producing one. In practice that's roughly **one per hour of exploring new forest**, and much rarer while you stay in areas you've already explored. Raise or lower `SpawnChance` to taste.

## Compatibility

- **Everyone must have the mod.** Clients and server need it installed, with matching versions.
- **Star Level System:** supported. Starred Forest Gammeltrolls keep their normal colours automatically.

## Installation

Install with a mod manager (r2modman, Gale, Thunderstore Mod Manager), or manually:

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Copy `ForestGammeltroll.dll` into `BepInEx/plugins/ForestGammeltroll/`.

## For server admins

With `devcommands` enabled, `fgt_testspawn [count]` pushes regular Troll spawns through the open-world spawn path in front of you, so you can check spawn settings without waiting. Stand in the Black Forest when you use it.

## Changelog

### 1.0.0
- Initial release.
