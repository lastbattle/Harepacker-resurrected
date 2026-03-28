using System.Linq;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class FieldObjectNpcScriptNameResolverTests
{
    [Fact]
    public void ResolvePublishedScriptNames_FlattensNestedNpcScriptPropertiesAndDeduplicates()
    {
        WzSubProperty property = new("script");

        WzSubProperty firstEntry = new("0");
        firstEntry.AddProperty(new WzStringProperty("script", "mTaxi"));
        property.AddProperty(firstEntry);

        WzSubProperty secondEntry = new("1");
        secondEntry.AddProperty(new WzStringProperty("script", "market00"));

        WzSubProperty nested = new("nested");
        nested.AddProperty(new WzStringProperty("0", "mTaxi"));
        secondEntry.AddProperty(nested);
        property.AddProperty(secondEntry);

        IReadOnlyList<string> names = FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(property);

        Assert.Equal(new[] { "mTaxi", "market00" }, names.OrderBy(static name => name).ToArray());
    }

    [Fact]
    public void ResolvePublishedScriptNames_ReturnsEmptyWhenNpcHasNoScriptProperty()
    {
        IReadOnlyList<string> names = FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames((WzImageProperty)null);

        Assert.Empty(names);
    }
}
