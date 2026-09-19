using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class VariantVocabularyTests
{
    [Theory]
    [InlineData("backport", MapVariant.Classic)]
    [InlineData("BACKPORT", MapVariant.Classic)]
    [InlineData("  backport  ", MapVariant.Classic)]
    [InlineData("original", MapVariant.Original)]
    [InlineData("unmanaged", MapVariant.Original)]
    [InlineData("classic", MapVariant.Original)]   // THEIR wire value is never "classic"
    [InlineData("", MapVariant.Original)]
    [InlineData(null, MapVariant.Original)]
    public void FromMapVariants_maps_their_words_to_ours(string? theirs, string ours)
        => Assert.Equal(ours, VariantVocabulary.FromMapVariants(theirs));

    [Theory]
    [InlineData(MapVariant.Classic, VariantVocabulary.MapVariantsBackport)]
    [InlineData(MapVariant.Original, VariantVocabulary.MapVariantsOriginal)]
    [InlineData("backport", VariantVocabulary.MapVariantsOriginal)]  // OUR wire value is never "backport"
    [InlineData(null, VariantVocabulary.MapVariantsOriginal)]
    public void ToMapVariants_maps_our_words_to_theirs(string? ours, string theirs)
        => Assert.Equal(theirs, VariantVocabulary.ToMapVariants(ours));

    [Theory]
    [InlineData(MapVariant.Classic)]
    [InlineData(MapVariant.Original)]
    public void Round_trip_through_their_vocabulary_is_lossless(string ours)
        => Assert.Equal(ours, VariantVocabulary.FromMapVariants(VariantVocabulary.ToMapVariants(ours)));

    [Theory]
    [InlineData(VariantVocabulary.MapVariantsBackport)]
    [InlineData(VariantVocabulary.MapVariantsOriginal)]
    public void Round_trip_through_our_vocabulary_is_lossless(string theirs)
        => Assert.Equal(theirs, VariantVocabulary.ToMapVariants(VariantVocabulary.FromMapVariants(theirs)));

    // The defect this whole module exists to prevent: a raw pass-through silently means "original".
    [Fact]
    public void A_raw_pass_through_would_be_wrong_in_both_directions()
    {
        Assert.Equal(MapVariant.Original, MapVariant.Normalise(VariantVocabulary.MapVariantsBackport));
        Assert.NotEqual(MapVariant.Normalise(VariantVocabulary.MapVariantsBackport),
                        VariantVocabulary.FromMapVariants(VariantVocabulary.MapVariantsBackport));
    }

    [Theory]
    [InlineData("unmanaged", true)]
    [InlineData("UNMANAGED", true)]
    [InlineData("original", false)]
    [InlineData(null, false)]
    public void IsUnmanaged_is_separate_from_the_variant(string? value, bool expected)
        => Assert.Equal(expected, VariantVocabulary.IsUnmanaged(value));
}
