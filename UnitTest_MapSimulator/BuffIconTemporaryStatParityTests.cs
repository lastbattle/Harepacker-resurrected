using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public class BuffIconTemporaryStatParityTests
{
    private static readonly MethodInfo CreateLevelDataMethod =
        typeof(SkillLoader).GetMethod(
            "CreateLevelData",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find SkillLoader.CreateLevelData.");

    private static readonly MethodInfo GetBuffTemporaryStatPresentationMethod =
        typeof(SkillManager).GetMethod(
            "GetBuffTemporaryStatPresentation",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find SkillManager.GetBuffTemporaryStatPresentation.");

    [Fact]
    public void CreateLevelData_ParsesRateBackedBuffFamiliesFromAuthoredSkillFields()
    {
        SkillData skill = new()
        {
            SkillId = 5111007,
            Name = "Dice",
            Description = "Rolls a die."
        };

        WzSubProperty commonNode = new("common");
        commonNode.WzProperties.Add(new WzStringProperty("expR", "30"));
        commonNode.WzProperties.Add(new WzStringProperty("dropR", "u(x/2)"));
        commonNode.WzProperties.Add(new WzStringProperty("mesoR", "2*x"));

        SkillLevelData levelData = CreateLevelData(skill, commonNode, level: 6);

        Assert.Equal(30, levelData.ExperienceRate);
        Assert.Equal(3, levelData.DropRate);
        Assert.Equal(12, levelData.MesoRate);
    }

    [Fact]
    public void TemporaryStatPresentation_UsesDirectRatePayloadsWithoutTextFallbacks()
    {
        ActiveBuff buff = new()
        {
            SkillId = 5111007,
            Level = 1,
            StartTime = 0,
            Duration = 30000,
            SkillData = new SkillData
            {
                SkillId = 5111007,
                Name = "Dice",
                Description = "Rolls a die."
            },
            LevelData = new SkillLevelData
            {
                ExperienceRate = 30,
                DropRate = 3,
                MesoRate = 12
            }
        };

        string[] labels = GetTemporaryStatLabels(buff);

        Assert.Contains("ExperienceRate", labels);
        Assert.Contains("DropRate", labels);
        Assert.Contains("MesoRate", labels);
    }

    private static SkillLevelData CreateLevelData(SkillData skill, WzImageProperty node, int level)
    {
        return (SkillLevelData)(CreateLevelDataMethod.Invoke(null, new object[] { skill, node, level })
            ?? throw new InvalidOperationException("SkillLoader.CreateLevelData returned null."));
    }

    private static string[] GetTemporaryStatLabels(ActiveBuff buff)
    {
        var result = GetBuffTemporaryStatPresentationMethod.Invoke(null, new object[] { buff }) as IEnumerable;
        Assert.NotNull(result);

        List<string> labels = new();
        foreach (object item in result)
        {
            Assert.NotNull(item);
            PropertyInfo labelProperty = item.GetType().GetProperty("Label", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Missing temporary stat Label property.");
            labels.Add((string)(labelProperty.GetValue(item) ?? string.Empty));
        }

        return labels.ToArray();
    }
}
