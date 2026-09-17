using System.Collections.Concurrent;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;

namespace FactoryClassic.Server.Variants;

// The variant the first player into a transit zone claimed for a location, held until a raid there
// starts or ends. Keyed by location and not by session, because the whole point is that everyone who
// joins that transit gets the same answer.
//
// The owner is kept so that player, and only that player, can still change their mind while the
// countdown runs. Anyone else who claims is told what they joined.
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class TransitClaimStore
{
    private readonly ConcurrentDictionary<string, (string Variant, string Owner)> _claims = new(StringComparer.Ordinal);

    private static string Key(string? locationId) => (locationId ?? "").Trim().ToLowerInvariant();

    public enum Outcome
    {
        Claimed,
        Changed,
        Joined,
    }

    // TryAdd is what makes the race safe: it returns true only for the caller that inserted, so two
    // players entering the zone in the same frame cannot both be told they were first.
    public string Claim(string? locationId, string? variant, string owner, out Outcome outcome)
    {
        var offered = MapVariant.Normalise(variant);
        var key = Key(locationId);

        if (_claims.TryAdd(key, (offered, owner)))
        {
            outcome = Outcome.Claimed;
            return offered;
        }

        // Released between the TryAdd and here, so nobody holds it: take it.
        if (!_claims.TryGetValue(key, out var held))
        {
            _claims[key] = (offered, owner);
            outcome = Outcome.Claimed;
            return offered;
        }

        if (held.Owner != owner)
        {
            outcome = Outcome.Joined;
            return held.Variant;
        }

        _claims[key] = (offered, owner);
        outcome = held.Variant == offered ? Outcome.Claimed : Outcome.Changed;
        return offered;
    }

    public string Held(string? locationId)
        => _claims.TryGetValue(Key(locationId), out var claim) ? claim.Variant : "";

    public void Release(string? locationId) => _claims.TryRemove(Key(locationId), out _);
}
