using System.Collections;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class SkillManagerBuffParityTests
{
    private static readonly MethodInfo GetBuffTemporaryStatPresentationMethod =
        typeof(SkillManager).GetMethod("GetBuffTemporaryStatPresentation", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SkillManager.GetBuffTemporaryStatPresentation was not found.");

    [Fact]
    public void GetBuffTemporaryStatPresentation_TracksEnhancedMadPayload()
    {
        var labels = GetTemporaryStatLabels(new ActiveBuff
        {
            SkillId = 1,
            SkillData = new SkillData
            {
                SkillId = 1,
                Name = "Test Buff"
            },
            LevelData = new SkillLevelData
            {
                Level = 1,
                EnhancedMAD = 15
            }
        });

        Assert.Contains("MAD", labels);
    }

    [Fact]
    public void GetBuffTemporaryStatPresentation_NormalizesSparseAbbreviationText()
    {
        var labels = GetTemporaryStatLabels(new ActiveBuff
        {
            SkillId = 2,
            SkillData = new SkillData
            {
                SkillId = 2,
                Name = "Abbreviation Buff",
                Description = "P. ATK, M. ATT, ACC, MaxHP, Crit DMG, abnormal status resistance"
            },
            LevelData = new SkillLevelData
            {
                Level = 1
            }
        });

        Assert.Contains("PAD", labels);
        Assert.Contains("MAD", labels);
        Assert.Contains("ACC", labels);
        Assert.Contains("MaxHP", labels);
        Assert.Contains("CriticalDamage", labels);
        Assert.Contains("DebuffResistance", labels);
    }

    [Fact]
    public void GetBuffTemporaryStatPresentation_HasteAddsSpeedAndJumpFamilies()
    {
        var labels = GetTemporaryStatLabels(new ActiveBuff
        {
            SkillId = 3,
            SkillData = new SkillData
            {
                SkillId = 3,
                Name = "Haste",
                Description = "Increases movement speed."
            },
            LevelData = new SkillLevelData
            {
                Level = 1
            }
        });

        Assert.Contains("Speed", labels);
        Assert.Contains("Jump", labels);
    }

    private static IReadOnlyList<string> GetTemporaryStatLabels(ActiveBuff buff)
    {
        object? rawResult = GetBuffTemporaryStatPresentationMethod.Invoke(null, [buff]);
        Assert.NotNull(rawResult);

        var labels = new List<string>();
        foreach (object? presentation in Assert.IsAssignableFrom<IEnumerable>(rawResult))
        {
            Assert.NotNull(presentation);
            PropertyInfo? labelProperty = presentation.GetType().GetProperty("Label", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(labelProperty);

            string? label = labelProperty.GetValue(presentation) as string;
            Assert.False(string.IsNullOrWhiteSpace(label));
            labels.Add(label!);
        }

        return labels;
    }
}
