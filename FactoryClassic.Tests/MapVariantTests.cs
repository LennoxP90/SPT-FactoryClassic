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

    // What is registered with MapVariants is the map name and the two SHORT variant names, because
    // that mod composes "<map> - <variant>" itself. Registering the full labels would put the map name
    // in twice: "Factory - Factory - Classic". This pins the three pieces AND the join.
    [Fact]
    public void TheRegisteredPiecesComposeIntoTheTwoLabels()
    {
        Assert.Equal("Factory", VariantDisplay.MapName);
        Assert.Equal("Classic", VariantDisplay.ClassicName);
        Assert.Equal("Vanilla", VariantDisplay.VanillaName);

        Assert.Equal(VariantDisplay.ClassicLabel, VariantDisplay.MapName + " - " + VariantDisplay.ClassicName);
        Assert.Equal(VariantDisplay.VanillaLabel, VariantDisplay.MapName + " - " + VariantDisplay.VanillaName);

        Assert.DoesNotContain(VariantDisplay.MapName, VariantDisplay.ClassicName);
        Assert.DoesNotContain(VariantDisplay.MapName, VariantDisplay.VanillaName);
    }

    // The wire value stays "original" so a consumer reading either mod's API sees one vocabulary.
    [Fact]
    public void TheWireValueIsNotTheDisplayWord()
    {
        Assert.Equal("original", MapVariant.Original);
        Assert.Contains("Vanilla", VariantDisplay.VanillaLabel);
    }
}
