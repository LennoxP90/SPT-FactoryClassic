#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// The arithmetic behind folding 4.1's Factory loot into the classic tile, free of SPT types so
    /// it can be tested without a server. It rests on one thing: a loose loot spawn point carries
    /// its own coordinates, so an item can be placed at the nearest classic point.
    /// </summary>
    public static class LootMergePolicy
    {
        public readonly struct Position
        {
            public readonly float X, Y, Z;
            public Position(float x, float y, float z) { X = x; Y = y; Z = z; }

            // Squared: only the ordering matters, over hundreds of thousands of comparisons.
            public float DistanceSquaredTo(Position other)
            {
                float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
                return dx * dx + dy * dy + dz * dz;
            }
        }

        /// <summary>
        /// Reads "(17.832003, 1.644, -29.806004)". The culture is pinned: a comma decimal separator
        /// would parse "1.644" as 1644 and put the item a kilometre away rather than fail.
        /// </summary>
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

        /// <summary>
        /// Index of the closest candidate, or -1 when there are none. Ties go to the earlier index,
        /// so the same tables always produce the same merge.
        /// </summary>
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

        /// <summary>
        /// A weight means nothing except against its own point's total: 8 among 20 is common, 8
        /// among 2000 is rare. So the item's SHARE moves with it, not its number.
        /// </summary>
        public static double ScaledWeight(double sourceWeight, double sourceTotal, double targetTotal)
        {
            if (sourceWeight <= 0d) return 0d;
            if (sourceTotal <= 0d || targetTotal <= 0d) return sourceWeight;
            return sourceWeight / sourceTotal * targetTotal;
        }

        // Floored at one, so a rare item becomes rare rather than rounding away to nothing.
        public static int RoundWeight(double weight)
        {
            if (weight <= 0d) return 0;
            var rounded = (int)Math.Round(weight, MidpointRounding.AwayFromZero);
            return rounded < 1 ? 1 : rounded;
        }
    }
}
