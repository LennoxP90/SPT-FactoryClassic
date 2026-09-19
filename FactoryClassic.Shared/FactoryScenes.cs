#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// Which of the three presets a Factory raid loads. They cannot be told apart by ServerName:
    /// the base preset carries an empty one and the night culling child carries "factory4_day".
    /// Only the scene paths are reliable.
    /// </summary>
    public enum PresetKind
    {
        NotOurs,
        Day,
        Night,
        Base,
        Culling,
    }

    public static class FactoryScenes
    {
        // The trailing slashes are load-bearing: without them every Factory_Rework path also
        // matches ClassicPrefix.
        public const string ClassicPrefix = "Assets/Content/Locations/Factory/";
        public const string CurrentPrefix = "Assets/Content/Locations/Factory_Rework/";

        public const string ClassicSoundScene = "Factory_Sound";

        public static readonly IReadOnlyList<string> ServerNames = new[] { "factory4_day", "factory4_night" };

        static readonly string[] ClassicBase =
        {
            ClassicPrefix + "Factory.unity",
            ClassicPrefix + "Factory_Scripts.unity",
            ClassicPrefix + "Factory_AI.unity",
            ClassicPrefix + "Factory_DesignStuff.unity",
            ClassicPrefix + "Factory_new.unity",
            ClassicPrefix + "Factory_Sound.unity",
        };

        static readonly string[] ClassicDay = { ClassicPrefix + "Factory_Day.unity" };
        static readonly string[] ClassicNight = { ClassicPrefix + "Factory_Night.unity" };
        static readonly string[] None = new string[0];

        /// <summary>
        /// Any loaded scene belonging to either Factory, by scene name. StartsWith rather than
        /// Contains: custom_factoryStorageZone, Custom_ChemicalFactory and
        /// Custom_Construction_Factory all mention the word without being this map.
        /// </summary>
        public static bool IsFactoryScene(string? sceneName)
            => sceneName != null && sceneName.StartsWith("Factory", StringComparison.Ordinal);

        public static bool IsClassicPath(string? path)
            => path != null && path.StartsWith(ClassicPrefix, StringComparison.Ordinal);

        public static bool IsCurrentPath(string? path)
            => path != null && path.StartsWith(CurrentPrefix, StringComparison.Ordinal);

        public static PresetKind Classify(IReadOnlyList<string?> paths)
        {
            if (paths == null || paths.Count == 0) return PresetKind.NotOurs;
            var ours = paths.Where(IsCurrentPath).ToList();
            if (ours.Count == 0) return PresetKind.NotOurs;

            // Culling before Day and Night: the children are Factory_Rework_Day_Culling and
            // Factory_Rework_Night_Culling, so a Day test would claim them.
            if (ours.Any(p => p!.Contains("_Culling."))) return PresetKind.Culling;
            if (ours.Any(p => p!.Contains("_Day_"))) return PresetKind.Day;
            if (ours.Any(p => p!.Contains("_Night_"))) return PresetKind.Night;
            return PresetKind.Base;
        }

        public static IReadOnlyList<string> ClassicPathsFor(PresetKind kind) => kind switch
        {
            PresetKind.Day => ClassicDay,
            PresetKind.Night => ClassicNight,
            PresetKind.Base => ClassicBase,
            PresetKind.Culling => None,
            _ => None,
        };
    }
}
