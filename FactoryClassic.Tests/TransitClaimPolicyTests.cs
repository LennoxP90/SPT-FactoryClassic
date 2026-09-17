using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

// The rule a transit arrives under: first into the zone decides, everyone joining reads that.
public class TransitClaimPolicyTests
{
    // Customs, Woods and Labyrinth all name factory4_day; nothing transits to factory4_night.
    [Theory]
    [InlineData("factory4_day", true)]
    [InlineData("FACTORY4_DAY", true)]
    [InlineData(" factory4_day ", true)]
    [InlineData("factory4_night", false)]
    [InlineData("Woods", false)]
    [InlineData("bigmap", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyTheDayTileIsATransitDestination(string destination, bool expected)
        => Assert.Equal(expected, TransitClaimPolicy.IsFactoryBound(destination));

    [Fact]
    public void AnUnclaimedDestinationTakesWhatIsOffered()
    {
        Assert.True(TransitClaimPolicy.WasFirst(null));
        Assert.True(TransitClaimPolicy.WasFirst(""));
        Assert.Equal(MapVariant.Classic, TransitClaimPolicy.Winner(null, MapVariant.Classic));
    }

    // The point of the rule: a second player joining does not get their own preference.
    [Fact]
    public void AClaimedDestinationIgnoresWhatIsOfferedAfterwards()
    {
        Assert.False(TransitClaimPolicy.WasFirst(MapVariant.Classic));
        Assert.Equal(MapVariant.Classic, TransitClaimPolicy.Winner(MapVariant.Classic, MapVariant.Original));
        Assert.Equal(MapVariant.Original, TransitClaimPolicy.Winner(MapVariant.Original, MapVariant.Classic));
    }

    [Fact]
    public void AnythingUnrecognisedIsTheShippedMap()
        => Assert.Equal(MapVariant.Original, TransitClaimPolicy.Winner(null, "nonsense"));
}
