using System;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator;

public class SkillManagerQueuedFollowUpParityTests
{
    [Fact]
    public void QueueOrReplaceFollowUpAttack_ReplacesPendingFinalAttackWithoutClearingSummonQueue()
    {
        SkillManager manager = CreateSkillManager();

        Type managerType = typeof(SkillManager);
        Type queuedFollowUpType = managerType.GetNestedType("QueuedFollowUpAttack", BindingFlags.NonPublic);
        Type queuedSummonType = managerType.GetNestedType("QueuedSummonAttack", BindingFlags.NonPublic);
        Assert.NotNull(queuedFollowUpType);
        Assert.NotNull(queuedSummonType);

        object initialFollowUp = CreateQueuedFollowUpAttack(queuedFollowUpType, skillId: 1111003, executeTime: 1000);
        object replacementFollowUp = CreateQueuedFollowUpAttack(queuedFollowUpType, skillId: 1111005, executeTime: 1100);
        object queuedSummon = Activator.CreateInstance(queuedSummonType);
        Assert.NotNull(initialFollowUp);
        Assert.NotNull(replacementFollowUp);
        Assert.NotNull(queuedSummon);

        InvokeCollectionMethod(GetPrivateField(manager, "_queuedFollowUpAttacks"), "Enqueue", initialFollowUp);
        InvokeCollectionMethod(GetPrivateField(manager, "_queuedSummonAttacks"), "Add", queuedSummon);

        MethodInfo queueMethod = managerType.GetMethod(
            "QueueOrReplaceFollowUpAttack",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(queueMethod);

        queueMethod.Invoke(manager, new[] { replacementFollowUp });

        Assert.Equal(1, GetCollectionCount(manager, "_queuedFollowUpAttacks"));
        Assert.Equal(1, GetCollectionCount(manager, "_queuedSummonAttacks"));

        object queued = InvokeCollectionMethod(GetPrivateField(manager, "_queuedFollowUpAttacks"), "Peek");
        Assert.Equal(1111005, (int)queuedFollowUpType.GetProperty("SkillId").GetValue(queued));
        Assert.Equal(1100, (int)queuedFollowUpType.GetProperty("ExecuteTime").GetValue(queued));
    }

    private static SkillManager CreateSkillManager()
    {
        SkillLoader loader = new(null, null, null);
        PlayerCharacter player = new((GraphicsDevice)null, texturePool: null, build: null);
        player.SetPosition(0f, 0f);
        return new SkillManager(loader, player);
    }

    private static object CreateQueuedFollowUpAttack(Type queuedFollowUpType, int skillId, int executeTime)
    {
        object queuedFollowUp = Activator.CreateInstance(queuedFollowUpType);
        Assert.NotNull(queuedFollowUp);
        queuedFollowUpType.GetProperty("SkillId").SetValue(queuedFollowUp, skillId);
        queuedFollowUpType.GetProperty("Level").SetValue(queuedFollowUp, 1);
        queuedFollowUpType.GetProperty("ExecuteTime").SetValue(queuedFollowUp, executeTime);
        queuedFollowUpType.GetProperty("FacingRight").SetValue(queuedFollowUp, true);
        queuedFollowUpType.GetProperty("RequiredWeaponCode").SetValue(queuedFollowUp, 30);
        return queuedFollowUp;
    }

    private static object GetPrivateField(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object value = field.GetValue(instance);
        Assert.NotNull(value);
        return value;
    }

    private static object InvokeCollectionMethod(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return method.Invoke(instance, args);
    }

    private static int GetCollectionCount(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object value = field.GetValue(instance);
        PropertyInfo countProperty = value?.GetType().GetProperty("Count");
        Assert.NotNull(countProperty);
        return (int)countProperty.GetValue(value);
    }
}
