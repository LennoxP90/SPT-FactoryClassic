using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class ExtensionApiContractTests
{
    // The contract's ids and the ones the mod actually acts on must be the same list, or a consumer
    // would be told about a location we never serve.
    [Fact]
    public void TheContractIdsAreTheOnesTheModServes()
    {
        Assert.Equal(
            new[] { ExtensionApiContract.DayLocationId, ExtensionApiContract.NightLocationId },
            FactoryScenes.ServerNames);
    }

    [Fact]
    public void TheVariantValuesAreTheWireValues()
    {
        Assert.Equal("classic", MapVariant.Classic);
        Assert.Equal("original", MapVariant.Original);
    }

    [Fact]
    public void TheVersionIsSet()
    {
        Assert.False(string.IsNullOrWhiteSpace(ExtensionApiContract.Version));
    }
}
