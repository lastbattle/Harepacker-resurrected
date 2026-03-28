using System.Reflection;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class SummonedPoolParityTests
{
    [Fact]
    public void ResolvePacketHitEffectPosition_AttachesToSummonWhenMobAttackMetadataRequiresIt()
    {
        MethodInfo method = GetResolvePacketHitEffectPositionMethod();

        var attackInfo = new MobAnimationSet.AttackInfoMetadata
        {
            HitAttach = true
        };

        object[] args =
        {
            new Rectangle(10, 20, 30, 40),
            new Vector2(111f, 222f),
            attackInfo,
            new Random(7)
        };

        Vector2 result = (Vector2)method.Invoke(null, args);

        Assert.Equal(new Vector2(111f, 222f), result);
    }

    [Fact]
    public void ResolvePacketHitEffectPosition_UsesRandomBodyRectPlacementWhenNotAttached()
    {
        MethodInfo method = GetResolvePacketHitEffectPositionMethod();

        object[] args =
        {
            new Rectangle(10, 20, 30, 40),
            new Vector2(111f, 222f),
            new MobAnimationSet.AttackInfoMetadata(),
            new Random(7)
        };

        Vector2 result = (Vector2)method.Invoke(null, args);

        Assert.InRange(result.X, 10f, 39f);
        Assert.InRange(result.Y, 20f, 59f);
        Assert.NotEqual(new Vector2(111f, 222f), result);
    }

    private static MethodInfo GetResolvePacketHitEffectPositionMethod()
    {
        MethodInfo method = typeof(SummonedPool).GetMethod(
            "ResolvePacketHitEffectPosition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[]
            {
                typeof(Rectangle),
                typeof(Vector2),
                typeof(MobAnimationSet.AttackInfoMetadata),
                typeof(Random)
            },
            null);

        Assert.NotNull(method);
        return method;
    }
}
