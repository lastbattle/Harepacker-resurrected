using System.Linq;
using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator
{
    public sealed class ShadowPartnerClientActionResolverTests
    {
        [Fact]
        public void EnumerateClientMappedCandidates_RawShowdownPrefersShadowPartnerHelperAliasBeforeRawName()
        {
            int showdownActionCode = FindActionCode("showdown");

            string[] candidates = ShadowPartnerClientActionResolver
                .EnumerateClientMappedCandidates("attack1", PlayerState.Standing, null, rawActionCode: showdownActionCode)
                .Take(4)
                .ToArray();

            Assert.Equal("stabO1", candidates[0]);
            Assert.Equal("stabO2", candidates[1]);
            Assert.Equal("stabOF", candidates[2]);
            Assert.Equal("showdown", candidates[3]);
        }

        [Fact]
        public void ResolveClientFrameRemap_AlertFamilyClampsIndexedHelperFrameToPublishedRange()
        {
            int[] remap = ShadowPartnerClientActionResolver
                .ResolveClientFrameRemap("alert5", null, "alert", 3)
                .ToArray();

            Assert.Single(remap);
            Assert.Equal(2, remap[0]);
        }

        [Fact]
        public void ResolveClientFrameRemap_Prone2UsesSecondProneFrame()
        {
            int[] remap = ShadowPartnerClientActionResolver
                .ResolveClientFrameRemap("prone2", null, "prone", 2)
                .ToArray();

            Assert.Single(remap);
            Assert.Equal(1, remap[0]);
        }

        private static int FindActionCode(string actionName)
        {
            for (int actionCode = 0; actionCode < 512; actionCode++)
            {
                if (CharacterPart.TryGetActionStringFromCode(actionCode, out string resolvedActionName)
                    && string.Equals(resolvedActionName, actionName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return actionCode;
                }
            }

            throw new Xunit.Sdk.XunitException($"Unable to locate action code for '{actionName}'.");
        }
    }
}
