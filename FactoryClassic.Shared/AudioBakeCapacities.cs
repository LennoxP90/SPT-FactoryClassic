#nullable enable
using System;

namespace FactoryClassic.Shared
{
    // A bake's last eight bytes are its per-pair maxima. SoundPropagationJobScheduler sizes its job
    // buffers from SpatialAudioLocationInfo's copy of the same two numbers, then slices them with
    // GetSubArray, unchecked in a release build. The classic tile's asset carries zero for both, so
    // the file's copy is transplanted onto it before the first slice.
    public static class AudioBakeCapacities
    {
        public const int TailLength = 8;

        public static bool TryReadTail(byte[]? tail, out uint maxRoutesPerPair, out uint maxPortalsPerPair)
        {
            maxRoutesPerPair = 0;
            maxPortalsPerPair = 0;
            if (tail == null || tail.Length != TailLength) return false;

            maxRoutesPerPair = BitConverter.ToUInt32(tail, 0);
            maxPortalsPerPair = BitConverter.ToUInt32(tail, 4);
            return true;
        }

        // Only ever raise. An oversized buffer is sliced down to the pair in hand; an undersized one
        // is the overrun. Zero capacities, as the empty table declares, therefore change nothing.
        public static uint Raise(uint current, uint fromBake) => fromBake > current ? fromBake : current;
    }
}
