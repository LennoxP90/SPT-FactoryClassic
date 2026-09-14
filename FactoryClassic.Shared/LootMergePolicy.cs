// Nullable is declared per file: Shared is source-linked into projects that disagree on the setting.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FactoryClassic.Shared
{
    // The decisions behind folding 4.1's Factory loot into the classic tile, kept free of SPT types so
    // they can be tested without a server.
    //
    // The whole approach rests on one thing: a loose loot spawn point carries its own coordinates, in
    // its locationId, as "(17.832003, 1.644, -29.806004)". So an item can be placed at the classic
    // point NEAREST to where 4.1 spawns it, rather than sprinkled at random - a medical item that
    // belonged by the offices stays by the offices.
    public static class LootMergePolicy
    {
        public readonly struct Position
        {
            public readonly float X, Y, Z;
            public Position(float x, float y, float z) { X = x; Y = y; Z = z; }

            // Squared, because only the ordering matters and the square root is not free across
            // hundreds of thousands of comparisons.
            public float DistanceSquaredTo(Position other)
            {
                float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
                return dx * dx + dy * dy + dz * dz;
            }
        }

        // "(17.832003, 1.644, -29.806004)". Invariant culture on purpose: a comma decimal separator
        // would otherwise parse "1.644" as 1644 on a machine with a European locale, and the item
        // would land a kilometre away rather than fail loudly.
        public static bool TryParsePosition(string? locationId, out Position position)
        {
            position = default;
            if (string.IsNullOrWhiteSpace(locationId)) return false;

            var text = locationId!.Trim();
            if (text.StartsWith("(", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.EndsWith(")", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1);

            var parts = text.Split(',');
            if (parts.Length != 3) return false;

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return false;
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return false;

            position = new Position(x, y, z);
            return true;
        }

        // Index of the closest candidate, or -1 when there are none. Ties go to the earlier index so
        // the same input always produces the same output; a merge that shuffles between runs cannot
        // be reasoned about.
        public static int NearestIndex(Position target, IReadOnlyList<Position> candidates)
        {
            if (candidates == null || candidates.Count == 0) return -1;

            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var distance = target.DistanceSquaredTo(candidates[i]);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        // What weight an item should carry once moved to a different spawn point.
        //
        // Relative probabilities only mean anything against their own point's total, so a weight of 8
        // among 20 is common while 8 among 2000 is rare. Carrying the raw number across would make an
        // item far rarer or far more common purely because of where it landed. This preserves its
        // SHARE: the same fraction of the destination's weight as it had of its source's.
        public static double ScaledWeight(double sourceWeight, double sourceTotal, double targetTotal)
        {
            if (sourceWeight <= 0d) return 0d;
            if (sourceTotal <= 0d || targetTotal <= 0d) return sourceWeight;
            return sourceWeight / sourceTotal * targetTotal;
        }

        // Relative probabilities are integers in the tables, so a scaled weight has to become one
        // without rounding a rare item away to nothing.
        public static int RoundWeight(double weight)
        {
            if (weight <= 0d) return 0;
            var rounded = (int)Math.Round(weight, MidpointRounding.AwayFromZero);
            return rounded < 1 ? 1 : rounded;
        }
    }
}
