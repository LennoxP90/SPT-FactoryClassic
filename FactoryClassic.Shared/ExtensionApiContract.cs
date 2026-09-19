#nullable enable

namespace FactoryClassic.Shared
{
    /// <summary>
    /// The version of the public surface other mods read. Minor for an addition, major for anything
    /// a consumer could break on: a removal, a rename, or a change in what a value means.
    /// </summary>
    public static class ExtensionApiContract
    {
        public const string Version = "1.0";

        public const string DayLocationId = "factory4_day";
        public const string NightLocationId = "factory4_night";
    }
}
