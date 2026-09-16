using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class LootMergePolicyTests
{
    // The real format, copied from a shipped looseLoot.json spawn point.
    [Fact]
    public void ParsesTheShippedPositionFormat()
    {
        Assert.True(LootMergePolicy.TryParsePosition("(17.832003, 1.644, -29.806004)", out var position));
        Assert.Equal(17.832003f, position.X, 4);
        Assert.Equal(1.644f, position.Y, 4);
        Assert.Equal(-29.806004f, position.Z, 4);
    }

    [Theory]
    [InlineData("17.8, 1.6, -29.8")]      // no brackets
    [InlineData(" (1, 2, 3) ")]           // padded
    public void ToleratesReasonableVariations(string text)
    {
        Assert.True(LootMergePolicy.TryParsePosition(text, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(1, 2)")]                 // too few
    [InlineData("(1, 2, 3, 4)")]           // too many
    [InlineData("(a, b, c)")]
    public void RefusesAnythingItCannotParse(string text)
    {
        Assert.False(LootMergePolicy.TryParsePosition(text, out _));
    }

    // A European locale parses "1.644" as 1644 with the current culture, which would silently move an
    // item a kilometre rather than fail. The parse pins InvariantCulture for exactly this.
    [Fact]
    public void ParsingDoesNotDependOnTheMachineLocale()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(LootMergePolicy.TryParsePosition("(17.832003, 1.644, -29.806004)", out var position));
            Assert.Equal(1.644f, position.Y, 4);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    static LootMergePolicy.Position P(float x, float y, float z) => new(x, y, z);

    [Fact]
    public void FindsTheNearestCandidate()
    {
        var candidates = new List<LootMergePolicy.Position> { P(0, 0, 0), P(10, 0, 0), P(3, 0, 0) };
        Assert.Equal(2, LootMergePolicy.NearestIndex(P(4, 0, 0), candidates));
        Assert.Equal(0, LootMergePolicy.NearestIndex(P(-1, 0, 0), candidates));
        Assert.Equal(1, LootMergePolicy.NearestIndex(P(100, 0, 0), candidates));
    }

    // Height matters as much as ground distance: the classic tile is stacked, and ignoring Y would
    // drop an upper-floor item into the tunnels below it.
    [Fact]
    public void HeightCountsTowardsDistance()
    {
        var candidates = new List<LootMergePolicy.Position> { P(0, 0, 0), P(0, 20, 0) };
        Assert.Equal(1, LootMergePolicy.NearestIndex(P(0, 18, 0), candidates));
    }

    // Deterministic on ties, so the same tables always produce the same merge.
    [Fact]
    public void TiesGoToTheEarlierCandidate()
    {
        var candidates = new List<LootMergePolicy.Position> { P(1, 0, 0), P(-1, 0, 0) };
        Assert.Equal(0, LootMergePolicy.NearestIndex(P(0, 0, 0), candidates));
    }

    [Fact]
    public void NoCandidatesMeansNoAnswer()
    {
        Assert.Equal(-1, LootMergePolicy.NearestIndex(P(0, 0, 0), new List<LootMergePolicy.Position>()));
        Assert.Equal(-1, LootMergePolicy.NearestIndex(P(0, 0, 0), null));
    }

    // The point of the scaling: a weight means nothing except against its own point's total.
    [Fact]
    public void WeightKeepsItsShareRatherThanItsNumber()
    {
        // 8 of 20 at the source is 40%, so 40% of a 2000-weight destination.
        Assert.Equal(800d, LootMergePolicy.ScaledWeight(8, 20, 2000), 6);
        // And the reverse: a big number among a bigger total stays proportionally small.
        Assert.Equal(1d, LootMergePolicy.ScaledWeight(100, 2000, 20), 6);
    }

    [Fact]
    public void DegenerateTotalsLeaveTheWeightAlone()
    {
        Assert.Equal(8d, LootMergePolicy.ScaledWeight(8, 0, 100), 6);
        Assert.Equal(8d, LootMergePolicy.ScaledWeight(8, 100, 0), 6);
        Assert.Equal(0d, LootMergePolicy.ScaledWeight(0, 100, 100), 6);
    }

    // A rare item scaled into a small point can land below 0.5 and round to zero, which would delete
    // it rather than make it rare.
    [Fact]
    public void RoundingNeverDeletesAnItemThatHasAnyWeight()
    {
        Assert.Equal(1, LootMergePolicy.RoundWeight(0.0001));
        Assert.Equal(1, LootMergePolicy.RoundWeight(0.5));
        Assert.Equal(3, LootMergePolicy.RoundWeight(2.5));
        Assert.Equal(0, LootMergePolicy.RoundWeight(0));
        Assert.Equal(0, LootMergePolicy.RoundWeight(-5));
    }
}
