using System.Reflection;
using HaCreator.MapSimulator.Pools;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class AffectedAreaParityTests
{
    [Fact]
    public void ResolveAreaBuffItemDurationMs_UsesInfoTimeSeconds()
    {
        var itemProperty = new WzSubProperty("05010079");
        var infoProperty = new WzSubProperty("info");
        infoProperty.WzProperties.Add(new WzIntProperty("time", 12));
        itemProperty.WzProperties.Add(infoProperty);

        MethodInfo method = typeof(AffectedAreaPool).GetMethod(
            "ResolveAreaBuffItemDurationMs",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        int durationMs = (int)method.Invoke(null, new object[] { itemProperty, 5010079 });

        Assert.Equal(12000, durationMs);
    }

    [Fact]
    public void ResolveAreaBuffItemDurationMs_FallsBackToZeroWithoutMetadata()
    {
        var itemProperty = new WzSubProperty("05010079");

        MethodInfo method = typeof(AffectedAreaPool).GetMethod(
            "ResolveAreaBuffItemDurationMs",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        int durationMs = (int)method.Invoke(null, new object[] { itemProperty, 5010079 });

        Assert.Equal(0, durationMs);
    }
}
