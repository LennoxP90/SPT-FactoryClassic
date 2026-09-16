using System.IO;
using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

// Asserted against the tables actually shipped in plugin-data, so a re-conversion that drops the
// tail or writes another tile's numbers fails the build rather than a raid.
public class AudioBakeCapacityTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SPT-FactoryClassic.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    static byte[] TailOf(string name)
    {
        var path = Path.Combine(RepoRoot(), "FactoryClassic.Client", "plugin-data", name);
        Assert.True(File.Exists(path), path + " is missing");

        var file = File.ReadAllBytes(path);
        var tail = new byte[AudioBakeCapacities.TailLength];
        System.Array.Copy(file, file.Length - tail.Length, tail, 0, tail.Length);
        return tail;
    }

    // 1550 pairs, the busiest holding 311 routes across 1129 portals.
    [Fact]
    public void TheConvertedTableCarriesItsOwnPerPairMaxima()
    {
        Assert.True(AudioBakeCapacities.TryReadTail(TailOf("factory_classic.audiobakedata"),
                                                    out var routes, out var portals));
        Assert.Equal(311u, routes);
        Assert.Equal(1129u, portals);
    }

    // No pairs, so no capacities, so Raise leaves the asset alone.
    [Fact]
    public void TheEmptyTableAsksForNothing()
    {
        Assert.True(AudioBakeCapacities.TryReadTail(TailOf("factory_classic_empty.audiobakedata"),
                                                    out var routes, out var portals));
        Assert.Equal(0u, routes);
        Assert.Equal(0u, portals);
        Assert.Equal(7u, AudioBakeCapacities.Raise(7u, routes));
    }

    [Fact]
    public void ARaiseNeverLowersAValueTheSceneAlreadyHolds()
    {
        Assert.Equal(3239u, AudioBakeCapacities.Raise(3239u, 311u));
        Assert.Equal(311u, AudioBakeCapacities.Raise(0u, 311u));
        Assert.Equal(311u, AudioBakeCapacities.Raise(311u, 311u));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void AnythingThatIsNotExactlyTheTailIsRefused(int length)
    {
        Assert.False(AudioBakeCapacities.TryReadTail(new byte[length], out _, out _));
    }

    [Fact]
    public void ANullTailIsRefusedRatherThanReadAsZero()
    {
        Assert.False(AudioBakeCapacities.TryReadTail(null, out _, out _));
    }
}
