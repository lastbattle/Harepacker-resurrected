using System;
using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public sealed class SkillManagerQueuedFollowUpParityTests
{
    [Fact]
    public void QueuedFinalAttack_Cancels_WhenFollowUpSkillIsNoLongerLearned()
    {
        const int triggerSkillId = 100000;
        const int followUpSkillId = 100100;

        SkillData followUpSkill = CreateAttackSkill(followUpSkillId, SkillAttackType.Melee);
        followUpSkill.Levels[1].Prop = 100;

        SkillManager manager = CreateManager(CreateWeaponBuild(1302000), followUpSkill);
        manager.SetSkillLevel(followUpSkillId, 1);

        SkillData triggerSkill = new()
        {
            SkillId = triggerSkillId,
            FinalAttackTriggers = new Dictionary<int, HashSet<int>>
            {
                [followUpSkillId] = new() { 30 }
            }
        };

        int castCount = 0;
        manager.OnSkillCast += _ => castCount++;

        InvokePrivate(manager, "TryQueueFollowUpAttack", triggerSkill, 0, null, true);
        manager.SetSkillLevel(followUpSkillId, 0);

        InvokePrivate(manager, "ProcessQueuedFollowUpAttacks", 200);

        Assert.Equal(0, castCount);
    }

    [Fact]
    public void DeferredMovingShootFollowUp_RevalidatesLiveSkillLevelAtFireTime()
    {
        const int skillId = 200200;

        SkillData movingProjectileSkill = CreateAttackSkill(skillId, SkillAttackType.Ranged);
        movingProjectileSkill.CasterMove = true;
        movingProjectileSkill.Projectile = new ProjectileData
        {
            SkillId = skillId,
            Animation = new SkillAnimation()
        };
        movingProjectileSkill.Levels[1].BulletCount = 1;
        movingProjectileSkill.Levels[2] = new SkillLevelData
        {
            Level = 2,
            Damage = 100,
            AttackCount = 1,
            MobCount = 1,
            BulletCount = 3
        };

        SkillManager manager = CreateManager(new CharacterBuild(), movingProjectileSkill);
        manager.SetSkillLevel(skillId, 1);

        InvokePrivate(
            manager,
            "ExecuteSkillPayload",
            movingProjectileSkill,
            1,
            0,
            false,
            null,
            true,
            true,
            null,
            null);

        manager.SetSkillLevel(skillId, 2);
        InvokePrivate(manager, "ProcessDeferredSkillPayloads", 500);

        List<ActiveProjectile> projectiles = GetPrivateField<List<ActiveProjectile>>(manager, "_projectiles");
        Assert.Equal(3, projectiles.Count);
        Assert.All(projectiles, projectile => Assert.Equal(2, projectile.SkillLevel));
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

    private static SkillData CreateAttackSkill(int skillId, SkillAttackType attackType)
    {
        return new SkillData
        {
            SkillId = skillId,
            MaxLevel = 2,
            IsAttack = true,
            AttackType = attackType,
            Levels = new Dictionary<int, SkillLevelData>
            {
                [1] = new()
                {
                    Level = 1,
                    Damage = 100,
                    AttackCount = 1,
                    MobCount = 1,
                    BulletCount = 1
                }
            }
        };
    }

    private static object InvokePrivate(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(instance, arguments);
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
