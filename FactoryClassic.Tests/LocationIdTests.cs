using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class LocationIdTests
{
    // The casing cases are the ones /mapvariants/managed actually returns: SPT's own spelling, which
    // is lower for the two Factory ids and capitalised for Lighthouse and Interchange. Comparing that
    // list without normalising is right for Factory and silently wrong for Lighthouse.
    [Theory]
    [InlineData("factory4_day", "factory4_day")]
    [InlineData("Lighthouse", "lighthouse")]
    [InlineData("Interchange", "interchange")]
    [InlineData("  FACTORY4_NIGHT  ", "factory4_night")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalise_trims_and_lower_cases(string? given, string expected)
        => Assert.Equal(expected, LocationId.Normalise(given));
}
