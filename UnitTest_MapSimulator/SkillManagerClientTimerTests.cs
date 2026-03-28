using System;
using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public class SkillManagerClientTimerTests
{
    [Fact]
    public void Update_ExpiresSkillZoneThroughClientTimerSeam()
    {
        SkillManager manager = CreateSkillManager();
        SkillData skill = new()
        {
            SkillId = 4221006,
            ZoneType = "invincible"
        };
        SkillLevelData levelData = new()
        {
            Level = 1,
            Time = 4
        };

        int expiredSkillId = 0;
        string expiredSource = null;
        manager.OnClientSkillTimerExpired = (skillId, source) =>
        {
            expiredSkillId = skillId;
            expiredSource = source;
        };

        InvokePrivate(
            manager,
            "StartSkillZone",
            skill,
            1,
            levelData,
            1000,
            new Rectangle(0, 0, 120, 60));

        Assert.Single(GetPrivateList<ActiveSkillZone>(manager, "_skillZones"));

        manager.Update(5000, 0f);

        Assert.Empty(GetPrivateList<ActiveSkillZone>(manager, "_skillZones"));
        Assert.Equal(skill.SkillId, expiredSkillId);
        Assert.Equal("skill-zone-expire", expiredSource);
    }

    [Fact]
    public void RequestClientSkillCancel_RemovesMatchingActiveSummons()
    {
        SkillManager manager = CreateSkillManager();
        List<ActiveSummon> summons = GetPrivateList<ActiveSummon>(manager, "_summons");
        summons.Add(new ActiveSummon
        {
            ObjectId = 1,
            SkillId = 35111002,
            StartTime = 1000,
            Duration = 30000
        });

        bool canceled = manager.RequestClientSkillCancel(35111002, 1500);

        Assert.True(canceled);
        Assert.Empty(summons);
    }

    [Fact]
    public void RepeatSkillModeEndAck_CompletesThroughClientTimerUpdate()
    {
        SkillManager manager = CreateSkillManager();
        PlayerCharacter player = GetPrivateField<PlayerCharacter>(manager, "_player");

        SkillData tankMode = new()
        {
            SkillId = 35121005,
            MaxLevel = 1
        };
        tankMode.Levels[1] = new SkillLevelData
        {
            Level = 1
        };

        SkillData tankSiege = new()
        {
            SkillId = 35121013,
            MaxLevel = 1
        };
        tankSiege.Levels[1] = new SkillLevelData
        {
            Level = 1,
            Time = 5
        };

        List<SkillData> availableSkills = GetPrivateList<SkillData>(manager, "_availableSkills");
        availableSkills.Add(tankMode);
        availableSkills.Add(tankSiege);
        manager.SetSkillLevel(tankMode.SkillId, 1);
        manager.SetSkillLevel(tankSiege.SkillId, 1);

        Assert.True(player.ApplySkillAvatarTransform(tankMode.SkillId, null));
        InvokePrivate(
            manager,
            "BeginRepeatSkillSustain",
            tankSiege,
            tankSiege.GetLevel(1),
            1000,
            tankMode.SkillId);
        Assert.True(player.ApplySkillAvatarTransform(tankSiege.SkillId, null));

        bool modeEndRequested = false;
        manager.OnRepeatSkillModeEndRequested = (_, _) => modeEndRequested = true;

        Assert.True(manager.RequestClientSkillCancel(tankSiege.SkillId, 1500));
        Assert.True(modeEndRequested);
        Assert.True(player.HasSkillAvatarTransform(tankSiege.SkillId));

        bool ackAccepted = manager.TryAcknowledgeRepeatSkillModeEndRequest(tankSiege.SkillId, 1501);

        Assert.True(ackAccepted);
        Assert.True(player.HasSkillAvatarTransform(tankSiege.SkillId));

        List<SkillManager.ClientSkillTimerExpiration> expiredTimers = new();
        manager.OnClientSkillTimersExpiredBatch = timers => expiredTimers.AddRange(timers);

        manager.Update(1501, 0f);

        Assert.False(player.HasSkillAvatarTransform(tankSiege.SkillId));
        Assert.True(player.HasSkillAvatarTransform(tankMode.SkillId));
        Assert.Contains(
            expiredTimers,
            timer => timer.SkillId == tankSiege.SkillId
                     && string.Equals(timer.Source, "repeat-mode-end-ack", StringComparison.Ordinal));
    }

    private static SkillManager CreateSkillManager()
    {
        SkillLoader loader = new(null, null, null);
        PlayerCharacter player = new((GraphicsDevice)null, (TexturePool)null, null);
        player.SetPosition(0f, 0f);
        return new SkillManager(loader, player);
    }

    private static List<T> GetPrivateList<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<T>>(field.GetValue(instance));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(instance, args);
    }
}
