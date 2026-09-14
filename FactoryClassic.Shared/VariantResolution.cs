#nullable enable

namespace FactoryClassic.Shared
{
    // Which variant a caller should load: its own recorded choice, else the last choice made for that
    // location, else the server's configured default.
    //
    // There is deliberately no "is this a headless" flag. A Fika headless hosts under its OWN session
    // rather than the starting player's, so it never has a choice of its own and has to inherit - but
    // so does any other caller with nothing recorded, and with the prompt turned off our client still
    // posts the configured value, so a normal player always has one. The same order is therefore right
    // for everybody, and the server never has to work out what it is talking to.
    public static class VariantResolution
    {
        public const string NoAnswer = "";

        public static string Resolve(string? ownChoice, string? latestForLocation, string? configuredDefault)
        {
            if (!string.IsNullOrEmpty(ownChoice)) return MapVariant.Normalise(ownChoice);
            if (!string.IsNullOrEmpty(latestForLocation)) return MapVariant.Normalise(latestForLocation);
            return MapVariant.Normalise(configuredDefault);
        }
    }
}
