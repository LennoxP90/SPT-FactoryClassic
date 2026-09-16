# Diagnostics

What to send with a bug report, and what the mod logs when it is working.

## Where the logs are

| log | where |
|---|---|
| Client, with this mod's own lines | `BepInEx\LogOutput.log` in your SPT install |
| Client, the game's own | `EscapeFromTarkov_Data\output_log.txt`, or `%USERPROFILE%\AppData\LocalLow\Battlestate Games\EscapeFromTarkov\Player.log` |
| Server | `SPT_Runtime\user\logs\spt\sptYYYYMMDD.log` |

⚠️ The launcher **deletes its own log on every start**, so copy it before relaunching. The client's
`LogOutput_prev.log` rotates at process start rather than at raid end, so straight after a raid it
still holds the run before the one you want.

## A healthy raid

Every line below comes from one classic raid. If yours is missing one, that is the useful part of
the report.

```
[SpatialAudio] armed
[SpatialAudio] OcclusionSettings was unassigned on the classic sound scene; supplied one
[SpatialAudio] the classic tile's location info carried maxRoutesCount=0 maxRoutePortalsCount=0; raised to 311/1129 from factory_classic.audiobakedata
[SpatialAudio] classic tile: 'AudioBakeData\factory_sound.audiobakedata' -> our converted routing table
[SpatialAudio] 46 room(s) loaded, 41 with a usable volume
[SpatialAudio] 90 portal(s) have a declared room pair in the scene's roomConnections
```

Six portals then report `connects no rooms and sits inside one` - ids 17, 18, 45, 61, 76 and 90.
That is expected, and no route in the table passes through any of them.

What should **not** appear:

- `Missing N RoomPair entries while reading bake data` - the game's own check on the routing table
- any `NullReferenceException` naming `InteractiveObjectOccluder` or `SpatialAudioSystem`
- `Spatial Audio Location info is missing, can't full initialized`

## Settings that produce more

In BepInEx's config for `com.lennoxp90.factoryclassic`, under `[Diagnostics]`:

| setting | default | what it adds |
|---|---|---|
| `ExceptionLog` | on | Mirrors Unity errors and exceptions into the log, the first in full and the rest as counts. BepInEx ships `WriteUnityLog=false`, so without this the exception that cancels a raid never reaches the log at all. Leave it on. |
| `BotReport` | off | Each live bot with its role, zone, navmesh status and health, four times per raid. Turn on for a report about bots standing still, not spawning, or spawning inside geometry. |
| `FrameSplitProbe` | off | The scripts / camera / present split of frame time every 10 s. Turn on for a report about stutter, so a CPU problem is not confused with a GPU one. |

Under `[General]`, two affect audio:

| setting | default | what it does |
|---|---|---|
| `SpatialRouting` | on | Use the classic tile's real routing table. Turning it **off** is the first thing to try if a raid crashes shortly after the map appears: it falls back to an empty table, which costs all room-to-room sound but is the configuration every earlier build shipped. |
| `WirePortals` | on | Repair audio portals that connect no rooms. Independent of the above. |

## A good report

1. What you expected and what happened, and whether it is repeatable.
2. `BepInEx\LogOutput.log` from the raid, copied before relaunching.
3. Which tile - Classic or Vanilla - and day or night.
4. Fika or single player, and if Fika, whether you hosted or a headless did.
5. `config.json` from `user/mods/LennoxP90-FactoryClassic/config/`, which gives the loot mode and
   quest gate.

For a crash with nothing in the log, say so explicitly: a native crash leaves no managed exception,
and that is itself the diagnosis.
