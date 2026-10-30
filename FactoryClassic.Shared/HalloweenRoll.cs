#nullable enable

namespace FactoryClassic.Shared
{
    /// <summary>
    /// Whether a Classic raid is the Halloween version. The chance is a whole percentage.
    /// </summary>
    public static class HalloweenRoll
    {
        public const int DefaultChance = 50;

        public static int Clamp(int? chance)
        {
            var value = chance ?? DefaultChance;
            return value < 0 ? 0 : value > 100 ? 100 : value;
        }

        /// <param name="sample">Uniform in [0, 1).</param>
        public static bool Roll(int chance, double sample) => sample * 100 < Clamp(chance);
    }
}
