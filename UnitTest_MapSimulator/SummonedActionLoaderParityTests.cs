using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class SummonedActionLoaderParityTests
{
    [Fact]
    public void ResolveClientSummonedUolPaths_HarvestsReqChildNameSkillIds()
    {
        WzSubProperty skillNode = BuildSkillNode(4341006);
        WzSubProperty reqNode = AddSubProperty(skillNode, "req");
        AddSubProperty(reqNode, "4331002");

        var resolvedPaths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode: null);

        Assert.Contains("Skill/433.img/skill/4331002", resolvedPaths);
    }

    [Fact]
    public void ResolveClientSummonedUolPaths_HarvestsNestedInfoOwnerBranchChildNames()
    {
        WzSubProperty skillNode = BuildSkillNode(35121006);
        WzSubProperty infoNode = AddSubProperty(skillNode, "info");
        WzSubProperty hidden = AddSubProperty(infoNode, "hiddenOwner");
        WzSubProperty reqLike = AddSubProperty(hidden, "reqLinks");
        AddSubProperty(reqLike, "35111009");

        var resolvedPaths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode);

        Assert.Contains("Skill/3511.img/skill/35111009", resolvedPaths);
    }

    [Fact]
    public void ResolveClientSummonedUolPaths_DoesNotHarvestNonOwnerBranchChildNames()
    {
        WzSubProperty skillNode = BuildSkillNode(35121006);
        WzSubProperty infoNode = AddSubProperty(skillNode, "info");
        WzSubProperty notes = AddSubProperty(infoNode, "notes");
        WzSubProperty links = AddSubProperty(notes, "links");
        AddSubProperty(links, "35111009");

        var resolvedPaths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode);

        Assert.DoesNotContain("Skill/3511.img/skill/35111009", resolvedPaths);
    }

    private static WzSubProperty BuildSkillNode(int skillId)
    {
        return new WzSubProperty(skillId.ToString(CultureInfo.InvariantCulture));
    }

    private static WzSubProperty AddSubProperty(WzSubProperty parent, string name)
    {
        var child = new WzSubProperty(name);
        parent.AddProperty(child);
        return child;
    }
}
