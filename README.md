# SPT FactoryClassic

The pre-rework Factory, playable on SPT 4.1 as a variant of `factory4_day` and `factory4_night`.
You pick which one you want at the map screen; vanilla Factory is untouched and one click away.

BSG never deleted the old Factory. Its seven scenes are still compiled into the SPT 4.1 client and
kept current with 4.1-era systems; they are simply not referenced by the shipped `ScenesPreset`.
This mod points the preset at them instead, and serves the matching loot, spawn and exit data.

Because it ships no assets at all, the whole mod is two DLLs and some JSON.

> **0.1.0 is a beta.** Everything here is played and working, but by few people on one server.
> Please report anything odd rather than assuming it is meant to be that way - see
> [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

## Requires

- SPT 4.1
- Fika is optional and supported, headless hosting included

## Install

Extract the archive over your SPT install folder, so that `BepInEx` and `SPT_Runtime` land next to
the ones already there. Restart the server.

## Configuration

Server side, in `user/mods/LennoxP90-FactoryClassic/config/config.json`. All three are read once at
load, so a change needs a server restart. None of them touch the Factory SPT ships.

| key | values | what it does |
|---|---|---|
| `variant` | `classic`, `original` | Which tile a raid gets when nobody picked one: a player with the prompt turned off, or a Fika headless that never saw the screen. |
| `lootMode` | `classic`, `hybrid`, `modern` | `classic` serves the 3.9.8 loot tables untouched. `hybrid` adds the 464 day / 476 night item templates 4.1 spawns on Factory that the old tables never had, each at the classic spawn point nearest where 4.1 puts it. `modern` rebuilds every point from 4.1's pool instead of adding to it. |
| `questGate` | `warn`, `hide`, `off` | What to do about the six quests the classic tile cannot satisfy. See [`docs/QUESTS.md`](docs/QUESTS.md). |

Client side, in BepInEx's config for `com.lennoxp90.factoryclassic`: whether to show the map prompt
at all, which tile to pick when it is off, and the two audio switches described in
[`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

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

Another mod can offer its own map variants through the same prompt rather than building a second
one. See [`docs/EXTENDING.md`](docs/EXTENDING.md).

## Licence

MIT. See [`LICENSE`](LICENSE).
