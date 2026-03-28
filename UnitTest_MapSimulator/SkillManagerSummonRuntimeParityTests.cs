using System;
using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public class SkillManagerSummonRuntimeParityTests
{
    [Fact]
    public void TryDamageSummonByObjectId_UsesSummonHealthBeforeQueueingRemoval()
    {
        SkillManager manager = CreateSkillManager();
        List<ActiveSummon> summons = GetPrivateList<ActiveSummon>(manager, "_summons");
        ActiveSummon summon = new()
        {
            ObjectId = 77,
            SkillId = 33111003,
            StartTime = 1000,
            Duration = 30000,
            SkillData = new SkillData { SkillId = 33111003 },
            LevelData = new SkillLevelData { HP = 3 },
            MaxHealth = 3,
            CurrentHealth = 3
        };
        summons.Add(summon);

        Assert.True(manager.TryDamageSummonByObjectId(77, 1, 1500));
        Assert.Single(summons);
        Assert.Equal(2, summon.CurrentHealth);
        Assert.False(summon.IsPendingRemoval);
        Assert.Equal(SummonActorState.Hit, summon.ActorState);

        Assert.True(manager.TryDamageSummonByObjectId(77, 2, 1600));
        Assert.Single(summons);
        Assert.Equal(0, summon.CurrentHealth);
        Assert.True(summon.IsPendingRemoval);
    }

    [Fact]
    public void UpdateSummonActorStateAfterMovement_PrefersSummonHitAnimationWhileActive()
    {
        SkillManager manager = CreateSkillManager();
        ActiveSummon summon = new()
        {
            SkillId = 35111002,
            StartTime = 1000,
            SkillData = new SkillData
            {
                SkillId = 35111002,
                SummonHitAnimation = new SkillAnimation
                {
                    Frames = new List<SkillFrame> { new() { Delay = 120 } }
                }
            },
            LastHitAnimationStartTime = 1500
        };

        InvokePrivate(manager, "UpdateSummonActorStateAfterMovement", summon, 1550);

        Assert.Equal(SummonActorState.Hit, summon.ActorState);
    }

    private static SkillManager CreateSkillManager()
    {
        SkillLoader loader = new(null, null, null);
        PlayerCharacter player = new((GraphicsDevice)null, texturePool: null, build: null);
        player.SetPosition(0f, 0f);
        return new SkillManager(loader, player);
    }

    private static List<T> GetPrivateList<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<T>>(field.GetValue(instance));
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(instance, args);
    }
}
