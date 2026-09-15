using System;
using FactoryClassic.Shared;

namespace FactoryClassic.Client.Api
{
    // The public surface other mods read.
    //
    // Consumed by reflection after a soft [BepInDependency("com.lennoxp90.factoryclassic", ...)], so
    // nothing needs a compile-time reference to this assembly: no shared library is loaded twice, and
    // no consumer inherits our SPT Core version floor. That is why everything here is a const, a
    // property returning a primitive, or an event of Action<string> - there is no type to bind to.
    //
    // Why this exists at all: the location id is "factory4_day" or "factory4_night" on BOTH tiles,
    // deliberately, because that invariant is what keeps quests, stats, insurance and transits
    // working across them. So a consumer keying off the location alone cannot tell which map is
    // loaded, and will happily show the wrong layout. DynamicMaps does exactly that today.
    public static class Factory
    {
        public const string DayLocationId = ExtensionApiContract.DayLocationId;      // "factory4_day"
        public const string NightLocationId = ExtensionApiContract.NightLocationId;  // "factory4_night"

        // The two variant values. "classic" is the pre-rework tile this mod adds; "original" is the
        // Factory SPT ships. The player-facing names are different again - see DisplayName.
        public const string Classic = MapVariant.Classic;    // "classic"
        public const string Original = MapVariant.Original;  // "original"

        public static string ApiVersion => ExtensionApiContract.Version;

        // NULL MEANS "NOT DECIDED YET", NEVER "ORIGINAL". A consumer reading before the player has
        // answered, or before a headless has asked the server, and treating null as the shipped map
        // will show the wrong one. Wait for VariantResolved instead.
        public static string CurrentVariant => PresetSwap.SessionChoice;

        public static bool IsClassic => MapVariant.IsClassic(CurrentVariant);

        // True once the variant is known for this raid. It goes false again when the map unloads.
        public static bool Decided => CurrentVariant != null;

        // True while the classic scenes are actually loaded, answered from the engine rather than
        // from our own state. Use this when what matters is what is on screen right now; use
        // CurrentVariant when what matters is what was chosen.
        public static bool ClassicSceneLoaded => PresetSwap.ClassicLoaded();

        // What a player is shown for a variant: "Factory - Classic" or "Factory - Vanilla". Use this
        // rather than inventing your own wording, so one map has one name everywhere.
        public static string DisplayName(string variant) => VariantDisplay.For(variant);

        public static bool IsFactory(string locationId)
            => string.Equals(locationId, DayLocationId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(locationId, NightLocationId, StringComparison.OrdinalIgnoreCase);

        // Fires with the variant as soon as it is known, and AGAIN if the player goes back and picks
        // the other map before the raid starts. The latest value is the one that loads, so handle
        // every call rather than only the first.
        //
        // Handlers are invoked one at a time, each guarded: one throwing is logged and the rest still
        // run. Do not rely on that - it exists so a faulty consumer cannot take the raid down.
        public static event Action<string> VariantResolved
        {
            add => PresetSwap.Resolved += value;
            remove => PresetSwap.Resolved -= value;
        }
    }
}
