using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class FaceLookLoaderParityTests
{
    [Fact]
    public void GetLook_ComposesFaceAndAccessoryFramesAndCachesByAccessoryId()
    {
        FacePart face = new()
        {
            ItemId = 20000,
            Expressions =
            {
                ["smile"] = CreateAnimation(
                    CreateFrame(delay: 80, browX: -1, browY: -12, z: "face"),
                    CreateFrame(delay: 90, browX: -2, browY: -13, z: "face"))
            }
        };
        CharacterPart faceAccessory = new()
        {
            ItemId = 1010000,
            Type = CharacterPartType.Face_Accessory,
            Animations =
            {
                ["smile"] = CreateAnimation(
                    CreateFrame(delay: 45, browX: -10, browY: -44, z: "accessoryFace"))
            }
        };

        FaceLookEntry firstLook = face.GetLook("smile", SkinColor.Light, faceAccessory);
        FaceLookEntry cachedLook = face.GetLook("smile", SkinColor.Light, faceAccessory);
        FaceLookEntry differentAccessoryLook = face.GetLook("smile", SkinColor.Light, new CharacterPart
        {
            ItemId = 1010001,
            Type = CharacterPartType.Face_Accessory,
            Animations =
            {
                ["smile"] = CreateAnimation(
                    CreateFrame(delay: 50, browX: -9, browY: -43, z: "accessoryFace"))
            }
        });

        Assert.Same(firstLook, cachedLook);
        Assert.NotSame(firstLook, differentAccessoryLook);
        Assert.Equal(2, firstLook.Frames.Count);
        Assert.True(firstLook.HasAccessory);
        Assert.Equal(170, firstLook.TotalDuration);
        Assert.Same(face.Expressions["smile"].Frames[1], firstLook.Frames[1].FaceFrame);
        Assert.Same(faceAccessory.Animations["smile"].Frames[0], firstLook.Frames[1].AccessoryFrame);
    }

    [Fact]
    public void GetLook_FallsBackToDefaultExpressionAndUsesAccessoryDelayWhenNeeded()
    {
        FacePart face = new()
        {
            ItemId = 20000,
            Expressions =
            {
                ["default"] = CreateAnimation(
                    CreateFrame(delay: 0, browX: -1, browY: -12, z: "face"))
            }
        };
        CharacterPart faceAccessory = new()
        {
            ItemId = 1010000,
            Type = CharacterPartType.Face_Accessory,
            Animations =
            {
                ["default"] = CreateAnimation(
                    CreateFrame(delay: 35, browX: -10, browY: -44, z: "accessoryFace"))
            }
        };

        FaceLookEntry look = face.GetLook("oops", SkinColor.Light, faceAccessory);

        Assert.NotNull(look);
        Assert.Equal("oops", look.ExpressionName);
        Assert.Single(look.Frames);
        Assert.Equal(35, look.Frames[0].Delay);
        Assert.Equal(35, look.TotalDuration);
        Assert.Same(face.Expressions["default"].Frames[0], look.Frames[0].FaceFrame);
        Assert.Same(faceAccessory.Animations["default"].Frames[0], look.Frames[0].AccessoryFrame);
        Assert.True(face.TryGetLookDuration("oops", SkinColor.Light, faceAccessory, out int durationMs));
        Assert.Equal(35, durationMs);
    }

    private static CharacterAnimation CreateAnimation(params CharacterFrame[] frames)
    {
        CharacterAnimation animation = new();
        foreach (CharacterFrame frame in frames)
        {
            animation.Frames.Add(frame);
        }

        animation.CalculateTotalDuration();
        return animation;
    }

    private static CharacterFrame CreateFrame(int delay, int browX, int browY, string z)
    {
        CharacterFrame frame = new()
        {
            Delay = delay,
            Origin = Point.Zero,
            Z = z
        };
        frame.Map["brow"] = new Point(browX, browY);
        return frame;
    }
}
