#nullable enable

namespace FactoryClassic.Shared
{
    // The version of the public surface other mods read. Bump the minor for an addition, the major
    // for anything a consumer could be broken by: a removed member, a renamed constant, or a change
    // in what a value MEANS. A consumer that checks this and finds a major it does not know should
    // decline rather than guess.
    public static class ExtensionApiContract
    {
        public const string Version = "1.0";

        // The two location ids. Both variants answer to these - the id never changes with the
        // variant, which is what keeps quests, stats, insurance and transits working across them.
        // A consumer keying off the location id alone therefore cannot tell the tiles apart, which
        // is precisely why the variant query exists.
        public const string DayLocationId = "factory4_day";
        public const string NightLocationId = "factory4_night";
    }
}
