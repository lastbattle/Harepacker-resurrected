using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public class MorphActionResolverParityTests
{
    [Theory]
    [InlineData("flamesplash")]
    [InlineData("swiftShot")]
    [InlineData("cannonSmash")]
    [InlineData("giganticBackstep")]
    [InlineData("rushBoom")]
    [InlineData("cannonSlam")]
    [InlineData("counterCannon")]
    [InlineData("cannonSpike")]
    [InlineData("superCannon")]
    [InlineData("magneticCannon")]
    [InlineData("bombExplosion")]
    [InlineData("monkeyBoomboom")]
    public void CannonFamilyRawActionsCollapseOntoPirateGunMorphSurface(string actionName)
    {
        CharacterPart morphPart = CreateMorphPart(
            "doublefire",
            "eburster",
            "shockwave",
            "somersault",
            "fist");

        string? resolvedActionName = MorphClientActionResolver
            .EnumerateClientActionAliases(morphPart, actionName)
            .FirstOrDefault();

        Assert.Equal("doublefire", resolvedActionName);
    }

    [Fact]
    public void CannonFamilyRawActionsDoNotStealArcherOnlyMorphSurface()
    {
        CharacterPart morphPart = CreateMorphPart("windshot", "windspear", "shoot1", "arrowRain");

        string[] resolvedActionNames = MorphClientActionResolver
            .EnumerateClientActionAliases(morphPart, "cannonSmash")
            .ToArray();

        Assert.DoesNotContain("windshot", resolvedActionNames);
        Assert.DoesNotContain("windspear", resolvedActionNames);
        Assert.DoesNotContain("shoot1", resolvedActionNames);
        Assert.DoesNotContain("arrowRain", resolvedActionNames);
    }

    [Fact]
    public void ParalyzeRawActionUsesGenericRangedMorphSurfaceBeforeArcherAliases()
    {
        CharacterPart morphPart = CreateMorphPart("windshot", "windspear", "shoot1", "arrowRain");

        string? resolvedActionName = MorphClientActionResolver
            .EnumerateClientActionAliases(morphPart, "paralyze")
            .FirstOrDefault();

        Assert.Equal("shoot1", resolvedActionName);
    }

    [Fact]
    public void ArrowEruptionRawActionUsesAuthoredArrowRainMorphSurface()
    {
        CharacterPart morphPart = CreateMorphPart("windshot", "windspear", "shoot1", "arrowRain");

        string? resolvedActionName = MorphClientActionResolver
            .EnumerateClientActionAliases(morphPart, "arrowEruption")
            .FirstOrDefault();

        Assert.Equal("arrowRain", resolvedActionName);
    }

    private static CharacterPart CreateMorphPart(params string[] actionNames)
    {
        var morphPart = new CharacterPart
        {
            Type = CharacterPartType.Morph,
            Animations = new Dictionary<string, CharacterAnimation>(),
            AvailableAnimations = new HashSet<string>(actionNames, System.StringComparer.OrdinalIgnoreCase)
        };

        foreach (string actionName in actionNames)
        {
            morphPart.Animations[actionName] = new CharacterAnimation
            {
                ActionName = actionName,
                Frames = { new CharacterFrame() }
            };
        }

        return morphPart;
    }
}
