using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public sealed class AbilityStatParityTests
{
    [Fact]
    public void PassiveAccuracyAndAvoidabilityAliases_ContributeToTotals()
    {
        CharacterBuild build = new()
        {
            Accuracy = 3,
            Avoidability = 4,
            DEX = 10,
            LUK = 10
        };

        SkillManager manager = CreateManager(build,
            CreatePassiveSkill(2310008, mastery: 65, accuracy: 7),
            CreatePassiveSkill(32120001, avoidability: 9));

        manager.SetSkillLevel(2310008, 1);
        manager.SetSkillLevel(32120001, 1);

        build.SkillStatBonusProvider = manager.GetTotalPassiveBonus;
        build.SkillMasteryProvider = manager.GetMastery;

        Assert.Equal(23, build.TotalAccuracy);
        Assert.Equal(18, build.TotalAvoidability);
    }

    [Fact]
    public void AdvancedChargeMastery_RequiresLiveChargeBuff()
    {
        CharacterBuild build = CreateWeaponBuild(1302000);
        SkillData advancedCharge = CreatePassiveSkill(1220010, mastery: 70);

        SkillManager manager = CreateManager(build, advancedCharge);
        manager.SetSkillLevel(1220010, 1);

        Assert.Equal(10, manager.GetMastery());

        AddBuff(manager, CreateBuffSkill(1211002, "Charge"));

        Assert.Equal(70, manager.GetMastery());
    }

    [Fact]
    public void StringlessMasterySkill_FallsBackToKnownWeaponFamily()
    {
        CharacterBuild build = CreateWeaponBuild(1492000);
        SkillData masterySkill = CreatePassiveSkill(5700000, mastery: 30);

        SkillManager manager = CreateManager(build, masterySkill);
        manager.SetSkillLevel(5700000, 1);

        Assert.Equal(30, manager.GetMastery());
    }

    private static SkillManager CreateManager(CharacterBuild build, params SkillData[] skills)
    {
        PlayerCharacter player = new((GraphicsDevice)null, (TexturePool)null, null);
        SetNonPublicProperty(player, nameof(PlayerCharacter.Build), build);
        player.SetPosition(0f, 0f);

        SkillLoader loader = new(null, null, null);
        SkillManager manager = new(loader, player);
        SetPrivateField(manager, "_availableSkills", new List<SkillData>(skills));
        return manager;
    }

    private static CharacterBuild CreateWeaponBuild(int weaponItemId)
    {
        CharacterBuild build = new();
        build.Equipment[EquipSlot.Weapon] = new WeaponPart
        {
            ItemId = weaponItemId,
            Attack = 10
        };
        return build;
    }

    private static SkillData CreatePassiveSkill(int skillId, int mastery = 0, int accuracy = 0, int avoidability = 0)
    {
        return new SkillData
        {
            SkillId = skillId,
            IsPassive = true,
            Levels = new Dictionary<int, SkillLevelData>
            {
                [1] = new()
                {
                    Level = 1,
                    Mastery = mastery,
                    ACC = accuracy,
                    EVA = avoidability
                }
            }
        };
    }

    private static SkillData CreateBuffSkill(int skillId, string name)
    {
        return new SkillData
        {
            SkillId = skillId,
            Name = name,
            IsBuff = true,
            Levels = new Dictionary<int, SkillLevelData>
            {
                [1] = new()
                {
                    Level = 1,
                    Time = 30
                }
            }
        };
    }

    private static void AddBuff(SkillManager manager, SkillData skill)
    {
        List<ActiveBuff> buffs = GetPrivateField<List<ActiveBuff>>(manager, "_buffs");
        buffs.Add(new ActiveBuff
        {
            SkillId = skill.SkillId,
            Level = 1,
            SkillData = skill,
            LevelData = skill.Levels[1],
            Duration = 30000
        });
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(instance);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private static void SetNonPublicProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property.SetValue(instance, value);
    }
}
