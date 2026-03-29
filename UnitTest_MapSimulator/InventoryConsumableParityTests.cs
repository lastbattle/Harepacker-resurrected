using System;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class InventoryConsumableParityTests
{
    private static readonly Type ConsumableItemEffectType =
        typeof(HaCreator.MapSimulator.MapSimulator).GetNestedType("ConsumableItemEffect", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find MapSimulator.ConsumableItemEffect.");

    private static readonly MethodInfo CreateConsumableBuffLevelDataMethod =
        typeof(HaCreator.MapSimulator.MapSimulator).GetMethod(
            "CreateConsumableBuffLevelData",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find MapSimulator.CreateConsumableBuffLevelData.");

    [Fact]
    public void CreateConsumableBuffLevelData_PreservesClientStyleIndieMaxVitalBonuses()
    {
        object effect = Activator.CreateInstance(ConsumableItemEffectType, nonPublic: true)
            ?? throw new InvalidOperationException("Could not create ConsumableItemEffect.");

        SetInitOnlyProperty(effect, "DurationMs", 3600_000);
        SetInitOnlyProperty(effect, "IndieMaxHp", 500);
        SetInitOnlyProperty(effect, "IndieMaxMp", 250);
        SetInitOnlyProperty(effect, "MaxHpPercent", 50);
        SetInitOnlyProperty(effect, "MaxMpPercent", 25);

        var levelData = (SkillLevelData?)CreateConsumableBuffLevelDataMethod.Invoke(null, new[] { effect });

        Assert.NotNull(levelData);
        Assert.Equal(3600, levelData!.Time);
        Assert.Equal(500, levelData.IndieMaxHP);
        Assert.Equal(250, levelData.IndieMaxMP);
        Assert.Equal(50, levelData.MaxHPPercent);
        Assert.Equal(25, levelData.MaxMPPercent);
    }

    private static void SetInitOnlyProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Could not find property '{propertyName}'.");
        property.SetValue(instance, value);
    }
}
