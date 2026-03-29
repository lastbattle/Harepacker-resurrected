using System.Reflection;
using System.Runtime.Serialization;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;
using HaCreator.MapSimulator.Entities;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator.Combat;

public class MobAttackSystemTests
{
    [Fact]
    public void BuildProjectileLaneAssignments_PreservesLanePoints_WhenDuplicateTargetsResolve()
    {
        var system = new MobAttackSystem();
        system.SetPlayerHitboxAccessor(() => new Rectangle(92, 188, 24, 24));

        MobItem mobItem = CreateMobShell(100f, 200f, flipX: false);
        var attack = new MobAttackEntry
        {
            IsRanged = true,
            HasRangeBounds = true,
            RangeLeft = -20,
            RangeRight = 20,
            RangeTop = -20,
            RangeBottom = 20,
            ProjectileCount = 2,
            AreaCount = 2,
            StartOffset = 0
        };

        MethodInfo buildAssignments = typeof(MobAttackSystem).GetMethod(
            "BuildProjectileLaneAssignments",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(buildAssignments);

        object result = buildAssignments.Invoke(
            system,
            new object[] { mobItem, attack, null, 100f, 200f, new Vector2(100f, 200f), 0 });

        var assignments = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result).Cast<object>().ToList();
        Assert.Equal(2, assignments.Count);

        Vector2 lanePoint0 = ReadLanePoint(assignments[0]);
        Vector2 lanePoint1 = ReadLanePoint(assignments[1]);
        Assert.Equal(new Vector2(100f, 200f), lanePoint0);
        Assert.Equal(new Vector2(140f, 200f), lanePoint1);

        Assert.All(assignments, assignment =>
        {
            MobTargetInfo targetInfo = ReadTargetInfo(assignment);
            Assert.NotNull(targetInfo);
            Assert.Equal(MobTargetType.Player, targetInfo.TargetType);
        });
    }

    private static Vector2 ReadLanePoint(object assignment)
    {
        PropertyInfo property = assignment.GetType().GetProperty("LanePoint", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<Vector2>(property.GetValue(assignment));
    }

    private static MobTargetInfo ReadTargetInfo(object assignment)
    {
        PropertyInfo property = assignment.GetType().GetProperty("TargetInfo", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<MobTargetInfo>(property.GetValue(assignment));
    }

    private static MobItem CreateMobShell(float x, float y, bool flipX)
    {
#pragma warning disable SYSLIB0050
        var mobItem = (MobItem)FormatterServices.GetUninitializedObject(typeof(MobItem));
#pragma warning restore SYSLIB0050
        mobItem.MovementEnabled = true;

        var movementInfo = new MobMovementInfo
        {
            X = x,
            Y = y,
            FlipX = flipX
        };

        FieldInfo movementInfoField = typeof(MobItem).GetField(
            "<MovementInfo>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(movementInfoField);
        movementInfoField.SetValue(mobItem, movementInfo);

        return mobItem;
    }
}
