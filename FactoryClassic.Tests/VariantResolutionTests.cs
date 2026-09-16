using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class VariantResolutionTests
{
    const string Classic = MapVariant.Classic;
    const string Original = MapVariant.Original;

    [Fact]
    public void AnOwnChoiceAlwaysWins()
    {
        Assert.Equal(Classic, VariantResolution.Resolve(Classic, Original, Original));
        Assert.Equal(Original, VariantResolution.Resolve(Original, Classic, Classic));
    }

    // A Fika headless hosts under its own session and so never has a choice of its own; this is how it
    // ends up on the map the starting player picked.
    [Fact]
    public void WithNoChoiceOfItsOwnTheLastChoiceIsInherited()
    {
        Assert.Equal(Classic, VariantResolution.Resolve(null, Classic, Original));
        Assert.Equal(Original, VariantResolution.Resolve(null, Original, Classic));
        Assert.Equal(Classic, VariantResolution.Resolve(VariantResolution.NoAnswer, Classic, Original));
    }

    [Fact]
    public void WithNothingRecordedAtAllTheConfiguredDefaultApplies()
    {
        Assert.Equal(Classic, VariantResolution.Resolve(null, null, Classic));
        Assert.Equal(Original, VariantResolution.Resolve(null, VariantResolution.NoAnswer, Original));
    }

    // An empty string means "nothing recorded", which is why the store returns it rather than
    // Original: the two have to stay distinguishable or a cold server would force the shipped map.
    [Fact]
    public void NoAnswerIsNotAnAnswer()
    {
        Assert.Equal(Classic, VariantResolution.Resolve(VariantResolution.NoAnswer, VariantResolution.NoAnswer, Classic));
    }

    [Fact]
    public void EveryAnswerIsNormalised()
    {
        Assert.Equal(Classic, VariantResolution.Resolve("CLASSIC", null, Original));
        Assert.Equal(Original, VariantResolution.Resolve(null, "vanilla", Classic));
        Assert.Equal(Original, VariantResolution.Resolve(null, null, "nonsense"));
    }
}
