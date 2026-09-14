// Nullable is declared per file: Shared is source-linked into projects that disagree on the setting.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactoryClassic.Shared
{
    // Which of the three presets a Factory raid loads we are looking at. They cannot be told apart by
    // ServerName: the base preset carries an empty one, and the night culling child carries
    // "factory4_day", a BSG slip also visible in its name, "factory_fework_night_culling". Only the
    // scene paths are reliable.
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
        // The trailing slash matters. Without it "Assets/Content/Locations/Factory_Rework/..." starts
        // with the classic prefix and every current path reads as classic.
        public const string ClassicPrefix = "Assets/Content/Locations/Factory/";
        public const string CurrentPrefix = "Assets/Content/Locations/Factory_Rework/";

        // The scene that only the classic tile loads, which is how anything downstream recognises the
        // variant without depending on the swap's own state.
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

        // Any loaded scene belonging to either Factory, by scene name rather than asset path. Both
        // variants' scenes begin with "Factory"; the other maps that mention the word do not
        // (custom_factoryStorageZone, Custom_ChemicalFactory, Custom_Construction_Factory).
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

            // Culling is checked before Day and Night: the culling children are named
            // Factory_Rework_Day_Culling and Factory_Rework_Night_Culling, so a Day test that only
            // looked for "Day" would claim them.
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
