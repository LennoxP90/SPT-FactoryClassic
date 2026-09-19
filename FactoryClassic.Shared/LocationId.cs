#nullable enable

namespace FactoryClassic.Shared
{
    /// <summary>
    /// One spelling of a location id wherever one crosses a boundary. SPT's own casing is mixed, so
    /// a raw comparison is right for factory4_day and wrong for Lighthouse.
    /// </summary>
    public static class LocationId
    {
        public static string Normalise(string? locationId) => (locationId ?? "").Trim().ToLowerInvariant();
    }
}
