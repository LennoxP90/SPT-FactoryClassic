using FactoryClassic.Shared;

namespace FactoryClassic.Server.Halloween;

/// <summary>
/// One Halloween answer per location, rolled on the first ask and held for the raid, so the host, the
/// headless and every client of one raid load the same scenes. The chance is read at each roll, which is
/// what lets a change in the config apply to the next raid without a restart.
///
/// Only the raid end of the session that rolled forgets an answer: in Fika every peer ends its own raid,
/// and a peer dying early must not reroll for a late joiner. An answer older than any raid is rolled again,
/// which covers a roller that never sent its raid end (crash, killed headless). Loot generation rolls with no
/// session, before any client asks; the first client that asks then owns that answer.
/// </summary>
public class HalloweenOutcomes(Func<int?> readChance, Func<double> sample, Func<DateTime> now)
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(90);

    private readonly Dictionary<string, (bool Halloween, string Roller, DateTime At)> _outcomes = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public bool Outcome(string locationId, string session) => Outcome(locationId, session, out _);

    public bool Outcome(string locationId, string session, out bool fresh)
    {
        var id = LocationId.Normalise(locationId);
        lock (_lock)
        {
            fresh = false;
            if (_outcomes.TryGetValue(id, out var held) && now() - held.At < MaxAge)
            {
                if (held.Roller == "" && session != "") _outcomes[id] = held with { Roller = session };
                return held.Halloween;
            }
            fresh = true;
            var rolled = HalloweenRoll.Roll(HalloweenRoll.Clamp(readChance()), sample());
            _outcomes[id] = (rolled, session, now());
            return rolled;
        }
    }

    public void Clear(string session)
    {
        lock (_lock)
            foreach (var id in _outcomes.Where(o => o.Value.Roller == session).Select(o => o.Key).ToList())
                _outcomes.Remove(id);
    }
}
