using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class ProjectileTargetSnapshotParityTests
{
    [Fact]
    public void ResolveDeferredProjectileTargetPositionSnapshot_PrefersStoredPoint_ForSingleTargetDeferredShots()
    {
        Vector2 storedPoint = new(120f, 48f);
        Vector2 livePoint = new(220f, 88f);

        Vector2? result = SkillManager.ResolveDeferredProjectileTargetPositionSnapshot(
            storedPoint,
            livePoint);

        Assert.Equal(storedPoint, result);
    }

    [Fact]
    public void ResolveDeferredProjectileTargetPositionSnapshot_PrefersLivePoint_WhenMultiTargetVolleyNeedsDistinctAssignments()
    {
        Vector2 storedPoint = new(120f, 48f);
        Vector2 livePoint = new(220f, 88f);

        Vector2? result = SkillManager.ResolveDeferredProjectileTargetPositionSnapshot(
            storedPoint,
            livePoint,
            preferStoredTargetPosition: false);

        Assert.Equal(livePoint, result);
    }

    [Fact]
    public void ResolveDeferredProjectileTargetPositionSnapshot_FallsBackToStoredPoint_WhenLiveAssignmentIsMissing()
    {
        Vector2 storedPoint = new(120f, 48f);

        Vector2? result = SkillManager.ResolveDeferredProjectileTargetPositionSnapshot(
            storedPoint,
            livePreferredTargetPosition: null,
            preferStoredTargetPosition: false);

        Assert.Equal(storedPoint, result);
    }
}
