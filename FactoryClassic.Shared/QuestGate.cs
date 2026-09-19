#nullable enable
using System;
using System.Collections.Generic;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// What the mod does about the six quests whose objective sits on a trigger zone that exists
    /// only in the scenes SPT 4.1 ships. See docs/QUESTS.md.
    /// </summary>
    public static class QuestGateMode
    {
        public const string Warn = "warn";
        public const string Hide = "hide";
        public const string Off = "off";

        public static string Normalise(string? mode)
        {
            var trimmed = (mode ?? "").Trim().ToLowerInvariant();
            return trimmed == Hide || trimmed == Off ? trimmed : Warn;
        }

        public static bool Warns(string? mode) => Normalise(mode) != Off;
        public static bool Hides(string? mode) => Normalise(mode) == Hide;
    }

    /// <summary>
    /// SPT's QuestStatusEnum, mirrored so this compiles under net472 with no server reference.
    /// QuestGatePolicyTests asserts every value against the real enum, so a renumber upstream fails
    /// the build rather than silently regrading every quest.
    /// </summary>
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
        /// <summary>
        /// The hard boundary of the feature: an accepted quest is warned about, never touched.
        /// </summary>
        public static bool IsAccepted(int status)
            => status == QuestStatusCode.Started || status == QuestStatusCode.AvailableForFinish;

        // Finished with, one way or another. Nothing to warn about and nothing to hide.
        public static bool IsSettled(int status)
            => status == QuestStatusCode.Success
            || status == QuestStatusCode.Fail
            || status == QuestStatusCode.MarkedAsFailed
            || status == QuestStatusCode.Expired;

        // Absent is the state every quest starts in, so it reads as Locked, not as an error.
        public static int StatusOf(IReadOnlyDictionary<string, int> statuses, string questId)
            => statuses != null && statuses.TryGetValue(questId, out var status) ? status : QuestStatusCode.Locked;

        // Sorted, so the list reads the same every time.
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

        // Settled ones are excluded too: removing a completed quest would read as losing it.
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
