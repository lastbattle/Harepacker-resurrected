using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class MeleeRangedMagicResolutionParityTests
{
    [Theory]
    [InlineData(33111006, true)]
    [InlineData(33121009, true)]
    [InlineData(33121001, false)]
    public void UsesClientTargetIndexedHitAnimation_CoversCheckedWildHunterLane(int skillId, bool expected)
    {
        Assert.Equal(expected, SkillManager.UsesClientTargetIndexedHitAnimation(skillId));
    }

    [Fact]
    public void ResolveClientTargetHitAnimation_ClientIndexedLane_UsesRawTargetOrderWithoutClamp()
    {
        SkillAnimation hit0 = CreateHitAnimation("hit0");
        SkillAnimation hit1 = CreateHitAnimation("hit1");
        SkillAnimation hit2 = CreateHitAnimation("hit2");
        SkillData skill = new()
        {
            SkillId = 33111006,
            TargetHitEffects = new[] { hit0, hit1, hit2 }
        };

        SkillAnimation resolvedThird = SkillManager.ResolveClientTargetHitAnimation(
            skill,
            fallbackAnimation: null,
            targetOrder: 2);
        SkillAnimation resolvedOutOfRange = SkillManager.ResolveClientTargetHitAnimation(
            skill,
            fallbackAnimation: null,
            targetOrder: 3);

        Assert.Same(hit2, resolvedThird);
        Assert.Null(resolvedOutOfRange);
    }

    [Fact]
    public void ResolveClientTargetHitAnimation_GenericIndexedFallback_RemainsClamped()
    {
        SkillAnimation hit0 = CreateHitAnimation("hit0");
        SkillAnimation hit1 = CreateHitAnimation("hit1");
        SkillAnimation hit2 = CreateHitAnimation("hit2");
        SkillData skill = new()
        {
            SkillId = 2301005,
            TargetHitEffects = new[] { hit0, hit1, hit2 }
        };

        SkillAnimation resolved = SkillManager.ResolveClientTargetHitAnimation(
            skill,
            fallbackAnimation: null,
            targetOrder: 9);

        Assert.Same(hit2, resolved);
    }

    [Fact]
    public void TryResolveRenderedMountedClientBodyRelMoveY_MatchedVehicle_ConsumesRenderedMapPoint()
    {
        AssembledFrame frame = new()
        {
            MapPoints = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
            {
                [AvatarActionLayerCoordinator.ClientBodyOriginMapPoint] = new Point(1, -54)
            }
        };
        CharacterPart mountedStatePart = new()
        {
            Slot = EquipSlot.TamingMob,
            ItemId = 1932015
        };

        bool resolved = PlayerCharacter.TryResolveRenderedMountedClientBodyRelMoveY(
            frame,
            mountedVehicleId: 1932015,
            mountedStatePart,
            out int bodyRelMoveY);

        Assert.True(resolved);
        Assert.Equal(-54, bodyRelMoveY);
    }

    [Fact]
    public void TryResolveRenderedMountedClientBodyRelMoveY_MismatchedVehicle_RejectsRenderedReuse()
    {
        AssembledFrame frame = new()
        {
            MapPoints = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
            {
                [AvatarActionLayerCoordinator.ClientBodyOriginMapPoint] = new Point(4, -51)
            }
        };
        CharacterPart mountedStatePart = new()
        {
            Slot = EquipSlot.TamingMob,
            ItemId = 1932030
        };

        bool resolved = PlayerCharacter.TryResolveRenderedMountedClientBodyRelMoveY(
            frame,
            mountedVehicleId: 1932015,
            mountedStatePart,
            out int bodyRelMoveY);

        Assert.False(resolved);
        Assert.Equal(0, bodyRelMoveY);
    }

    [Fact]
    public void TryResolveRenderedMountedClientBodyRelMoveY_UnspecifiedVehicle_AdmitsActiveMount()
    {
        AssembledFrame frame = new()
        {
            MapPoints = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
            {
                [AvatarActionLayerCoordinator.ClientBodyOriginMapPoint] = new Point(4, -51)
            }
        };
        CharacterPart mountedStatePart = new()
        {
            Slot = EquipSlot.TamingMob,
            ItemId = 1932030
        };

        bool resolved = PlayerCharacter.TryResolveRenderedMountedClientBodyRelMoveY(
            frame,
            mountedVehicleId: 0,
            mountedStatePart,
            out int bodyRelMoveY);

        Assert.True(resolved);
        Assert.Equal(-51, bodyRelMoveY);
    }

    [Fact]
    public void TryResolveRenderedMountedClientBodyRelMoveY_MissingMapPoint_RejectsRenderedFrame()
    {
        AssembledFrame frame = new();
        CharacterPart mountedStatePart = new()
        {
            Slot = EquipSlot.TamingMob,
            ItemId = 1932015
        };

        bool resolved = PlayerCharacter.TryResolveRenderedMountedClientBodyRelMoveY(
            frame,
            mountedVehicleId: 1932015,
            mountedStatePart,
            out int bodyRelMoveY);

        Assert.False(resolved);
        Assert.Equal(0, bodyRelMoveY);
    }

    private static SkillAnimation CreateHitAnimation(string name)
    {
        return new SkillAnimation { Name = name };
    }
}
