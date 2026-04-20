using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class MorphClientActionResolverParityTests
{
    public static IEnumerable<object[]> PostTableRedirectAliasCases()
    {
        yield return Case("darkSpin", new[] { "alert", "swingT1" });
        yield return Case("blessOfGaia", new[] { "alert" });
        yield return Case("demonGravity", new[] { "alert" });
        yield return Case("swingRes", new[] { "swingO2" });
        yield return Case("lasergun", new[] { "shoot2", "stabO1" });
        yield return Case("battlecharge", new[] { "swingOF", "stabT1", "alert" });
        yield return Case("darkTornado_pre", new[] { "stabO1", "swingO2", "swingTF" });
        yield return Case("darkTornado", new[] { "swingTF", "swingO2" });
        yield return Case("darkTornado_after", new[] { "swingO2", "swingTF", "alert" });
        yield return Case("tripleBlow", new[] { "alert", "swingO1", "swingO3", "stabO1" });
        yield return Case("quadBlow", new[] { "alert", "swingO1", "swingO3", "stabO2", "stabO1" });
        yield return Case("deathBlow", new[] { "swingPF", "stabO2", "swingO1", "swingO2", "swingOF", "stabT2", "stabT1", "alert" });
        yield return Case("finishBlow", new[] { "swingPF", "stabO2", "swingO1", "swingO2", "swingOF", "stabT2" });

        static object[] Case(string actionName, string[] expectedPrefix)
        {
            return
            [
                actionName,
                expectedPrefix
            ];
        }
    }

    [Theory]
    [MemberData(nameof(PostTableRedirectAliasCases))]
    public void EnumerateClientActionAliases_Preserves_Checked_PostTable_BodyRedirectOrder(
        string requestedAction,
        string[] expectedPrefix)
    {
        CharacterPart part = CreateMorphPart(expectedPrefix);

        List<string> aliases = MorphClientActionResolver
            .EnumerateClientActionAliases(part, requestedAction)
            .Take(expectedPrefix.Length + 8)
            .ToList();

        Assert.True(aliases.Count >= expectedPrefix.Length,
            $"Expected at least {expectedPrefix.Length} aliases for '{requestedAction}' but got {aliases.Count}: {string.Join(", ", aliases)}");

        for (int i = 0; i < expectedPrefix.Length; i++)
        {
            Assert.Equal(expectedPrefix[i], aliases[i], ignoreCase: true);
        }
    }

    private static CharacterPart CreateMorphPart(IEnumerable<string> publishedActions)
    {
        return new CharacterPart
        {
            Type = CharacterPartType.Morph,
            AvailableAnimations = new HashSet<string>(
                publishedActions.Where(action => !string.IsNullOrWhiteSpace(action)),
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
