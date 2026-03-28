using System;
using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public class SkillManagerSwallowParityTests
{
    [Fact]
    public void RequestClientSkillCancel_AcceptsWzLinkedSwallowDummySkill()
    {
        SkillManager manager = CreateSkillManager();
        SeedSwallowSkills(manager);
        SetPrivateField(manager, "_swallowState", CreateSwallowState());

        bool canceled = manager.RequestClientSkillCancel(33101006, 1000);

        Assert.True(canceled);
        Assert.Null(GetPrivateField<object>(manager, "_swallowState"));
    }

    [Fact]
    public void TryResolveSwallowAbsorbRequest_AcceptsWzLinkedSwallowDummySkill()
    {
        SkillManager manager = CreateSkillManager();
        SeedSwallowSkills(manager);
        SetPrivateField(manager, "_swallowState", CreateSwallowState());

        bool accepted = manager.TryResolveSwallowAbsorbRequest(33101007, 123, success: false, currentTime: 1000);

        Assert.True(accepted);
        Assert.Null(GetPrivateField<object>(manager, "_swallowState"));
    }

    [Fact]
    public void SwallowRestrictionDetection_DoesNotFallBackToNameText()
    {
        MethodInfo method = typeof(PlayerSkillStateRestrictionEvaluator).GetMethod(
            "IsSwallowSkill",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        bool isSwallow = (bool)method.Invoke(null, new object[]
        {
            new SkillData
            {
                SkillId = 99999999,
                Name = "Fake Swallow",
                ActionName = "swallow_loop"
            }
        });

        Assert.False(isSwallow);
    }

    private static SkillManager CreateSkillManager()
    {
        SkillLoader loader = new(null, null, null);
        PlayerCharacter player = new((GraphicsDevice)null, (TexturePool)null, null);
        player.SetPosition(0f, 0f);
        return new SkillManager(loader, player);
    }

    private static void SeedSwallowSkills(SkillManager manager)
    {
        List<SkillData> availableSkills = GetPrivateField<List<SkillData>>(manager, "_availableSkills");
        availableSkills.Add(new SkillData
        {
            SkillId = 33101005,
            DummySkillParents = new[] { 33101006, 33101007 }
        });
        availableSkills.Add(new SkillData
        {
            SkillId = 33101006,
            IsSwallowSkill = true
        });
        availableSkills.Add(new SkillData
        {
            SkillId = 33101007,
            IsSwallowSkill = true
        });
    }

    private static object CreateSwallowState()
    {
        Type swallowStateType = typeof(SkillManager).GetNestedType("SwallowState", BindingFlags.NonPublic);
        Assert.NotNull(swallowStateType);

        object state = Activator.CreateInstance(swallowStateType, nonPublic: true);
        Assert.NotNull(state);

        SetProperty(state, "SkillId", 33101005);
        SetProperty(state, "ParentSkillId", 33101005);
        SetProperty(state, "Level", 1);
        SetProperty(state, "TargetMobId", 123);
        SetProperty(state, "PendingAbsorbOutcome", true);
        return state;
    }

    private static void SetProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property.SetValue(instance, value);
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
}
