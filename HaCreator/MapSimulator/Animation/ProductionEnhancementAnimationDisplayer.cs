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

        public void ClearWindow(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            _owner.ClearWindow(windowName);
        }
    }
}
