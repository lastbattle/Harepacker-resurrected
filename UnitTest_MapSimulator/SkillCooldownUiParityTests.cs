using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class SkillCooldownUiParityTests
    {
        [Theory]
        [InlineData((int)SkillTooltipAnchorOwner.SkillBook, 340, 200, 340, 220)]
        [InlineData((int)SkillTooltipAnchorOwner.QuickSlot, 340, 200, 340, 220)]
        [InlineData((int)SkillTooltipAnchorOwner.StatusBarCooldownTray, 340, 200, 340, 72)]
        [InlineData((int)SkillTooltipAnchorOwner.StatusBarOffBarCooldownTray, 340, 200, 360, 220)]
        [InlineData((int)SkillTooltipAnchorOwner.LegacyPanel, 340, 200, 352, 196)]
        public void ResolveTooltipAnchorFromCursor_UsesClientOwnerOffsets(
            int owner,
            int cursorX,
            int cursorY,
            int expectedX,
            int expectedY)
        {
            Point actual = SkillTooltipFrameLayout.ResolveTooltipAnchorFromCursor(
                new Point(cursorX, cursorY),
                (SkillTooltipAnchorOwner)owner);

            Assert.Equal(new Point(expectedX, expectedY), actual);
        }

        [Fact]
        public void ResolveSameFamilyOriginFallback_UsesLegacyOriginForZeroAuthoredTipOrigin()
        {
            Point resolved = SkillTooltipFrameLayout.ResolveSameFamilyOriginFallback(
                authoredOrigin: Point.Zero,
                authoredWidth: 193,
                authoredHeight: 102,
                fallbackOrigin: new Point(195, -25),
                fallbackWidth: 193,
                fallbackHeight: 102);

            Assert.Equal(new Point(195, -25), resolved);
        }

        [Fact]
        public void ResolveCooldownMaskFrameIndex_UsesQuickSlotClientSecondQuantization()
        {
            int frameIndex = SkillManager.ResolveCooldownMaskFrameIndex(
                remainingMs: 9000,
                durationMs: 10000,
                maskSurface: SkillManager.CooldownMaskSurface.QuickSlot);

            Assert.Equal(1, frameIndex);
        }

        [Fact]
        public void ResolveCooldownMaskFallbackFillRatio_UsesFinalSkillBookSliver()
        {
            float ratio = SkillManager.ResolveCooldownMaskFallbackFillRatio(
                frameIndex: 15,
                maskSurface: SkillManager.CooldownMaskSurface.SkillBook);

            Assert.Equal(1f / 16f, ratio, 5);
        }

        [Fact]
        public void ResolveTopMarginForClientParity_ReturnsClientNoticeAnchor()
        {
            int topMargin = SkillCooldownNoticeUI.ResolveTopMarginForClientParity(screenHeight: 578);

            Assert.Equal(44, topMargin);
        }

        [Fact]
        public void ResolveNoticeDurationForClientParity_UsesFixedFiveSecondLifetime()
        {
            int durationMs = SkillCooldownNoticeUI.ResolveNoticeDurationForClientParity(SkillCooldownNoticeType.Started);

            Assert.Equal(5000, durationMs);
        }

        [Fact]
        public void ResolveMaxConcurrentNoticesForClientParity_UsesSingleNoticeOwner()
        {
            int maxNotices = SkillCooldownNoticeUI.ResolveMaxConcurrentNoticesForClientParity();

            Assert.Equal(1, maxNotices);
        }

        [Fact]
        public void ShouldAcceptIncomingNoticeForClientParity_NoActiveOwner_Accepts()
        {
            bool accepted = SkillCooldownNoticeUI.ShouldAcceptIncomingNoticeForClientParity(
                activeNotices: Array.Empty<(int SkillId, bool IsExpired)>(),
                incomingSkillId: 1001005);

            Assert.True(accepted);
        }

        [Fact]
        public void ShouldAcceptIncomingNoticeForClientParity_ActiveOwnerDifferentSkill_RejectsPreemption()
        {
            bool accepted = SkillCooldownNoticeUI.ShouldAcceptIncomingNoticeForClientParity(
                activeNotices: new[] { (1001005, false) },
                incomingSkillId: 2001002);

            Assert.False(accepted);
        }

        [Fact]
        public void ShouldAcceptIncomingNoticeForClientParity_ActiveOwnerSameSkill_AcceptsRefresh()
        {
            bool accepted = SkillCooldownNoticeUI.ShouldAcceptIncomingNoticeForClientParity(
                activeNotices: new[] { (1001005, false) },
                incomingSkillId: 1001005);

            Assert.True(accepted);
        }
    }
}
