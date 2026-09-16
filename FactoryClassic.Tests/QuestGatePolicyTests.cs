using System.Collections.Generic;
using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Enums;
using Xunit;

namespace FactoryClassic.Tests;

public class QuestGatePolicyTests
{
    // QuestStatusCode mirrors SPT's enum so the shared policy needs no server reference and still
    // compiles under net472 for the client. That mirror is a copy, and a copy drifts silently: a
    // renumber upstream would regrade every quest with nothing to show for it. This is the test that
    // turns that into a build failure.
    [Fact]
    public void TheMirroredStatusCodesMatchSpt()
    {
        Assert.Equal((int)QuestStatusEnum.Locked, QuestStatusCode.Locked);
        Assert.Equal((int)QuestStatusEnum.AvailableForStart, QuestStatusCode.AvailableForStart);
        Assert.Equal((int)QuestStatusEnum.Started, QuestStatusCode.Started);
        Assert.Equal((int)QuestStatusEnum.AvailableForFinish, QuestStatusCode.AvailableForFinish);
        Assert.Equal((int)QuestStatusEnum.Success, QuestStatusCode.Success);
        Assert.Equal((int)QuestStatusEnum.Fail, QuestStatusCode.Fail);
        Assert.Equal((int)QuestStatusEnum.FailRestartable, QuestStatusCode.FailRestartable);
        Assert.Equal((int)QuestStatusEnum.MarkedAsFailed, QuestStatusCode.MarkedAsFailed);
        Assert.Equal((int)QuestStatusEnum.Expired, QuestStatusCode.Expired);
        Assert.Equal((int)QuestStatusEnum.AvailableAfter, QuestStatusCode.AvailableAfter);
    }

    [Theory]
    [InlineData(null, QuestGateMode.Warn)]
    [InlineData("", QuestGateMode.Warn)]
    [InlineData("  ", QuestGateMode.Warn)]
    [InlineData("nonsense", QuestGateMode.Warn)]
    [InlineData("HIDE", QuestGateMode.Hide)]
    [InlineData(" Off ", QuestGateMode.Off)]
    public void AnUnreadableModeFallsBackToWarning(string configured, string expected)
    {
        Assert.Equal(expected, QuestGateMode.Normalise(configured));
    }

    [Fact]
    public void OnlyHideHides_AndOnlyOffIsSilent()
    {
        Assert.True(QuestGateMode.Warns(QuestGateMode.Warn));
        Assert.True(QuestGateMode.Warns(QuestGateMode.Hide));
        Assert.False(QuestGateMode.Warns(QuestGateMode.Off));

        Assert.False(QuestGateMode.Hides(QuestGateMode.Warn));
        Assert.True(QuestGateMode.Hides(QuestGateMode.Hide));
        Assert.False(QuestGateMode.Hides(QuestGateMode.Off));
    }

    static readonly Dictionary<string, string> Gated = new()
    {
        ["q1"] = "Capacity Check",
        ["q2"] = "Black Swan",
        ["q3"] = "Is This a Reference",
    };

    [Fact]
    public void WarningsNameTheQuestsTheProfileHasAccepted()
    {
        var statuses = new Dictionary<string, int>
        {
            ["q1"] = QuestStatusCode.Started,
            ["q2"] = QuestStatusCode.AvailableForFinish,
            ["q3"] = QuestStatusCode.AvailableForStart,
        };

        Assert.Equal(new[] { "Black Swan", "Capacity Check" }, QuestGatePolicy.Warnings(Gated, statuses));
    }

    [Fact]
    public void AQuestAlreadyHandedInIsNotWorthWarningAbout()
    {
        var statuses = new Dictionary<string, int> { ["q1"] = QuestStatusCode.Success };
        Assert.Empty(QuestGatePolicy.Warnings(Gated, statuses));
    }

    [Fact]
    public void NothingAcceptedMeansNothingToSay()
    {
        Assert.Empty(QuestGatePolicy.Warnings(Gated, new Dictionary<string, int>()));
        Assert.Empty(QuestGatePolicy.Warnings(Gated, null));
        Assert.Empty(QuestGatePolicy.Warnings(null, new Dictionary<string, int>()));
    }

    // The boundary the whole design rests on. Once a quest is in the player's list it stays there,
    // whatever the mode: hiding it would look like the mod deleted their progress.
    [Theory]
    [InlineData(QuestStatusCode.Started)]
    [InlineData(QuestStatusCode.AvailableForFinish)]
    public void AnAcceptedQuestIsNeverHidden(int status)
    {
        var statuses = new Dictionary<string, int> { ["q1"] = status };
        Assert.DoesNotContain("q1", QuestGatePolicy.Hidden(Gated.Keys, statuses));
    }

    [Theory]
    [InlineData(QuestStatusCode.Success)]
    [InlineData(QuestStatusCode.Fail)]
    [InlineData(QuestStatusCode.MarkedAsFailed)]
    [InlineData(QuestStatusCode.Expired)]
    public void AQuestAlreadySettledIsLeftWhereItIs(int status)
    {
        var statuses = new Dictionary<string, int> { ["q1"] = status };
        Assert.DoesNotContain("q1", QuestGatePolicy.Hidden(Gated.Keys, statuses));
    }

    [Theory]
    [InlineData(QuestStatusCode.Locked)]
    [InlineData(QuestStatusCode.AvailableForStart)]
    [InlineData(QuestStatusCode.AvailableAfter)]
    [InlineData(QuestStatusCode.FailRestartable)]
    public void AQuestNotYetTakenIsTheOnlyKindHidden(int status)
    {
        var statuses = new Dictionary<string, int> { ["q1"] = status };
        Assert.Contains("q1", QuestGatePolicy.Hidden(Gated.Keys, statuses));
    }

    // A quest the profile has never seen has no entry at all, which is the normal case for a fresh
    // profile rather than a fault.
    [Fact]
    public void AQuestMissingFromTheProfileCountsAsNotYetOffered()
    {
        Assert.Equal(QuestStatusCode.Locked, QuestGatePolicy.StatusOf(new Dictionary<string, int>(), "q1"));
        Assert.Equal(new[] { "q1", "q2", "q3" }, new SortedSet<string>(QuestGatePolicy.Hidden(Gated.Keys, null)));
    }
}
