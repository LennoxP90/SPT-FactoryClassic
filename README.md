# SPT FactoryClassic

The pre-rework Factory, playable on SPT 4.1 as a variant of `factory4_day` and `factory4_night`.
You pick which one you want at the map screen; vanilla Factory is untouched and one click away.

BSG never deleted the old Factory. Its seven scenes are still compiled into the SPT 4.1 client and
kept current with 4.1-era systems; they are simply not referenced by the shipped `ScenesPreset`.
This mod points the preset at them instead, and serves the matching loot, spawn and exit data.

The classic map itself ships no assets. The one asset in the archive is the Halloween bundle below.

> **Halloween 2026 is its own tile.** Pick it on the map screen to load the version of
> classic Factory from EFT's 1.3.0 Halloween event: gore, lootable corpses, event lights, the event's
> grey grade and its own ambience. Please report anything odd rather than assuming it is meant to be
> that way - see [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

## Halloween Factory

- **Picked, not rolled.** The map screen offers Factory - Vanilla, Factory - Classic and Factory -
  Halloween 2026, day or night, and every player in that raid loads what the host picked. It is not tied
  to SPT's seasonal events. Needs MapVariants 1.1 or later: with an older one FactoryClassic does not load, and BepInEx and the server log say why.
- **What changes:** the event's props, gore and corpses on top of the classic map, its lights, a grey
  colour grade that keeps reds, and its ambience (two scary drones, distant zombies, flying ghosts, rats).
  The 65 corpses are lootable, from 4.1 Factory's own PMC corpse pool.
- **What does not:** spawns, exits, bots and the rest of the loot are classic Factory's.
- **The bundle borrows from your install.** Textures, meshes and sounds the event shares with the game are
  read from your own SPT 4.1 client rather than shipped, which keeps the bundle to about 81 MB.

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

Server side, in `user/mods/LennoxP90-FactoryClassic/config/config.json`, or from the FactoryClassic page
in the server's web interface. They are read once at load, so a change needs a server restart. None of
them touch the Factory SPT ships.

| key | values | what it does |
|---|---|---|
| `variant` | ignored | **Deprecated and does nothing.** Which tile you get when nobody picks one is MapVariants' question now. The key is still read so an existing `config.json` loads, and a value other than `classic` is reported in the server log rather than silently dropped. |
| `lootMode` | `classic`, `hybrid`, `modern` | `classic` serves the 3.9.8 loot tables untouched. `hybrid` adds the 464 day / 476 night item templates 4.1 spawns on Factory that the old tables never had, each at the classic spawn point nearest where 4.1 puts it. `modern` rebuilds every point from 4.1's pool instead of adding to it. |
| `questGate` | `warn`, `hide`, `off` | What to do about the six quests the classic tile cannot satisfy. See [`docs/QUESTS.md`](docs/QUESTS.md). |

Client side, in BepInEx's config for `com.lennoxp90.factoryclassic`: the two audio switches, the
loot-cluster repair and the camera diagnostic, all described in
[`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md), and **Halloween -> `AmbienceGainDb`** (0 to 20 dB, default
14). The event's ambience is mastered very quietly; this lifts the game's ambience channels by that much
during a Halloween raid only, and can be moved live in a raid from the F12 menu.

**The two choice settings moved to MapVariants.** `PromptSelection` is now
`com.lennoxp90.mapvariants` -> `General` -> `PromptSelection`, and `DefaultSelection` is under that
mod's `Factory` section, offering `Classic`, `Vanilla` and `Halloween 2026`. Copies left behind in an older
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
- **Halloween Factory's ambience has no indoor/outdoor blend.** The event's own blenders do not survive
  the conversion, so the mix is static; the game's per-room effects still apply. Two of its sounds, the
  burning-meat loop and the transformer sparks, exist only in the event build and are silent.
- **The Halloween bundle is built against the SPT 4.1 client (EFT 0.16.9.5.40743)** and reads assets out of
  it. A different client build needs a rebuilt bundle.
- **Halloween Factory in Fika:** every peer reads the host's pick from the server, but a full two-client
  raid and a headless-hosted raid have not been played yet.

## For mod authors

This mod publishes which Factory a raid is loading, Halloween 2026 included, so another mod can stop
showing the wrong layout or add content to the event map. See [`docs/EXTENDING.md`](docs/EXTENDING.md). To offer variants of a map of your own, register
with MapVariants rather than building a second prompt.

## Licence

MIT. See [`LICENSE`](LICENSE).
