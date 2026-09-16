using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class MapVariantTests
{
    [Theory]
    [InlineData("classic", "classic")]
    [InlineData("CLASSIC", "classic")]
    [InlineData("Classic", "classic")]
    [InlineData("original", "original")]
    [InlineData("vanilla", "original")]
    [InlineData("", "original")]
    [InlineData(null, "original")]
    [InlineData("nonsense", "original")]
    public void AnythingButClassicIsOriginal(string input, string expected)
    {
        Assert.Equal(expected, MapVariant.Normalise(input));
    }

    [Fact]
    public void IsClassicAgreesWithNormalise()
    {
        Assert.True(MapVariant.IsClassic("classic"));
        Assert.False(MapVariant.IsClassic("original"));
        Assert.False(MapVariant.IsClassic(null));
    }

    [Fact]
    public void DisplayNamesAreTheTwoLabelsTheSpecFixes()
    {
        Assert.Equal("Factory - Classic", VariantDisplay.For("classic"));
        Assert.Equal("Factory - Vanilla", VariantDisplay.For("original"));
        Assert.Equal("Factory - Vanilla", VariantDisplay.For(null));
    }

    // BSG's folder is named Factory_Rework and it is the CURRENT map, so the word means the opposite
    // of what it means in InterchangeRework and must never reach a player.
    [Fact]
    public void NoLabelSaysRework()
    {
        Assert.DoesNotContain("ework", VariantDisplay.ClassicLabel);
        Assert.DoesNotContain("ework", VariantDisplay.VanillaLabel);
    }

    // The wire value stays "original" so a consumer reading either mod's API sees one vocabulary.
    [Fact]
    public void TheWireValueIsNotTheDisplayWord()
    {
        Assert.Equal("original", MapVariant.Original);
        Assert.Contains("Vanilla", VariantDisplay.VanillaLabel);
    }
}
