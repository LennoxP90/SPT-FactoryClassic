using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

public class HalloweenRollTests
{
    [Fact] public void ClampNullIsDefault() => Assert.Equal(50, HalloweenRoll.Clamp(null));
    [Fact] public void ClampLow() => Assert.Equal(0, HalloweenRoll.Clamp(-5));
    [Fact] public void ClampHigh() => Assert.Equal(100, HalloweenRoll.Clamp(150));
    [Fact] public void ZeroNever() => Assert.False(HalloweenRoll.Roll(0, 0.0));
    [Fact] public void HundredAlways() => Assert.True(HalloweenRoll.Roll(100, 0.9999));

    [Fact]
    public void FiftyBoundary()
    {
        Assert.True(HalloweenRoll.Roll(50, 0.4999));
        Assert.False(HalloweenRoll.Roll(50, 0.5));
    }
}
