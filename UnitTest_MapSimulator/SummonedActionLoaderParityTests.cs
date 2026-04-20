using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class SummonedActionLoaderParityTests
{
    [Fact]
    public void ResolveClientSummonedUolPathsForTest_HarvestsNestedSkillStringLeaf()
    {
        WzSubProperty skillNode = CreateMountedSkillNode(35101005);
        WzSubProperty ownerNode = EnsureSubPropertyPath(skillNode, "wrapper", "owner");
        ownerNode.AddProperty(new WzStringProperty("hiddenSummonPath", "Skill/501.img/skill/5010005/summon/attack1"));

        IReadOnlyList<string> paths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode: null);

        Assert.Contains("Skill/501.img/skill/5010005/summon/attack1", paths);
    }

    [Fact]
    public void ResolveClientSummonedUolPathsForTest_HarvestsNestedSkillUolLeaf()
    {
        WzSubProperty skillNode = CreateMountedSkillNode(4341006);
        WzSubProperty ownerNode = EnsureSubPropertyPath(skillNode, "wrapper", "candidate");
        ownerNode.AddProperty(new WzUOLProperty("pathRef", "wz/Skill/3512.img/skill/35121006/summon/repeat"));

        IReadOnlyList<string> paths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode: null);

        Assert.Contains("Skill/3512.img/skill/35121006/summon/repeat", paths);
    }

    [Fact]
    public void ResolveClientSummonedUolPathsForTest_DoesNotTraverseBeyondBoundedSkillDepth()
    {
        WzSubProperty skillNode = CreateMountedSkillNode(35111011);
        WzSubProperty deepNode = EnsureSubPropertyPath(skillNode, "a", "b", "c");
        deepNode.AddProperty(new WzStringProperty("tooDeep", "Skill/1311.img/skill/13111004/summon/hit"));

        IReadOnlyList<string> paths = SkillLoader.ResolveClientSummonedUolPathsForTest(skillNode, infoNode: null);

        Assert.DoesNotContain("Skill/1311.img/skill/13111004/summon/hit", paths);
    }

    private static WzSubProperty CreateMountedSkillNode(int skillId)
    {
        var skillRoot = new WzSubProperty("Skill");
        var imageNode = new WzSubProperty($"{skillId / 10000}.img");
        var skillContainer = new WzSubProperty("skill");
        var skillNode = new WzSubProperty(skillId.ToString());

        skillRoot.AddProperty(imageNode);
        imageNode.AddProperty(skillContainer);
        skillContainer.AddProperty(skillNode);
        return skillNode;
    }

    private static WzSubProperty EnsureSubPropertyPath(WzSubProperty root, params string[] segments)
    {
        WzSubProperty current = root;
        foreach (string segment in segments)
        {
            if (current[segment] is not WzSubProperty child)
            {
                child = new WzSubProperty(segment);
                current.AddProperty(child);
            }

            current = child;
        }

        return current;
    }
}
