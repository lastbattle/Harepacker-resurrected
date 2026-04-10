using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int StageSystemStringPoolId = 0x17CC;
        private const string StageSystemFallbackPath = "Etc/StageSystem.img";
        private const int StageKeywordStringPoolId = 0x17CD;
        private const string StageKeywordFallbackPath = "Etc/StageKeyword.img";
        private const int StageAffectedMapStringPoolId = 0x17CE;
        private const string StageAffectedMapFallbackPath = "Etc/StageAffectedMap.img";

        private readonly ContextOwnedStagePeriodRuntime _contextOwnedStagePeriodRuntime = new();
        private readonly ContextStagePeriodPacketInboxManager _contextStagePeriodPacketInbox = new();
        private ContextOwnedStageSystemCatalog _contextOwnedStageSystemCatalog;
        private readonly Dictionary<string, ContextOwnedStageUnitEnableState> _contextOwnedStageKeywordCache = new(StringComparer.Ordinal);
        private readonly Dictionary<int, ContextOwnedStageUnitEnableState> _contextOwnedStageQuestCache = new();
        private readonly Dictionary<string, byte> _contextOwnedStagePeriodCache = new(StringComparer.Ordinal);
        private HashSet<string> _contextOwnedStageActiveKeywords = new(StringComparer.Ordinal);
        private HashSet<int> _contextOwnedStageActiveQuestIds = new();
        private HashSet<int> _contextOwnedStageAffectedMapIds = new();
        private ContextOwnedStagePeriodCatalogEntry _contextOwnedStageCurrentPeriod;
        private IReadOnlyList<ContextOwnedStageBackImageEntry> _contextOwnedStageCurrentBackImages = Array.Empty<ContextOwnedStageBackImageEntry>();
        private uint? _contextOwnedStageCurrentBackColorArgb;

        private bool TryApplyContextOwnedStagePeriodPacket(byte[] payload, out string message)
        {
            _contextOwnedStagePeriodRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return _contextOwnedStagePeriodRuntime.TryApplyPacket(
                payload,
                currTickCount,
                BuildContextOwnedStagePeriodCallbacks(),
                out message);
        }

        private ContextOwnedStagePeriodCallbacks BuildContextOwnedStagePeriodCallbacks()
        {
            return new ContextOwnedStagePeriodCallbacks
            {
                ValidateStagePeriodChange = ValidateContextOwnedStagePeriodChange,
                ApplyStagePeriodChange = ApplyContextOwnedStagePeriodChange
            };
        }

        private ContextOwnedStagePeriodValidationResult ValidateContextOwnedStagePeriodChange(PacketStagePeriodChangePacket packet)
        {
            if (!TryGetContextOwnedStageSystemCatalog(out ContextOwnedStageSystemCatalog catalog, out string error))
            {
                return ContextOwnedStagePeriodValidationResult.Rejected(error);
            }

            if (!catalog.TryGetPeriod(packet.StagePeriod, packet.Mode, out _))
            {
                return ContextOwnedStagePeriodValidationResult.Rejected(
                    $"CWvsContext::OnStageChange rejected '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} because CStageSystem::BuildCacheData has no matching period entry in {ResolveContextOwnedStageSystemPath()}.");
            }

            return ContextOwnedStagePeriodValidationResult.Accepted();
        }

        private string ApplyContextOwnedStagePeriodChange(PacketStagePeriodChangePacket packet, int currentTick)
        {
            if (!TryGetContextOwnedStageSystemCatalog(out ContextOwnedStageSystemCatalog catalog, out string catalogError))
            {
                return catalogError;
            }

            if (!catalog.TryGetPeriod(packet.StagePeriod, packet.Mode, out ContextOwnedStagePeriodCatalogEntry period))
            {
                return $"CWvsContext::OnStageChange decoded '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)}, but the simulator could not resolve the validated period entry.";
            }

            catalog.ApplyCacheData(
                period,
                _contextOwnedStageKeywordCache,
                _contextOwnedStageQuestCache,
                _contextOwnedStagePeriodCache);
            _contextOwnedStageCurrentPeriod = period;
            _contextOwnedStageCurrentBackColorArgb = period.ResolveActiveBackColorArgb();
            _contextOwnedStageCurrentBackImages = period.ResolveActiveBackImages();
            _contextOwnedStageActiveKeywords = ContextOwnedStageSystemCatalog.CaptureEnabledKeywords(_contextOwnedStageKeywordCache);
            _contextOwnedStageActiveQuestIds = ContextOwnedStageSystemCatalog.CaptureEnabledQuestIds(_contextOwnedStageQuestCache);
            _contextOwnedStageAffectedMapIds = ResolveContextOwnedStageAffectedMaps(catalog, period);

            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            if (mapId <= 0 || _mapBoard?.BoardItems == null)
            {
                return $"CWvsContext::OnStageChange cached '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} with {_contextOwnedStageCurrentBackImages.Count.ToString(CultureInfo.InvariantCulture)} stage back image(s), {_contextOwnedStageActiveKeywords.Count.ToString(CultureInfo.InvariantCulture)} keyword(s), and {_contextOwnedStageActiveQuestIds.Count.ToString(CultureInfo.InvariantCulture)} enabled quest id(s), but no live field stage was active for CStage::FadeOut -> CMapLoadable::ReloadBack -> CStage::FadeIn.";
            }

            _screenEffects?.StageTransitionFadeOut(
                PORTAL_FADE_DURATION_MS,
                currentTick,
                () =>
                {
                    ReloadContextOwnedStagePeriodBackLayers();
                    _screenEffects?.StageTransitionFadeIn(PORTAL_FADE_DURATION_MS, Environment.TickCount);
                });

            string backColorText = _contextOwnedStageCurrentBackColorArgb.HasValue
                ? $"0x{_contextOwnedStageCurrentBackColorArgb.Value:X8}"
                : "none";
            return $"CWvsContext::OnStageChange cached '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} with {_contextOwnedStageCurrentBackImages.Count.ToString(CultureInfo.InvariantCulture)} stage back image(s), backColor {backColorText}, {_contextOwnedStageActiveKeywords.Count.ToString(CultureInfo.InvariantCulture)} keyword(s), and {_contextOwnedStageActiveQuestIds.Count.ToString(CultureInfo.InvariantCulture)} enabled quest id(s), then scheduled CStage::FadeOut -> CMapLoadable::ReloadBack -> CStage::FadeIn for map {mapId.ToString(CultureInfo.InvariantCulture)} (affectedMapMatch={ShouldApplyContextOwnedStageBackData(mapId)}).";
        }

        private void ReloadContextOwnedStagePeriodBackLayers()
        {
            RestorePacketOwnedBackEffect();
            backgrounds_back.Clear();

            if (TryReloadContextOwnedStagePeriodStageBackLayers())
            {
                _backgroundsBackArray = backgrounds_back.ToArray();
                RefreshMobRenderArray();
                return;
            }

            if (_mapBoard?.BoardItems?.BackBackgrounds == null)
            {
                _backgroundsBackArray = Array.Empty<BackgroundItem>();
                RefreshMobRenderArray();
                return;
            }

            foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
            {
                TryAppendContextOwnedStageBackground(background, background?.BaseInfo?.ParentObject as WzImageProperty);
            }

            _backgroundsBackArray = backgrounds_back.ToArray();
            RefreshMobRenderArray();
        }

        private bool TryReloadContextOwnedStagePeriodStageBackLayers()
        {
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            if (!ShouldApplyContextOwnedStageBackData(mapId) || _contextOwnedStageCurrentBackImages.Count == 0)
            {
                return false;
            }

            int zOrder = 1;
            foreach (ContextOwnedStageBackImageEntry entry in _contextOwnedStageCurrentBackImages)
            {
                BackgroundInfo backgroundInfo = BackgroundInfo.Get(_DxDeviceManager?.GraphicsDevice, entry.BackgroundSet, entry.InfoType, entry.Number);
                if (backgroundInfo?.ParentObject is not WzImageProperty sourceProperty)
                {
                    continue;
                }

                BackgroundInstance backgroundInstance = (BackgroundInstance)backgroundInfo.CreateInstance(
                    _mapBoard,
                    entry.X,
                    entry.Y,
                    entry.Z != 0 ? entry.Z : zOrder++,
                    entry.Rx,
                    entry.Ry,
                    entry.Cx,
                    entry.Cy,
                    entry.Type,
                    entry.Alpha,
                    entry.Front,
                    entry.Flip,
                    entry.Page,
                    entry.ScreenMode,
                    entry.SpineAnimation,
                    entry.SpineRandomStart);
                TryAppendContextOwnedStageBackground(backgroundInstance, sourceProperty);
            }

            return backgrounds_back.Count > 0;
        }

        private void TryAppendContextOwnedStageBackground(BackgroundInstance background, WzImageProperty sourceProperty)
        {
            if (background == null || sourceProperty == null)
            {
                return;
            }

            ConcurrentBag<WzObject> usedProps = new();
            BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(
                _texturePool,
                sourceProperty,
                background,
                _DxDeviceManager.GraphicsDevice,
                usedProps,
                background.Flip);
            if (bgItem != null)
            {
                backgrounds_back.Add(bgItem);
            }
        }

        internal Color ResolveContextOwnedStageBackClearColor()
        {
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            if (!ShouldApplyContextOwnedStageBackData(mapId) || !_contextOwnedStageCurrentBackColorArgb.HasValue)
            {
                return Color.Black;
            }

            return ContextOwnedStagePeriodColorHelper.Resolve(_contextOwnedStageCurrentBackColorArgb.Value);
        }

        private bool ShouldApplyContextOwnedStageBackData(int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            return _contextOwnedStageAffectedMapIds.Count == 0 || _contextOwnedStageAffectedMapIds.Contains(mapId);
        }

        private HashSet<int> ResolveContextOwnedStageAffectedMaps(
            ContextOwnedStageSystemCatalog catalog,
            ContextOwnedStagePeriodCatalogEntry period)
        {
            Func<int, MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType> questStateProvider = _questRuntime != null
                ? _questRuntime.GetCurrentState
                : null;
            return catalog?.ResolveAffectedMaps(period, questStateProvider) ?? new HashSet<int>();
        }

        private void RefreshContextOwnedStagePeriodQuestStateGates()
        {
            if (_contextOwnedStageCurrentPeriod == null
                || !TryGetContextOwnedStageSystemCatalog(out ContextOwnedStageSystemCatalog catalog, out _))
            {
                return;
            }

            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            bool appliedBeforeRefresh = ShouldApplyContextOwnedStageBackData(mapId);
            HashSet<int> refreshedAffectedMaps = ResolveContextOwnedStageAffectedMaps(catalog, _contextOwnedStageCurrentPeriod);
            if (_contextOwnedStageAffectedMapIds.SetEquals(refreshedAffectedMaps))
            {
                return;
            }

            _contextOwnedStageAffectedMapIds = refreshedAffectedMaps;
            if (mapId > 0 && appliedBeforeRefresh != ShouldApplyContextOwnedStageBackData(mapId))
            {
                ReloadContextOwnedStagePeriodBackLayers();
            }
        }

        private string DescribeContextOwnedStagePeriodStatus()
        {
            _contextOwnedStagePeriodRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            string backColorText = _contextOwnedStageCurrentBackColorArgb.HasValue
                ? $"0x{_contextOwnedStageCurrentBackColorArgb.Value:X8}"
                : "none";
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            string cacheSummary = $"stageBacks={_contextOwnedStageCurrentBackImages.Count.ToString(CultureInfo.InvariantCulture)} backColor={backColorText} keywords={_contextOwnedStageActiveKeywords.Count.ToString(CultureInfo.InvariantCulture)} quests={_contextOwnedStageActiveQuestIds.Count.ToString(CultureInfo.InvariantCulture)} affectedMaps={_contextOwnedStageAffectedMapIds.Count.ToString(CultureInfo.InvariantCulture)} themeModes={_contextOwnedStagePeriodCache.Count.ToString(CultureInfo.InvariantCulture)} applyToCurrentMap={ShouldApplyContextOwnedStageBackData(mapId)}";
            return $"{_contextOwnedStagePeriodRuntime.DescribeStatus()}{Environment.NewLine}{cacheSummary}{Environment.NewLine}{_contextStagePeriodPacketInbox.LastStatus}";
        }

        private void EnsureContextOwnedStagePeriodInboxState(bool shouldRun)
        {
            if (!shouldRun)
            {
                if (_contextStagePeriodPacketInbox.IsRunning)
                {
                    _contextStagePeriodPacketInbox.Stop();
                }

                return;
            }

            if (_contextStagePeriodPacketInbox.IsRunning)
            {
                return;
            }

            try
            {
                _contextStagePeriodPacketInbox.Start();
            }
            catch (Exception ex)
            {
                _contextStagePeriodPacketInbox.Stop();
                _chat?.AddErrorMessage($"Context-owned stage-period inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainContextOwnedStagePeriodInbox()
        {
            while (_contextStagePeriodPacketInbox.TryDequeue(out ContextStagePeriodPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyContextOwnedStagePeriodPacket(message.Payload, out string detail);
                _contextStagePeriodPacketInbox.RecordDispatchResult(message, applied, detail);
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

                if (applied)
                {
                    _chat?.AddSystemMessage(detail, currTickCount);
                }
                else
                {
                    _chat?.AddErrorMessage(detail, currTickCount);
                }
            }
        }

        private bool TryGetContextOwnedStageSystemCatalog(out ContextOwnedStageSystemCatalog catalog, out string error)
        {
            catalog = _contextOwnedStageSystemCatalog;
            if (catalog != null)
            {
                error = null;
                return true;
            }

            catalog = ContextOwnedStageSystemCatalog.Build(
                ResolveContextOwnedStageSystemPath(),
                ResolveContextOwnedStageKeywordPath(),
                ResolveContextOwnedStageAffectedMapPath(),
                out error);
            if (catalog == null)
            {
                return false;
            }

            _contextOwnedStageSystemCatalog = catalog;
            return true;
        }

        private static string ResolveContextOwnedStageSystemPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageSystemStringPoolId, StageSystemFallbackPath);
        }

        private static string ResolveContextOwnedStageKeywordPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageKeywordStringPoolId, StageKeywordFallbackPath);
        }

        private static string ResolveContextOwnedStageAffectedMapPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageAffectedMapStringPoolId, StageAffectedMapFallbackPath);
        }
    }
}
