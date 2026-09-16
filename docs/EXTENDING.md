# Working with FactoryClassic from another mod

This mod puts the pre-rework Factory back on SPT 4.1 as a **variant** of the maps SPT already ships.
If your mod does anything map-specific on Factory, read the next paragraph at least.

## The one rule, and why it needs an API

**The location id does not change with the variant.** Both tiles answer to `factory4_day` and
`factory4_night`. That is deliberate: it is what keeps quests, stats, insurance and transits working
across them, and changing it would break all four.

The consequence is that **you cannot tell the two maps apart from the location id**, and a mod that
tries will silently do the wrong thing on one of them. DynamicMaps does this today: it filters map
definitions on `GameUtils.GetCurrentMapInternalName()`, which is `factory4_day` on both tiles, so on
the classic map it shows the reworked layout, the wrong extracts and the wrong levels.

That is what this API is for.

## Reading it

Consume it **by reflection**, after a soft dependency. No compile-time reference is needed or wanted:
that way no shared assembly is loaded twice, and you do not inherit our SPT Core version floor.

```csharp
[BepInDependency("com.lennoxp90.factoryclassic", BepInDependency.DependencyFlags.SoftDependency)]
public class YourPlugin : BaseUnityPlugin { }
```

```csharp
static Type Api => AccessTools.TypeByName("FactoryClassic.Client.Api.Factory");

static string CurrentVariant() =>
    Api is null ? null : (string)AccessTools.Property(Api, "CurrentVariant").GetValue(null);
```

If the type is not found the mod is not installed, and every Factory raid is the one SPT ships.

## The surface

Everything is a constant, a property returning a primitive, or an `Action<string>` event. There is no
type of ours you have to bind to.

| member | type | meaning |
|---|---|---|
| `DayLocationId`, `NightLocationId` | `const string` | `factory4_day`, `factory4_night` |
| `Classic`, `Original` | `const string` | `"classic"`, `"original"` - the two variant values |
| `ApiVersion` | `string` | Contract version, currently `1.0` |
| `CurrentVariant` | `string` | The variant for this raid, **or null** |
| `IsClassic` | `bool` | Shorthand for `CurrentVariant == "classic"` |
| `Decided` | `bool` | Whether the variant is known yet |
| `ClassicSceneLoaded` | `bool` | Whether the classic scenes are loaded **right now** |
| `DisplayName(string variant)` | `string` | `"Factory - Classic"` or `"Factory - Vanilla"` |
| `IsFactory(string locationId)` | `bool` | Either Factory id, case-insensitively |
| `VariantResolved` | `event Action<string>` | Fires when the variant becomes known |

## Four things that will bite you

**`CurrentVariant` is null until it is decided, and null does NOT mean the shipped map.** It means
"not yet". The answer arrives when the player answers the prompt, or - on a Fika headless, which
never sees a map screen - when it asks the server as the raid loads. Treating null as `original` is
the single most likely way to show the wrong map.

**`VariantResolved` can fire more than once per raid.** A player who opens the prompt, picks one, goes
back and picks the other gets two events. The **last** value is the one that loads, so handle every
call rather than only the first.

**`CurrentVariant` and `ClassicSceneLoaded` answer different questions.** The first is what was
chosen, known before any scene loads. The second is what the engine actually has, and only becomes
true once the scenes are in. If you are choosing a map image at raid start you want the first; if you
are reading the live scene you want the second.

**The variant values are not the display names.** The wire values are `classic` and `original` -
`original` matching InterchangeRework's, so a mod supporting both maps sees one vocabulary. What a
player is shown is `Factory - Classic` and `Factory - Vanilla`. Use `DisplayName` rather than
inventing wording, and note that **the word "rework" must not appear in anything a player reads**: it
is BSG's folder name for the *current* map, so it names the opposite tile.

## Prompting on the map screen yourself

If your mod also asks the player something before a raid, **do not rewire the screen's Next and Ready
buttons.** `OnClick.RemoveAllListeners()` is a destructive claim on a shared object: whichever mod
runs last silently deletes the other's handler, and the loser gets no error. Patch the screen
controller's `ShowNextScreen` and `ShowReadyScreen` instead, and decline anything that is not your
location. InterchangeRework's `docs/EXTENDING.md` carries the full write-up including the three traps
that make it fiddly.

## Verifying it is there

At load the mod logs its own surface exactly as reflection sees it:

```
[Api] FactoryClassic.Client.Api.Factory v1.0
[Api]   const: DayLocationId=factory4_day, NightLocationId=factory4_night, Classic=classic, Original=original
[Api]   props: ApiVersion:String, CurrentVariant:String, IsClassic:Boolean, ...
```

If your reflection disagrees with those lines, your lookup is wrong rather than the API.

## Versioning

`ApiVersion` is `major.minor`. The minor moves for an addition; the major moves for anything you could
be broken by - a removed member, a renamed constant, or a change in what a value means. Check the
major and decline one you do not know rather than guessing.

## Not built

Server-side variant-keyed content registration - adding containers, loose loot, exits, spawns or
bosses to one variant only - does not exist yet. If you need it, say what for; the shape depends
entirely on the use case and guessing at it would produce the wrong thing.
