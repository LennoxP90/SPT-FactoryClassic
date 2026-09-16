using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

// The fixtures are the exact contents of factory_day_preset.bundle and factory_night_preset.bundle
// as shipped by the SPT 4.1 client, read on 2026-09-14.
public class FactoryScenesTests
{
    const string C = "Assets/Content/Locations/Factory_Rework/";

    static readonly string[] ShippedDay = { C + "Factory_Rework_Day_Scripts.unity", C + "Factory_Rework_Day_Light.unity" };
    static readonly string[] ShippedNight = { C + "Factory_Rework_Night_Scripts.unity", C + "Factory_Rework_Night_Light.unity" };
    static readonly string[] ShippedDayCulling = { C + "Factory_Rework_Quests.unity", C + "Factory_Rework_Day_Culling.unity" };
    static readonly string[] ShippedNightCulling = { C + "Factory_Rework_Night_Quests.unity", C + "Factory_Rework_Night_Culling.unity" };

    static readonly string[] ShippedBase =
    {
        C + "Factory_Rework_Areas.unity", C + "Factory_Rework_Main_Building.unity",
        C + "Factory_Rework_Basement.unity", C + "Factory_Rework_Admin_Office.unity",
        C + "Factory_Rework_Laboratory.unity", C + "Factory_Rework_Background.unity",
        C + "Factory_Rework_DesignStuff.unity", C + "Factory_Rework_DesignMain.unity",
        C + "Factory_Rework_AI.unity", C + "Factory_Sound_Rework.unity",
    };

    [Fact]
    public void ClassifiesEachShippedPreset()
    {
        Assert.Equal(PresetKind.Day, FactoryScenes.Classify(ShippedDay));
        Assert.Equal(PresetKind.Night, FactoryScenes.Classify(ShippedNight));
        Assert.Equal(PresetKind.Base, FactoryScenes.Classify(ShippedBase));
        Assert.Equal(PresetKind.Culling, FactoryScenes.Classify(ShippedDayCulling));
        Assert.Equal(PresetKind.Culling, FactoryScenes.Classify(ShippedNightCulling));
    }

    [Fact]
    public void AnotherMapsPresetIsNotOurs()
    {
        Assert.Equal(PresetKind.NotOurs, FactoryScenes.Classify(new[] { "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_AI.unity" }));
        Assert.Equal(PresetKind.NotOurs, FactoryScenes.Classify(new string[] { null }));
        Assert.Equal(PresetKind.NotOurs, FactoryScenes.Classify(new string[0]));
    }

    // The night culling child declares ServerName "factory4_day". Classification must read the paths.
    [Fact]
    public void ClassificationIgnoresServerName()
    {
        Assert.Equal(PresetKind.Culling, FactoryScenes.Classify(ShippedNightCulling));
    }

    // Day and Night culling both contain "Day"/"Night" in their names, so ordering inside Classify
    // matters: Culling has to be tested first.
    [Fact]
    public void CullingIsNotMistakenForDayOrNight()
    {
        Assert.NotEqual(PresetKind.Day, FactoryScenes.Classify(ShippedDayCulling));
        Assert.NotEqual(PresetKind.Night, FactoryScenes.Classify(ShippedNightCulling));
    }

    [Fact]
    public void ClassicBaseIsTheSixScenesSharedByDayAndNight()
    {
        Assert.Equal(new[]
        {
            "Assets/Content/Locations/Factory/Factory.unity",
            "Assets/Content/Locations/Factory/Factory_Scripts.unity",
            "Assets/Content/Locations/Factory/Factory_AI.unity",
            "Assets/Content/Locations/Factory/Factory_DesignStuff.unity",
            "Assets/Content/Locations/Factory/Factory_new.unity",
            "Assets/Content/Locations/Factory/Factory_Sound.unity",
        }, FactoryScenes.ClassicPathsFor(PresetKind.Base));
    }

    [Fact]
    public void DayAndNightEachReplaceTwoKeysWithOne()
    {
        Assert.Equal(new[] { "Assets/Content/Locations/Factory/Factory_Day.unity" }, FactoryScenes.ClassicPathsFor(PresetKind.Day));
        Assert.Equal(new[] { "Assets/Content/Locations/Factory/Factory_Night.unity" }, FactoryScenes.ClassicPathsFor(PresetKind.Night));
    }

    // The classic preset has no culling child at all, so its key list is emptied rather than rewritten.
    [Fact]
    public void CullingBecomesEmpty()
    {
        Assert.Empty(FactoryScenes.ClassicPathsFor(PresetKind.Culling));
    }

    // The swap replaces a list rather than renaming entries, so every classic list must be no longer
    // than the shipped one it replaces. Otherwise a key object would have to be constructed.
    [Fact]
    public void EveryClassicListFitsInsideTheShippedOneItReplaces()
    {
        Assert.True(FactoryScenes.ClassicPathsFor(PresetKind.Day).Count <= ShippedDay.Length);
        Assert.True(FactoryScenes.ClassicPathsFor(PresetKind.Night).Count <= ShippedNight.Length);
        Assert.True(FactoryScenes.ClassicPathsFor(PresetKind.Base).Count <= ShippedBase.Length);
        Assert.True(FactoryScenes.ClassicPathsFor(PresetKind.Culling).Count <= ShippedDayCulling.Length);
    }

    [Fact]
    public void ClassicPathsAreRecognisedAsClassic()
    {
        Assert.True(FactoryScenes.IsClassicPath("Assets/Content/Locations/Factory/Factory.unity"));
        Assert.False(FactoryScenes.IsClassicPath(null));
    }

    // Factory_Rework_* paths start with the classic prefix as a string, so a naive StartsWith on
    // "Assets/Content/Locations/Factory" would classify every current path as classic.
    [Fact]
    public void CurrentPathsAreNotMistakenForClassic()
    {
        foreach (var path in ShippedBase) Assert.False(FactoryScenes.IsClassicPath(path));
    }

    [Theory]
    [InlineData("Factory", true)]
    [InlineData("Factory_Sound", true)]
    [InlineData("Factory_Rework_Areas", true)]
    [InlineData("Factory_Sound_Rework", true)]
    [InlineData("Shopping_Mall_AI", false)]
    [InlineData("custom_factoryStorageZone", false)]
    [InlineData("Custom_ChemicalFactory", false)]
    [InlineData("Custom_Construction_Factory", false)]
    [InlineData(null, false)]
    public void FactorySceneNamesAreRecognisedWithoutClaimingOtherMaps(string name, bool expected)
    {
        Assert.Equal(expected, FactoryScenes.IsFactoryScene(name));
    }

    [Fact]
    public void BothFactoryLocationIdsAreCovered()
    {
        Assert.Equal(new[] { "factory4_day", "factory4_night" }, FactoryScenes.ServerNames);
    }
}
