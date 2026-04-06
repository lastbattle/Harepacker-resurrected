using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public sealed class AntiMacroOwnerParityTests
    {
        [Fact]
        public void AntiMacroOwnerStringPoolText_ResolvesExactClientNoticePayloads()
        {
            Assert.True(
                AntiMacroOwnerStringPoolText.TryResolve(
                    AntiMacroOwnerStringPoolText.NoticeFailureRestrictionStringPoolId,
                    out string failureRestriction));
            Assert.Equal(
                "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.",
                failureRestriction);

            Assert.True(
                AntiMacroOwnerStringPoolText.TryResolve(
                    AntiMacroOwnerStringPoolText.NoticeAdminThanksStringPoolId,
                    out string adminThanks));
            Assert.Equal("Thank you for your cooperation.", adminThanks);
        }

        [Fact]
        public void AntiMacroOwnerStringPoolText_ResolvesExactClientChatPayloads()
        {
            Assert.True(
                AntiMacroOwnerStringPoolText.TryResolve(
                    AntiMacroOwnerStringPoolText.ChatAdminActivateStringPoolId,
                    out string adminActivate));
            Assert.Equal(
                "%s_The screenshot has been saved. The Lie Detector has been activated.",
                adminActivate);

            Assert.True(
                AntiMacroOwnerStringPoolText.TryGetEvidence(
                    AntiMacroOwnerStringPoolText.ChatAdminActivateStringPoolId,
                    out string rawHex,
                    out byte seed,
                    out string clientSource));
            Assert.Equal(0xC6, seed);
            Assert.Equal(
                "C6 1C ED AB 98 A4 5C EB C6 D4 EF 04 F4 F7 5B B4 15 4D BE 9C AD BF 19 A9 D0 D2 F3 41 E2 F8 5E B9 1E 17 BE A0 A4 A9 19 87 DC D2 BD 25 F4 ED 4D BF 0E 56 EC D4 A4 AD 4A EB D7 D2 F8 0F B1 F8 4B A8 13 4F FF 80 A9 A8 17",
                rawHex);
            Assert.Equal(
                "CWvsContext::OnAntiMacroResult mode 5 / MapleStory.exe v95 StringPool::GetString",
                clientSource);
        }
    }
}
