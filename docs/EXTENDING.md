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
| `Halloween2026` | `const string` | `"halloween2026"` - MapVariants' id for Halloween 2026 (1.1) |
| `ApiVersion` | `string` | Contract version, currently `1.1` |
| `CurrentVariant` | `string` | The variant for this raid, **or null** |
| `IsClassic` | `bool` | Shorthand for `CurrentVariant == "classic"` |
| `IsHalloween` | `bool` | Whether this Factory raid is Halloween 2026 (1.1) |
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

**`VariantResolved` is raised by MapVariants now, and filtered back to Factory here.** Since 0.3.0
the choice lives in MapVariants, which raises its own event for every map it manages. This mod
subscribes to that and re-raises only the Factory raids, translated into our vocabulary, so the
contract you already code against is unchanged: it still fires for Factory and nothing else.

**`CurrentVariant` returns null on a non-Factory raid, even while another map's variant is decided.**
MapVariants' session state is global; ours is not. That is the same "null means not decided **for
Factory**" contract this page has always described, made explicit now that another map can be decided
at the same time.

**The variant values are not the display names.** The wire values are `classic` and `original` -
`original` matching InterchangeRework's, so a mod supporting both maps sees one vocabulary. What a
player is shown is `Factory - Classic` and `Factory - Vanilla`. Use `DisplayName` rather than
inventing wording, and note that **the word "rework" must not appear in anything a player reads**: it
is BSG's folder name for the *current* map, so it names the opposite tile.

## Halloween 2026

Halloween 2026 is a third tile on the map screen, picked by the player like the other two. It is the
classic map with the 1.3.0 event layer on top, so on a Halloween raid **`CurrentVariant` is `classic`**
and `IsClassic` is true; `IsHalloween` is what tells it apart. It is never rolled and never tied to
SPT's seasonal events.

**Client.** Read `IsHalloween` once the variant is decided, or in a `VariantResolved` handler. To act
when the event scenes are in, wait for scene load and check for `FactoryClassic_Halloween`: a quest
zone, a spawn marker or anything else you add to the map is a GameObject you create in that scene
after it loads. Every peer of a Fika raid and the headless resolve the same variant, because they all
read it from the server.

**Server.** MapVariants answers which variant a location is serving. Resolve
`MapVariants.Server.Variants.Providers` from the DI container by type name, as this mod does in
`FactoryClassic.Server/MapVariantsBridge.cs`, and call `InstalledVariant("factory4_day")` (or night):
it returns `"halloween2026"` while a Halloween raid is being built and served. Read it when you need
it, not at load: it changes with each player's pick.

The id is stable. A later event would get its own id rather than reusing this one.

## Prompting on the map screen yourself

**This mod no longer patches the map screen at all.** Since 0.3.0 the prompt, the transit prompt and
the loading label all belong to MapVariants, which FactoryClassic registers with as a provider. If
your mod also asks the player something before a raid, talk to MapVariants rather than to this one.

The old advice still holds for anyone who does patch that screen: **do not rewire the screen's Next
and Ready buttons.** `OnClick.RemoveAllListeners()` is a destructive claim on a shared object:
whichever mod runs last silently deletes the other's handler, and the loser gets no error. Patch the
screen controller's `ShowNextScreen` and `ShowReadyScreen` instead, and decline anything that is not
your location. InterchangeRework's `docs/EXTENDING.md` carries the full write-up including the three
traps that make it fiddly.

## Verifying it is there

At load the mod logs its own surface exactly as reflection sees it:

```
[Api] FactoryClassic.Client.Api.Factory v1.1
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
