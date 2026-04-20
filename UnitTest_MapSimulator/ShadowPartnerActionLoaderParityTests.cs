using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class ShadowPartnerActionLoaderParityTests
{
    [Fact]
    public void BuiltInPiecePlan_Fake_KeepsMountedEventDelayAndMoveFallback()
    {
        SkillAnimation animation = Build("fake");

        Assert.NotNull(animation);
        Assert.Equal(3, animation.Frames.Count);
        Assert.Equal(810, animation.ClientEventDelayMs);
        Assert.Equal(90, animation.Frames[0].Delay);
        Assert.Equal(720, animation.Frames[1].Delay);
        Assert.Equal(1, animation.Frames[2].Delay);
        Assert.Equal(new Point(2, 0), animation.Frames[1].Origin);
    }

    [Fact]
    public void BuiltInPiecePlan_FlashBang_KeepsMountedTotalMinusLastEventDelay()
    {
        SkillAnimation animation = Build("flashBang");

        Assert.NotNull(animation);
        Assert.Equal(3, animation.Frames.Count);
        Assert.Equal(180, animation.ClientEventDelayMs);
        Assert.Equal(90, animation.Frames[0].Delay);
        Assert.Equal(90, animation.Frames[1].Delay);
        Assert.Equal(330, animation.Frames[2].Delay);
    }

    [Fact]
    public void BuiltInPiecePlan_Timeleap_KeepsNegativeDelayAccumulationEventDelay()
    {
        SkillAnimation animation = Build("timeleap");

        Assert.NotNull(animation);
        Assert.Equal(3, animation.Frames.Count);
        Assert.Equal(990, animation.ClientEventDelayMs);
        Assert.Equal(450, animation.Frames[0].Delay);
        Assert.Equal(540, animation.Frames[1].Delay);
        Assert.Equal(450, animation.Frames[2].Delay);
    }

    [Fact]
    public void BuiltInPiecePlan_HomingAndOwlDead_KeepNegativeDelayCarryover()
    {
        SkillAnimation homing = Build("homing");
        SkillAnimation owlDead = Build("owlDead");

        Assert.NotNull(homing);
        Assert.Equal(3, homing.Frames.Count);
        Assert.Equal(720, homing.ClientEventDelayMs);
        Assert.Equal(720, homing.Frames[0].Delay);

        Assert.NotNull(owlDead);
        Assert.Equal(3, owlDead.Frames.Count);
        Assert.Equal(120, owlDead.ClientEventDelayMs);
        Assert.Equal(120, owlDead.Frames[0].Delay);
    }

    [Fact]
    public void BuiltInPiecePlan_SingleFrameRows_KeepZeroEventDelayCarryover()
    {
        SkillAnimation recovery = Build("recovery");
        SkillAnimation backstep = Build("backstep");

        Assert.NotNull(recovery);
        Assert.Single(recovery.Frames);
        Assert.Equal(0, recovery.ClientEventDelayMs);
        Assert.Equal(30, recovery.Frames[0].Delay);

        Assert.NotNull(backstep);
        Assert.Single(backstep.Frames);
        Assert.Equal(0, backstep.ClientEventDelayMs);
        Assert.Equal(300, backstep.Frames[0].Delay);
    }

    private static SkillAnimation Build(string actionName)
    {
        Dictionary<string, SkillAnimation> animations = CreateActionAnimations();
        return ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
            animations,
            actionName,
            supportedRawActionNames: null,
            piecePlanOverride: null,
            requireSupportedRawActionName: false);
    }

    private static Dictionary<string, SkillAnimation> CreateActionAnimations()
    {
        return new Dictionary<string, SkillAnimation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["alert"] = CreateAnimation("alert", 3),
            ["shoot2"] = CreateAnimation("shoot2", 1),
            ["stabO1"] = CreateAnimation("stabO1", 2),
            ["swingO3"] = CreateAnimation("swingO3", 3),
            ["swingO1"] = CreateAnimation("swingO1", 1)
        };
    }

    private static SkillAnimation CreateAnimation(string name, int frameCount)
    {
        var animation = new SkillAnimation { Name = name };
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            animation.Frames.Add(new SkillFrame
            {
                Delay = 7,
                Origin = Point.Zero,
                Bounds = Rectangle.Empty
            });
        }

        animation.CalculateDuration();
        return animation;
    }
}
