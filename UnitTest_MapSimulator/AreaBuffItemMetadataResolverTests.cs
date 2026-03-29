using HaCreator.MapSimulator.Pools;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class AreaBuffItemMetadataResolverTests
{
    [Fact]
    public void ResolveDurationMs_UsesDirectInfoTimeWhenPresent()
    {
        WzSubProperty itemProperty = new("05010079")
        {
            WzProperties =
            {
                new WzSubProperty("info")
                {
                    WzProperties =
                    {
                        new WzIntProperty("time", 12)
                    }
                }
            }
        };

        int durationMs = AreaBuffItemMetadataResolver.ResolveDurationMs(itemProperty);

        Assert.Equal(12_000, durationMs);
    }

    [Fact]
    public void ResolveDurationMs_UsesLinkedItemPathWhenDirectMetadataIsMissing()
    {
        WzSubProperty itemProperty = new("05010079")
        {
            WzProperties =
            {
                new WzSubProperty("info")
                {
                    WzProperties =
                    {
                        new WzStringProperty("path", "Cash/0501.img/05010080")
                    }
                }
            }
        };

        WzSubProperty linkedItemProperty = new("05010080")
        {
            WzProperties =
            {
                new WzSubProperty("info")
                {
                    WzProperties =
                    {
                        new WzIntProperty("time", 18)
                    }
                }
            }
        };

        int durationMs = AreaBuffItemMetadataResolver.ResolveDurationMs(
            itemProperty,
            linkedItemPropertyLoader: path => path == "Cash/0501.img/05010080" ? linkedItemProperty : null);

        Assert.Equal(18_000, durationMs);
    }

    [Theory]
    [InlineData("Summons a dark fog for 15 sec.", 15_000)]
    [InlineData("Creates a fog for 2 minutes.", 120_000)]
    public void ResolveDurationMs_ParsesHumanReadableDescriptionFallback(string itemDescription, int expectedDurationMs)
    {
        int durationMs = AreaBuffItemMetadataResolver.ResolveDurationMs(itemProperty: null, itemDescription: itemDescription);

        Assert.Equal(expectedDurationMs, durationMs);
    }
}
