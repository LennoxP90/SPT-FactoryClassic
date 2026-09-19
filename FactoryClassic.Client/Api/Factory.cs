using System;
using System.Collections.Generic;
using FactoryClassic.Shared;

namespace FactoryClassic.Client.Api
{
    // MapVariants' session state is GLOBAL rather than per location, so everything here filters on
    // CurrentLocationId: unfiltered, a Lighthouse raid would be reported as Factory's answer.
    internal static class VariantSource
    {
        internal static string Current
        {
            get
            {
                var locationId = MapVariantsApi.CurrentLocationId;
                if (locationId == null || !Factory.IsFactory(locationId)) return null;
                var theirs = MapVariantsApi.CurrentVariant;
                return theirs == null ? null : VariantVocabulary.FromMapVariants(theirs);
            }
        }

        // Handlers are wrapped so a non-Factory raid is swallowed rather than forwarded, which keeps
        // the event's contract at what a 0.2.0 consumer already relies on.
        static readonly Dictionary<Action<string>, Action<string>> Filters = new Dictionary<Action<string>, Action<string>>();

        internal static event Action<string> Resolved
        {
            add
            {
                if (value == null || Filters.ContainsKey(value)) return;
                Action<string> filtered = theirs =>
                {
                    var locationId = MapVariantsApi.CurrentLocationId;
                    if (locationId == null || !Factory.IsFactory(locationId)) return;
                    value(VariantVocabulary.FromMapVariants(theirs));
                };
                Filters[value] = filtered;
                MapVariantsApi.Subscribe(filtered);
            }
            remove
            {
                if (value == null || !Filters.TryGetValue(value, out var filtered)) return;
                Filters.Remove(value);
                MapVariantsApi.Unsubscribe(filtered);
            }
        }
    }

    /// <summary>
    /// The public surface other mods read, documented in docs/EXTENDING.md. Consumed by reflection,
    /// so every member stays a const, a primitive or an Action&lt;string&gt;: no consumer needs a
    /// compile-time reference, and none inherits our SPT Core version floor.
    /// </summary>
    public static class Factory
    {
        public const string DayLocationId = ExtensionApiContract.DayLocationId;
        public const string NightLocationId = ExtensionApiContract.NightLocationId;

        public const string Classic = MapVariant.Classic;
        public const string Original = MapVariant.Original;

        public static string ApiVersion => ExtensionApiContract.Version;

        /// <summary>
        /// NULL MEANS "NOT DECIDED YET", NEVER "ORIGINAL". Treating null as the shipped map shows
        /// the wrong one; wait for VariantResolved instead.
        /// </summary>
        public static string CurrentVariant => VariantSource.Current;

        public static bool IsClassic => MapVariant.IsClassic(CurrentVariant);

        public static bool Decided => CurrentVariant != null;

        /// <summary>
        /// From the engine, not from the choice: what is on screen now, not what was picked.
        /// </summary>
        public static bool ClassicSceneLoaded => PresetSwap.ClassicLoaded();

        public static string DisplayName(string variant) => VariantDisplay.For(variant);

        public static bool IsFactory(string locationId)
            => string.Equals(locationId, DayLocationId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(locationId, NightLocationId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Fires with the variant as soon as it is known, and AGAIN if the player goes back and
        /// picks the other map. The latest value is the one that loads, so handle every call.
        /// </summary>
        public static event Action<string> VariantResolved
        {
            add => VariantSource.Resolved += value;
            remove => VariantSource.Resolved -= value;
        }
    }
}
