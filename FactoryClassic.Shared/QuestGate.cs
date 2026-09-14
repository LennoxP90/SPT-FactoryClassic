#nullable enable
using System;
using System.Collections.Generic;

namespace FactoryClassic.Shared
{
    // What the mod does about quests the classic tile cannot satisfy.
    //
    // Six quests place their objective on a trigger zone that exists only in the scenes SPT 4.1
    // ships. The zones are GameObjects baked into those scenes, so on the classic tile they are
    // simply not there and the objective can never fire. Grafting replacements in was rejected:
    // it would put quest triggers on a map BSG never put them on, and guessing where is worse
    // than saying so.
    public static class QuestGateMode
    {
        // Let the player take them and say so before a classic raid loads. The default, because it
        // costs the player nothing and explains the one thing that would otherwise look like a bug.
        public const string Warn = "warn";

        // Also keep them off the trader board while classic is the map in play, so the situation
        // does not arise. Never applied to a quest already accepted.
        public const string Hide = "hide";

        // Do nothing at all.
        public const string Off = "off";

        public static string Normalise(string? mode)
        {
            var trimmed = (mode ?? "").Trim().ToLowerInvariant();
            return trimmed == Hide || trimmed == Off ? trimmed : Warn;
        }

        public static bool Warns(string? mode) => Normalise(mode) != Off;
        public static bool Hides(string? mode) => Normalise(mode) == Hide;
    }

    // SPT's QuestStatusEnum, mirrored so this file needs no server reference and still compiles
    // under net472 for the client. QuestGatePolicyTests asserts every value against the real enum,
    // so a renumber upstream fails the build rather than silently regrading every quest.
    public static class QuestStatusCode
    {
        public const int Locked = 0;
        public const int AvailableForStart = 1;
        public const int Started = 2;
        public const int AvailableForFinish = 3;
        public const int Success = 4;
        public const int Fail = 5;
        public const int FailRestartable = 6;
        public const int MarkedAsFailed = 7;
        public const int Expired = 8;
        public const int AvailableAfter = 9;
    }

    public static class QuestGatePolicy
    {
        // In the player's active list: taken, not yet handed in. The hard boundary of the whole
        // feature is here - an accepted quest is warned about and never touched, so the mod never
        // hides, fails, rewrites or removes one, and never writes to a profile.
        public static bool IsAccepted(int status)
            => status == QuestStatusCode.Started || status == QuestStatusCode.AvailableForFinish;

        // Finished with, one way or another. Nothing to warn about and nothing to hide.
        public static bool IsSettled(int status)
            => status == QuestStatusCode.Success
            || status == QuestStatusCode.Fail
            || status == QuestStatusCode.MarkedAsFailed
            || status == QuestStatusCode.Expired;

        // A quest with no entry in the profile has never been offered, which is the state every
        // quest starts in, so an absent status is treated as Locked rather than as an error.
        public static int StatusOf(IReadOnlyDictionary<string, int> statuses, string questId)
            => statuses != null && statuses.TryGetValue(questId, out var status) ? status : QuestStatusCode.Locked;

        // The names to show the player before a classic raid loads, sorted so the list reads the
        // same every time.
        public static List<string> Warnings(
            IReadOnlyDictionary<string, string> gated, IReadOnlyDictionary<string, int> statuses)
        {
            var names = new List<string>();
            if (gated == null) return names;

            foreach (var entry in gated)
                if (IsAccepted(StatusOf(statuses, entry.Key)))
                    names.Add(entry.Value ?? entry.Key);

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // The ids to keep off the trader board. Accepted is excluded by the boundary above; settled
        // is excluded because removing a completed quest from the list would read to the player as
        // losing it.
        public static HashSet<string> Hidden(
            IEnumerable<string> gatedIds, IReadOnlyDictionary<string, int> statuses)
        {
            var hidden = new HashSet<string>(StringComparer.Ordinal);
            if (gatedIds == null) return hidden;

            foreach (var questId in gatedIds)
            {
                var status = StatusOf(statuses, questId);
                if (!IsAccepted(status) && !IsSettled(status)) hidden.Add(questId);
            }
            return hidden;
        }
    }
}
