#nullable enable
using System;

namespace FactoryClassic.Shared
{
    // Which variant a transit arrives on, and who decides.
    //
    // A transit has no map screen, so nobody is prompted and the old ladder fell through to a
    // per-session choice that is never cleared - which on Fika let a joiner load different scenes
    // from the ones the server was serving. The rule instead is: the first player to enter the
    // transit zone claims the variant for that location, and everyone who joins reads the claim.
    //
    // First claim wins, and later claimants are told what they joined rather than being refused, so
    // the caller can say so. The claim is released when a raid for that location starts or ends.
    public static class TransitClaimPolicy
    {
        // Every transit into Factory targets factory4_day - Customs, Woods and Labyrinth all name it,
        // and none names factory4_night. A night transit does not exist to be handled.
        public const string FactoryDestination = "factory4_day";

        public static bool IsFactoryBound(string? destination)
            => string.Equals((destination ?? "").Trim(), FactoryDestination, StringComparison.OrdinalIgnoreCase);

        // The winner of a claim: whatever is already held, else the one being offered.
        public static string Winner(string? held, string? offered)
            => string.IsNullOrEmpty(held) ? MapVariant.Normalise(offered) : MapVariant.Normalise(held);

        public static bool WasFirst(string? held) => string.IsNullOrEmpty(held);
    }
}
