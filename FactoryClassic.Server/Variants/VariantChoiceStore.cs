using System.Collections.Concurrent;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace FactoryClassic.Server.Variants;

// The starting player's choice for a raid, keyed by session and location. In memory only: a choice is
// a property of one raid, not of a profile, so nothing is written to disk.
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class VariantChoiceStore
{
    private readonly ConcurrentDictionary<string, string> _choices = new(StringComparer.Ordinal);

    // The most recent choice per location. A Fika headless asks under its OWN session rather than the
    // starting player's, and hosts one raid at a time, so this is how it inherits the answer.
    private readonly ConcurrentDictionary<string, string> _latest = new(StringComparer.Ordinal);

    private static string Key(MongoId sessionId, string locationId)
        => sessionId + "|" + (locationId ?? "").ToLowerInvariant();

    public void Record(MongoId sessionId, string locationId, string variant)
    {
        var normalised = MapVariant.Normalise(variant);
        _choices[Key(sessionId, locationId)] = normalised;
        _latest[(locationId ?? "").ToLowerInvariant()] = normalised;
    }

    // Empty, never Original, when nothing is recorded: VariantResolution needs to tell "no answer"
    // apart from "answered original", or a cold headless is forced onto the shipped map.
    public string LatestFor(string locationId)
        => _latest.TryGetValue((locationId ?? "").ToLowerInvariant(), out var variant) ? variant : VariantResolution.NoAnswer;

    public string ChoiceFor(MongoId sessionId, string locationId)
        => _choices.TryGetValue(Key(sessionId, locationId), out var variant) ? variant : VariantResolution.NoAnswer;

    public void Forget(MongoId sessionId, string locationId) => _choices.TryRemove(Key(sessionId, locationId), out _);
}
