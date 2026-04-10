using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Animation
{
    internal sealed class ProductionEnhancementAnimationDisplayer
    {
        private const int ItemUpgradeEnchantSkillDelayMs = 3120;
        private const int ViciousHammerRepeatDurationMs = 2700;
        private const int VegaRepeatDurationMs = 2500;
        private const string CashGachaponNormalTag = "cashgachapon:normal";
        private const string CashGachaponJackpotTag = "cashgachapon:jackpot";
        private const string CashGachaponCopyNormalTag = "cashgachaponcopy:normal";
        private const string CashGachaponCopyJackpotTag = "cashgachaponcopy:jackpot";
        private static readonly Point ViciousHammerRepeatOffset = new(91, 81);
        private static readonly Point ViciousHammerFinishedOffset = new(89, 105);
        private static readonly Point VegaCastingOffset = new(91, 41);
        private static readonly Point VegaResultTwinklingOffset = new(85, 114);
        private static readonly Point VegaResultArrowOffset = new(85, 114);
        private static readonly Point VegaSuccessOffset = new(79, 71);
        private static readonly Point VegaFailureOffset = new(6, 45);

        private readonly AnimationDisplayerWindowOverlayOwner _owner = new();

        private List<IDXObject> _itemMakeSuccessFrames;
        private List<IDXObject> _itemMakeFailureFrames;
        private List<IDXObject> _itemUpgradeSuccessFrames;
        private List<IDXObject> _itemUpgradeFailureFrames;
        private List<IDXObject> _viciousHammerRepeatFrames;
        private List<IDXObject> _viciousHammerFinishedFrames;
        private List<IDXObject> _skillBookSuccessFrontFrames;
        private List<IDXObject> _skillBookSuccessBackFrames;
        private List<IDXObject> _skillBookFailureFrontFrames;
        private List<IDXObject> _skillBookFailureBackFrames;
        private List<IDXObject> _vegaTwinklingFrames;
        private List<IDXObject> _vegaSpellingFrames;
        private List<IDXObject> _vegaArrowFrames;
        private List<IDXObject> _vegaSuccessFrames;
        private List<IDXObject> _vegaFailureFrames;
        private List<IDXObject> _cashGachaponNormalFrames;
        private List<IDXObject> _cashGachaponJackpotFrames;
        private List<IDXObject> _cashGachaponCopyNormalFrames;
        private List<IDXObject> _cashGachaponCopyJackpotFrames;
        private readonly Dictionary<ItemUpgradeUI.VisualThemeKind, CubeAnimationTheme> _cubeThemes = new();

        public AnimationDisplayerWindowOverlayOwner Owner => _owner;

        public void ConfigureItemMake(List<IDXObject> successFrames, List<IDXObject> failureFrames)
        {
            _itemMakeSuccessFrames = successFrames;
            _itemMakeFailureFrames = failureFrames;
        }

        public void ConfigureItemUpgrade(List<IDXObject> successFrames, List<IDXObject> failureFrames)
        {
            _itemUpgradeSuccessFrames = successFrames;
            _itemUpgradeFailureFrames = failureFrames;
        }

        public void ConfigureViciousHammer(List<IDXObject> repeatFrames, List<IDXObject> finishedFrames)
        {
            _viciousHammerRepeatFrames = repeatFrames;
            _viciousHammerFinishedFrames = finishedFrames;
        }

        public void ConfigureSkillBook(
            List<IDXObject> successFrontFrames,
            List<IDXObject> successBackFrames,
            List<IDXObject> failureFrontFrames,
            List<IDXObject> failureBackFrames)
        {
            _skillBookSuccessFrontFrames = successFrontFrames;
            _skillBookSuccessBackFrames = successBackFrames;
            _skillBookFailureFrontFrames = failureFrontFrames;
            _skillBookFailureBackFrames = failureBackFrames;
        }

        public void ConfigureVega(
            List<IDXObject> twinklingFrames,
            List<IDXObject> spellingFrames,
            List<IDXObject> arrowFrames,
            List<IDXObject> successFrames,
            List<IDXObject> failureFrames)
        {
            _vegaTwinklingFrames = twinklingFrames;
            _vegaSpellingFrames = spellingFrames;
            _vegaArrowFrames = arrowFrames;
            _vegaSuccessFrames = successFrames;
            _vegaFailureFrames = failureFrames;
        }

        public void ConfigureCashGachapon(
            List<IDXObject> normalFrames,
            List<IDXObject> jackpotFrames,
            List<IDXObject> copyNormalFrames,
            List<IDXObject> copyJackpotFrames)
        {
            _cashGachaponNormalFrames = normalFrames;
            _cashGachaponJackpotFrames = jackpotFrames;
            _cashGachaponCopyNormalFrames = copyNormalFrames;
            _cashGachaponCopyJackpotFrames = copyJackpotFrames;
        }

        public void ConfigureCube(ItemUpgradeUI.VisualThemeKind themeKind, List<IDXObject> effectFrames, Point offset)
        {
            if (effectFrames == null || effectFrames.Count == 0)
            {
                _cubeThemes.Remove(themeKind);
                return;
            }

            _cubeThemes[themeKind] = new CubeAnimationTheme(effectFrames, offset);
        }

        public void PlayItemMakeResult(bool success, int currentTimeMs)
        {
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.ItemMaker,
                "itemmake",
                success ? _itemMakeSuccessFrames : _itemMakeFailureFrames,
                Point.Zero,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs);
        }

        public void PlayItemUpgradeResult(bool success, bool enchantSkillBranch, int currentTimeMs)
        {
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.ItemUpgrade,
                "itemupgrade",
                success ? _itemUpgradeSuccessFrames : _itemUpgradeFailureFrames,
                Point.Zero,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                enchantSkillBranch ? ItemUpgradeEnchantSkillDelayMs : 0);
        }

        public void PlayCubeResult(ItemUpgradeUI.VisualThemeKind themeKind, int currentTimeMs)
        {
            if (!_cubeThemes.TryGetValue(themeKind, out CubeAnimationTheme theme))
            {
                return;
            }

            _owner.RegisterOneTime(
                MapSimulatorWindowNames.ItemUpgrade,
                $"cube:{themeKind}",
                theme.Frames,
                theme.Offset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs);
        }

        public bool TryGetCubePresentationDurationMs(ItemUpgradeUI.VisualThemeKind themeKind, out int durationMs)
        {
            durationMs = 0;
            if (!_cubeThemes.TryGetValue(themeKind, out CubeAnimationTheme theme))
            {
                return false;
            }

            int totalDurationMs = 0;
            for (int i = 0; i < theme.Frames.Count; i++)
            {
                totalDurationMs += Math.Max(1, theme.Frames[i]?.Delay ?? 0);
            }

            durationMs = Math.Max(totalDurationMs, 1);
            return true;
        }

        public void PlayViciousHammerResult(int currentTimeMs)
        {
            _owner.RegisterRepeat(
                MapSimulatorWindowNames.ItemUpgrade,
                "vicioushammer:repeat",
                _viciousHammerRepeatFrames,
                ViciousHammerRepeatOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                ViciousHammerRepeatDurationMs);
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.ItemUpgrade,
                "vicioushammer:finished",
                _viciousHammerFinishedFrames,
                ViciousHammerFinishedOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                ViciousHammerRepeatDurationMs);
        }

        public void PlaySkillBookResult(bool success, int currentTimeMs)
        {
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.Skills,
                "skillbook:back",
                success ? _skillBookSuccessBackFrames : _skillBookFailureBackFrames,
                Point.Zero,
                AnimationDisplayerWindowOverlayPass.Underlay,
                currentTimeMs);
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.Skills,
                "skillbook:front",
                success ? _skillBookSuccessFrontFrames : _skillBookFailureFrontFrames,
                Point.Zero,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs);
        }

        public void PlayVegaCasting(int currentTimeMs)
        {
            _owner.RegisterRepeat(
                MapSimulatorWindowNames.VegaSpell,
                "vega:casting",
                _vegaSpellingFrames,
                VegaCastingOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                VegaRepeatDurationMs);
        }

        public int GetVegaResultPreludeDurationMs()
        {
            return VegaRepeatDurationMs;
        }

        public void PlayVegaResultPrelude(int currentTimeMs)
        {
            _owner.RegisterRepeat(
                MapSimulatorWindowNames.VegaSpell,
                "vega:result:twinkling",
                _vegaTwinklingFrames,
                VegaResultTwinklingOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                VegaRepeatDurationMs);
            _owner.RegisterRepeat(
                MapSimulatorWindowNames.VegaSpell,
                "vega:result:arrow",
                _vegaArrowFrames,
                VegaResultArrowOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs,
                VegaRepeatDurationMs);
        }

        public void PlayVegaResultPopup(bool success, int currentTimeMs)
        {
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.VegaSpell,
                "vega:result:popup",
                success ? _vegaSuccessFrames : _vegaFailureFrames,
                success ? VegaSuccessOffset : VegaFailureOffset,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs);
        }

        public bool PlayCashGachaponResult(bool isCopyResult, bool isJackpot, int currentTimeMs)
        {
            List<IDXObject> frames = ResolveCashGachaponFrames(isCopyResult, isJackpot);
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            ClearCashGachaponTags();
            _owner.RegisterOneTime(
                MapSimulatorWindowNames.CashShopStage,
                ResolveCashGachaponTag(isCopyResult, isJackpot),
                frames,
                Point.Zero,
                AnimationDisplayerWindowOverlayPass.Overlay,
                currentTimeMs);
            return true;
        }

        public void ClearWindow(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            _owner.ClearWindow(windowName);
        }

        private readonly struct CubeAnimationTheme
        {
            public CubeAnimationTheme(List<IDXObject> frames, Point offset)
            {
                Frames = frames ?? throw new ArgumentNullException(nameof(frames));
                Offset = offset;
            }

            public List<IDXObject> Frames { get; }

            public Point Offset { get; }
        }

        private List<IDXObject> ResolveCashGachaponFrames(bool isCopyResult, bool isJackpot)
        {
            if (isCopyResult)
            {
                return isJackpot ? _cashGachaponCopyJackpotFrames : _cashGachaponCopyNormalFrames;
            }

            return isJackpot ? _cashGachaponJackpotFrames : _cashGachaponNormalFrames;
        }

        internal static string ResolveCashGachaponTag(bool isCopyResult, bool isJackpot)
        {
            if (isCopyResult)
            {
                return isJackpot ? CashGachaponCopyJackpotTag : CashGachaponCopyNormalTag;
            }

            return isJackpot ? CashGachaponJackpotTag : CashGachaponNormalTag;
        }

        private void ClearCashGachaponTags()
        {
            _owner.RemoveTag(MapSimulatorWindowNames.CashShopStage, CashGachaponNormalTag);
            _owner.RemoveTag(MapSimulatorWindowNames.CashShopStage, CashGachaponJackpotTag);
            _owner.RemoveTag(MapSimulatorWindowNames.CashShopStage, CashGachaponCopyNormalTag);
            _owner.RemoveTag(MapSimulatorWindowNames.CashShopStage, CashGachaponCopyJackpotTag);
        }
    }
}
