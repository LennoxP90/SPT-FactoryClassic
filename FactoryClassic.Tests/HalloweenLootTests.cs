using FactoryClassic.Server.Halloween;
using SPTarkov.Server.Core.Models.Eft.Common;
using Xunit;

namespace FactoryClassic.Tests;

public class HalloweenLootTests
{
    private static StaticContainerData Container(string id) => new() { Template = new SpawnpointTemplate { Id = id } };

    [Fact]
    public void CorpsesOnlyOnHalloween()
    {
        var on = false;
        var list = HalloweenLoot.With([Container("crate")], [Container("corpse")], () => on);
        Assert.Equal(["crate"], list.Select(c => c.Template!.Id));
        on = true;
        Assert.Equal(["crate", "corpse"], list.Select(c => c.Template!.Id));
    }

    [Fact]
    public void CorpseEntryIsAGuaranteedContainerOfItsTemplate()
    {
        var entry = HalloweenLoot.Entry(new HalloweenCorpse { Id = "container_Factory_Event_00005", Tpl = HalloweenLoot.DeadCivilian });
        Assert.Equal(1f, entry.Probability);
        Assert.True(entry.Template!.IsAlwaysSpawn);
        Assert.Equal("container_Factory_Event_00005", entry.Template.Id);
        var item = Assert.Single(entry.Template.Items!);
        Assert.Equal(HalloweenLoot.DeadCivilian, item.Template);
        Assert.Equal(entry.Template.Root, item.Id.ToString());
    }
}
