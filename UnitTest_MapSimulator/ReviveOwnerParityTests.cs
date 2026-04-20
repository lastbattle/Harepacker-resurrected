using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class ReviveOwnerParityTests
{
    [Fact]
    public void ResolveReviveOwnerVariant_MirrorsRecoveredBranchPriority()
    {
        ReviveOwnerVariant soulStoneFirst = MapSimulator.ResolveReviveOwnerVariant(
            hasSoulStone: true,
            premiumSafetyCharmCount: 1,
            safetyCharmCount: 1,
            wheelOfFortuneCount: 1,
            canUsePremiumCurrentFieldRecovery: true,
            canUseUpgradeTombRevive: true);
        Assert.Equal(ReviveOwnerVariant.SoulStoneChoice, soulStoneFirst);

        ReviveOwnerVariant wheelBeforePremium = MapSimulator.ResolveReviveOwnerVariant(
            hasSoulStone: false,
            premiumSafetyCharmCount: 1,
            safetyCharmCount: 1,
            wheelOfFortuneCount: 1,
            canUsePremiumCurrentFieldRecovery: true,
            canUseUpgradeTombRevive: true);
        Assert.Equal(ReviveOwnerVariant.UpgradeTombChoice, wheelBeforePremium);

        ReviveOwnerVariant premiumBeforeSafety = MapSimulator.ResolveReviveOwnerVariant(
            hasSoulStone: false,
            premiumSafetyCharmCount: 1,
            safetyCharmCount: 1,
            wheelOfFortuneCount: 0,
            canUsePremiumCurrentFieldRecovery: true,
            canUseUpgradeTombRevive: false);
        Assert.Equal(ReviveOwnerVariant.PremiumSafetyCharmChoice, premiumBeforeSafety);

        ReviveOwnerVariant safetyFallback = MapSimulator.ResolveReviveOwnerVariant(
            hasSoulStone: false,
            premiumSafetyCharmCount: 0,
            safetyCharmCount: 1,
            wheelOfFortuneCount: 0,
            canUsePremiumCurrentFieldRecovery: true,
            canUseUpgradeTombRevive: false);
        Assert.Equal(ReviveOwnerVariant.SafetyCharmChoice, safetyFallback);
    }

    [Fact]
    public void ReviveOwnerRuntime_UpdateOnlyAutoResolvesAfterTimeoutBoundary()
    {
        const int openTick = 12_345;
        ReviveOwnerRuntime runtime = new();
        runtime.Open("test", "normal", "premium", ReviveOwnerVariant.DefaultOnly, openTick);

        ReviveOwnerResolution atBoundary = runtime.Update(openTick + ReviveOwnerRuntime.AutoResolveMs);
        Assert.False(atBoundary.Handled);
        Assert.True(runtime.IsOpen);

        ReviveOwnerResolution afterBoundary = runtime.Update(openTick + ReviveOwnerRuntime.AutoResolveMs + 1);
        Assert.True(afterBoundary.Handled);
        Assert.True(afterBoundary.TimedOut);
        Assert.False(afterBoundary.Premium);
        Assert.False(runtime.IsOpen);
    }

    [Fact]
    public void TryBuildReviveOwnerTransferFieldPayload_MatchesOpcode41Shape()
    {
        Assert.True(MapSimulator.TryBuildReviveOwnerTransferFieldPayload(premium: false, out byte[] normalPayload));
        Assert.True(MapSimulator.TryBuildReviveOwnerTransferFieldPayload(premium: true, out byte[] premiumPayload));

        Assert.Equal(10, normalPayload.Length);
        Assert.Equal(10, premiumPayload.Length);
        Assert.Equal("00000000000000000000", Convert.ToHexString(normalPayload));
        Assert.Equal("00000000000000000100", Convert.ToHexString(premiumPayload));
    }

    [Fact]
    public void ReviveOwnerRuntime_SingleButtonBranchUsesRecoveredPlus42Offset()
    {
        ReviveOwnerNativeBranchSpec defaultBranchSpec = ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.DefaultOnly);
        Assert.False(defaultBranchSpec.HasNoButton);
        Assert.Equal(42, defaultBranchSpec.YesButtonOffsetX);

        ReviveOwnerNativeBranchSpec premiumBranchSpec = ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.PremiumSafetyCharmChoice);
        Assert.True(premiumBranchSpec.HasNoButton);
        Assert.Equal(0, premiumBranchSpec.YesButtonOffsetX);
    }

    [Fact]
    public void TryGetReviveOwnerMapInfoFlag_ReadsCaseInsensitiveBoolStringsFromImageInfo()
    {
        MapInfo mapInfo = CreateMapInfoWithInfoProperties(
            new WzStringProperty("noResurrection", "YeS"),
            new WzStringProperty("reviveCurField", "off"));

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoFlag(mapInfo, "noResurection", out bool noResurrection));
        Assert.True(noResurrection);

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoFlag(mapInfo, "reviveCurField", out bool reviveCurField));
        Assert.False(reviveCurField);
    }

    [Fact]
    public void TryGetReviveOwnerMapInfoFlag_ReadsAdditionalAndUnsupportedBuckets()
    {
        MapInfo additionalMapInfo = new();
        additionalMapInfo.additionalProps.Add(new WzStringProperty("ReviveCurFieldOfNoTransfer", "enabled"));

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoFlag(additionalMapInfo, "reviveCurField", out bool additionalFlag));
        Assert.True(additionalFlag);

        MapInfo unsupportedMapInfo = new();
        WzSubProperty nestedInfo = new("info");
        nestedInfo.AddProperty(new WzStringProperty("forceReturnOnDead", "1"));
        unsupportedMapInfo.unsupportedInfoProperties.Add(nestedInfo);

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoFlag(unsupportedMapInfo, "forceReturnOnDead", out bool unsupportedFlag));
        Assert.True(unsupportedFlag);
    }

    [Fact]
    public void TryGetReviveOwnerMapInfoPoint_AcceptsVectorAndXYSubProperty()
    {
        MapInfo vectorMapInfo = new();
        vectorMapInfo.additionalProps.Add(new WzVectorProperty("ReviveCurFieldOfNoTransferPoint", 111, 222));

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoPoint(
            vectorMapInfo,
            "ReviveCurFieldOfNoTransferPoint",
            out Vector2 vectorPoint));
        Assert.Equal(new Vector2(111, 222), vectorPoint);

        MapInfo xyMapInfo = new();
        WzSubProperty pointContainer = new("ReviveCurFieldOfNoTransferPoint");
        pointContainer.AddProperty(new WzStringProperty("x", "-45"));
        pointContainer.AddProperty(new WzStringProperty("Y", "78"));
        xyMapInfo.unsupportedInfoProperties.Add(pointContainer);

        Assert.True(MapSimulator.TryGetReviveOwnerMapInfoPoint(
            xyMapInfo,
            "ReviveCurFieldOfNoTransferPoint",
            out Vector2 xyPoint));
        Assert.Equal(new Vector2(-45, 78), xyPoint);
    }

    [Fact]
    public void ResolveCurrentFieldReviveRespawnPointWithSource_TracksAuthoredSpawnAndFallback()
    {
        MapInfo authoredPointMapInfo = new();
        authoredPointMapInfo.additionalProps.Add(new WzVectorProperty("ReviveCurFieldOfNoTransferPoint", 14, 29));

        ReviveOwnerRespawnPointResolution authored = MapSimulator.ResolveCurrentFieldReviveRespawnPointWithSource(
            authoredPointMapInfo,
            spawnPoint: new Vector2(500, 500),
            fallbackPoint: new Vector2(700, 700));
        Assert.Equal(ReviveOwnerRespawnPointSource.AuthoredCurrentFieldPoint, authored.Source);
        Assert.Equal(new Vector2(14, 29), authored.Point);

        MapInfo spawnApproximationMapInfo = new()
        {
            id = 910000000,
            forcedReturn = 910000000
        };
        ReviveOwnerRespawnPointResolution spawnApprox = MapSimulator.ResolveCurrentFieldReviveRespawnPointWithSource(
            spawnApproximationMapInfo,
            spawnPoint: new Vector2(123, 456),
            fallbackPoint: new Vector2(999, 888));
        Assert.Equal(ReviveOwnerRespawnPointSource.SpawnApproximation, spawnApprox.Source);
        Assert.Equal(new Vector2(123, 456), spawnApprox.Point);

        MapInfo forcedReturnMapInfo = CreateMapInfoWithInfoProperties(new WzIntProperty("forceReturnOnDead", 1));
        ReviveOwnerRespawnPointResolution fallback = MapSimulator.ResolveCurrentFieldReviveRespawnPointWithSource(
            forcedReturnMapInfo,
            spawnPoint: new Vector2(101, 202),
            fallbackPoint: new Vector2(303, 404));
        Assert.Equal(ReviveOwnerRespawnPointSource.DeathPoint, fallback.Source);
        Assert.Equal(new Vector2(303, 404), fallback.Point);
    }

    private static MapInfo CreateMapInfoWithInfoProperties(params WzImageProperty[] infoProperties)
    {
        MapInfo mapInfo = new();
        WzImage image = new("test.img");
        WzSubProperty info = new("info");
        foreach (WzImageProperty property in infoProperties)
        {
            info.AddProperty(property);
        }

        image.AddProperty(info);
        mapInfo.Image = image;
        return mapInfo;
    }
}
