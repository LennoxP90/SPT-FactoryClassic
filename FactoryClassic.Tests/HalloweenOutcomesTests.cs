using FactoryClassic.Server.Halloween;
using Xunit;

namespace FactoryClassic.Tests;

public class HalloweenOutcomesTests
{
    private static DateTime _now = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

    private static HalloweenOutcomes Make(int chance, params double[] samples)
    {
        var queue = new Queue<double>(samples);
        return new HalloweenOutcomes(() => chance, () => queue.Dequeue(), () => _now);
    }

    [Fact]
    public void SecondAskReturnsMemo()
    {
        var o = Make(50, 0.1, 0.9);
        Assert.True(o.Outcome("factory4_day", "host"));
        Assert.True(o.Outcome("FACTORY4_DAY", "peer"));
    }

    [Fact]
    public void ClearRerolls()
    {
        var o = Make(50, 0.1, 0.9);
        o.Outcome("factory4_day", "host");
        o.Clear("host");
        Assert.False(o.Outcome("factory4_day", "host"));
    }

    [Fact]
    public void ChanceReadAtEachRoll()
    {
        var chances = new Queue<int?>([0, 100]);
        var o = new HalloweenOutcomes(() => chances.Dequeue(), () => 0.5, () => _now);
        Assert.False(o.Outcome("factory4_day", "host"));
        o.Clear("host");
        Assert.True(o.Outcome("factory4_day", "host"));
    }

    [Fact]
    public void MissingKeyUsesDefault()
    {
        var o = new HalloweenOutcomes(() => null, () => 0.49, () => _now);
        Assert.True(o.Outcome("factory4_night", "host"));
    }

    [Fact]
    public void DayAndNightIndependent()
    {
        var o = Make(50, 0.1, 0.9);
        Assert.True(o.Outcome("factory4_day", "host"));
        Assert.False(o.Outcome("factory4_night", "host"));
    }

    [Fact]
    public void AnotherPlayersRaidEndKeepsTheAnswer()
    {
        var o = Make(50, 0.1, 0.9);
        Assert.True(o.Outcome("factory4_day", "host"));
        o.Clear("peer");
        Assert.True(o.Outcome("factory4_day", "late-joiner"));
    }

    [Fact]
    public void AnAnswerOlderThanAnyRaidRerolls()
    {
        var now = _now;
        var queue = new Queue<double>([0.1, 0.9]);
        var o = new HalloweenOutcomes(() => 50, () => queue.Dequeue(), () => now);
        Assert.True(o.Outcome("factory4_day", "host"));
        now += HalloweenOutcomes.MaxAge + TimeSpan.FromMinutes(1);
        Assert.False(o.Outcome("factory4_day", "host"));
    }

    [Fact]
    public void LootRollIsClaimedByTheFirstClientThatAsks()
    {
        var o = Make(50, 0.1, 0.9);
        Assert.True(o.Outcome("factory4_day", ""));
        o.Clear("host");
        Assert.True(o.Outcome("factory4_day", "host"));
        o.Clear("peer");
        Assert.True(o.Outcome("factory4_day", "peer"));
        o.Clear("host");
        Assert.False(o.Outcome("factory4_day", "host"));
    }

    [Fact]
    public void FreshTellsARollFromAHeldAnswer()
    {
        var o = Make(50, 0.1);
        o.Outcome("factory4_day", "host", out var first);
        o.Outcome("factory4_day", "peer", out var second);
        Assert.True(first);
        Assert.False(second);
    }
}
