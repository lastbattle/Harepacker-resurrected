using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class SkillManagerSummonParityTests
{
    [Fact]
    public void TryBuildTeslaCoilTriangle_UsesSummonSlotOrdering()
    {
        MethodInfo method = typeof(SkillManager).GetMethod(
            "TryBuildTeslaCoilTriangle",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var coils = new List<ActiveSummon>
        {
            new() { SummonSlotIndex = 2, PositionX = 120f, PositionY = 0f },
            new() { SummonSlotIndex = 0, PositionX = -120f, PositionY = 0f },
            new() { SummonSlotIndex = 1, PositionX = 0f, PositionY = -90f }
        };

        object[] args = { coils, null, null, null };
        bool success = (bool)method.Invoke(null, args);

        Assert.True(success);
        Assert.Equal(new Vector2(-120f, 0f), Assert.IsType<Vector2>(args[1]));
        Assert.Equal(new Vector2(0f, -90f), Assert.IsType<Vector2>(args[2]));
        Assert.Equal(new Vector2(120f, 0f), Assert.IsType<Vector2>(args[3]));
    }

    [Fact]
    public void DoesRectangleIntersectTriangle_DetectsTeslaTriangleTargets()
    {
        MethodInfo method = typeof(SkillManager).GetMethod(
            "DoesRectangleIntersectTriangle",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        Vector2 left = new(-120f, 0f);
        Vector2 apex = new(0f, -90f);
        Vector2 right = new(120f, 0f);

        bool inside = (bool)method.Invoke(
            null,
            new object[]
            {
                new Rectangle(-10, -20, 20, 20),
                left,
                apex,
                right
            });
        bool outside = (bool)method.Invoke(
            null,
            new object[]
            {
                new Rectangle(180, 40, 20, 20),
                left,
                apex,
                right
            });

        Assert.True(inside);
        Assert.False(outside);
    }
}
