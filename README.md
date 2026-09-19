# SPT FactoryClassic

The pre-rework Factory, playable on SPT 4.1 as a variant of `factory4_day` and `factory4_night`.
You pick which one you want at the map screen; vanilla Factory is untouched and one click away.

BSG never deleted the old Factory. Its seven scenes are still compiled into the SPT 4.1 client and
kept current with 4.1-era systems; they are simply not referenced by the shipped `ScenesPreset`.
This mod points the preset at them instead, and serves the matching loot, spawn and exit data.

Because it ships no assets at all, the whole mod is two DLLs and some JSON.

> **1.0.0 moves the map-screen choice into MapVariants.** Factory itself plays exactly as it did;
> the prompt, the transit choice and the loading label are drawn by that mod now, and the two F12
> settings that used to live here moved with them. Please report anything odd rather than assuming
> it is meant to be that way - see [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

## Requires

- SPT 4.1
- **MapVariants** (`com.lennoxp90.mapvariants`), required, not optional. It owns the map-screen
  choice, the transit prompt and the loading label for every map that has a second version, this one
  included. Without it neither half of this mod loads and Factory is the map SPT ships.
- Fika is optional and supported, headless hosting included

## Install

Extract the archive over your SPT install folder, so that `BepInEx` and `SPT_Runtime` land next to
the ones already there. Restart the server.

## Configuration

Server side, in `user/mods/LennoxP90-FactoryClassic/config/config.json`. All three are read once at
load, so a change needs a server restart. None of them touch the Factory SPT ships.

| key | values | what it does |
|---|---|---|
| `variant` | ignored | **Deprecated and does nothing.** Which tile you get when nobody picks one is MapVariants' question now. The key is still read so an existing `config.json` loads, and a value other than `classic` is reported in the server log rather than silently dropped. |
| `lootMode` | `classic`, `hybrid`, `modern` | `classic` serves the 3.9.8 loot tables untouched. `hybrid` adds the 464 day / 476 night item templates 4.1 spawns on Factory that the old tables never had, each at the classic spawn point nearest where 4.1 puts it. `modern` rebuilds every point from 4.1's pool instead of adding to it. |
| `questGate` | `warn`, `hide`, `off` | What to do about the six quests the classic tile cannot satisfy. See [`docs/QUESTS.md`](docs/QUESTS.md). |

Client side, in BepInEx's config for `com.lennoxp90.factoryclassic`: the two audio switches, the
loot-cluster repair and the camera diagnostic, all described in
[`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

**The two choice settings moved to MapVariants.** `PromptSelection` is now
`com.lennoxp90.mapvariants` -> `General` -> `PromptSelection`, and `DefaultSelection` is under that
mod's `Factory` section, offering `Classic` and `Vanilla`. Copies left behind in an older
FactoryClassic config are ignored; delete them or leave them, it makes no difference.

## Known limitations

- **Six quests cannot be finished on the classic tile.** Their objectives fire off trigger zones
  baked into the scene, and 18 of the 25 Factory quest zones were authored after these scenes were.
  Nothing breaks in the other direction, the mod never writes to a profile, and an accepted quest
  still completes on Factory - Vanilla. [`docs/QUESTS.md`](docs/QUESTS.md) has the list.
- **The art is 2018 art.** It has not been upscaled and cannot be automatically: BSG re-authored
  the textures rather than upscaling them, and none of the 292 share a name with a classic one.
- **Six audio portals connect no rooms** and cannot be recovered, so those particular openings are
  acoustically absent. No route in the routing table passes through any of them.
- `hybrid` and `modern` loot modes are implemented and tested offline, but have not been played.

## For mod authors

This mod publishes which Factory a raid is loading, so another mod can stop showing the wrong
layout. See [`docs/EXTENDING.md`](docs/EXTENDING.md). To offer variants of a map of your own, register
with MapVariants rather than building a second prompt.

## Licence

MIT. See [`LICENSE`](LICENSE).
