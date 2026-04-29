using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render.DX;
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
        private const int StageSystemStringPoolId = 0x17BF;
        private const string StageSystemFallbackPath = "Etc/StageSystem.img";
        private const int StageKeywordStringPoolId = 0x17C0;
        private const string StageKeywordFallbackPath = "Etc/StageKeyword.img";
        private const int StageAffectedMapStringPoolId = 0x17BE;
        private const string StageAffectedMapFallbackPath = "Etc/StageAffectedMap.img";

        private readonly ContextOwnedStagePeriodRuntime _contextOwnedStagePeriodRuntime = new();
        private readonly ContextStagePeriodPacketInboxManager _contextStagePeriodPacketInbox = new();
        private ContextOwnedStageSystemCatalog _contextOwnedStageSystemCatalog;
        private readonly Dictionary<string, ContextOwnedStageUnitEnableState> _contextOwnedStageKeywordCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ContextOwnedStageUnitEnableState> _contextOwnedStageQuestCache = new();
        private readonly Dictionary<string, byte> _contextOwnedStagePeriodCache = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _contextOwnedStageActiveKeywords = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<int> _contextOwnedStageActiveQuestIds = new();
        private HashSet<int> _contextOwnedStageAffectedMapIds = new();
        private ContextOwnedStagePeriodCatalogEntry _contextOwnedStageCurrentPeriod;
        private IReadOnlyList<ContextOwnedStageBackImageEntry> _contextOwnedStageCurrentBackImages = Array.Empty<ContextOwnedStageBackImageEntry>();
        private uint? _contextOwnedStageCurrentBackColorArgb;
        private int _contextOwnedStagePeriodStartTick;

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
                IsStagePeriodCurrent = IsContextOwnedStagePeriodAlreadyCurrent,
                ValidateStagePeriodChange = ValidateContextOwnedStagePeriodChange,
                ApplyStagePeriodChange = ApplyContextOwnedStagePeriodChange
            };
        }

        private bool IsContextOwnedStagePeriodAlreadyCurrent(PacketStagePeriodChangePacket packet)
        {
            string stagePeriod = string.IsNullOrWhiteSpace(packet.StagePeriod)
                ? null
                : packet.StagePeriod.Trim();
            return stagePeriod != null
                && _contextOwnedStagePeriodCache.TryGetValue(stagePeriod, out byte cachedMode)
                && cachedMode == packet.Mode;
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
            _contextOwnedStagePeriodStartTick = currentTick;
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
                    _screenEffects?.StageTransitionFadeIn(
                        PORTAL_FADE_DURATION_MS,
                        _screenEffects.LastFadeCompletionTimeMs);
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
            backgrounds_front.Clear();

            if (TryReloadContextOwnedStagePeriodStageBackLayers())
            {
                _backgroundsBackArray = backgrounds_back.ToArray();
                _backgroundsFrontArray = backgrounds_front.ToArray();
                RefreshMobRenderArray();
                return;
            }

            if (_mapBoard?.BoardItems == null)
            {
                _backgroundsBackArray = Array.Empty<BackgroundItem>();
                _backgroundsFrontArray = Array.Empty<BackgroundItem>();
                RefreshMobRenderArray();
                return;
            }

            foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds ?? Enumerable.Empty<BackgroundInstance>())
            {
                TryAppendContextOwnedStageBackground(background, background?.BaseInfo?.ParentObject as WzImageProperty);
            }

            foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds ?? Enumerable.Empty<BackgroundInstance>())
            {
                TryAppendContextOwnedStageBackground(background, background?.BaseInfo?.ParentObject as WzImageProperty);
            }

            _backgroundsBackArray = backgrounds_back.ToArray();
            _backgroundsFrontArray = backgrounds_front.ToArray();
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
                BackgroundInfo backgroundInfo = ResolveContextOwnedStageBackInfo(entry);
                if (backgroundInfo?.ParentObject is not WzImageProperty sourceProperty)
                {
                    continue;
                }

                ContextOwnedStageBackImageEntry resolvedEntry =
                    ContextOwnedStageSystemCatalog.ResolveClientMakeBackPieceFields(
                        entry,
                        sourceProperty,
                        backgroundInfo.Type);
                if (!ShouldRenderContextOwnedStageBackForCurrentScreen(resolvedEntry.ScreenMode))
                {
                    continue;
                }

                BackgroundInstance backgroundInstance = (BackgroundInstance)backgroundInfo.CreateInstance(
                    _mapBoard,
                    resolvedEntry.X,
                    resolvedEntry.Y,
                    resolvedEntry.Z != 0 ? resolvedEntry.Z : zOrder++,
                    resolvedEntry.Rx,
                    resolvedEntry.Ry,
                    resolvedEntry.Cx,
                    resolvedEntry.Cy,
                    resolvedEntry.Type,
                    resolvedEntry.Alpha,
                    resolvedEntry.Front,
                    resolvedEntry.Flip,
                    resolvedEntry.Page,
                    resolvedEntry.ScreenMode,
                    resolvedEntry.SpineAnimation,
                    resolvedEntry.SpineRandomStart);
                TryAppendContextOwnedStageBackground(backgroundInstance, sourceProperty);
            }

            return backgrounds_back.Count > 0 || backgrounds_front.Count > 0;
        }

        private bool ShouldRenderContextOwnedStageBackForCurrentScreen(int screenMode)
        {
            return ShouldRenderContextOwnedStageBackForScreenMode(
                screenMode,
                IsContextOwnedStagePeriodLargeScreenMode(_renderParams.Resolution));
        }

        internal static bool ShouldRenderContextOwnedStageBackForScreenMode(int screenMode, bool isLargeScreen)
        {
            return screenMode switch
            {
                1 => !isLargeScreen,
                2 => isLargeScreen,
                _ => true
            };
        }

        internal static bool IsContextOwnedStagePeriodLargeScreenMode(RenderResolution resolution)
        {
            return resolution != RenderResolution.Res_All
                && resolution != RenderResolution.Res_800x600;
        }

        private BackgroundInfo ResolveContextOwnedStageBackInfo(ContextOwnedStageBackImageEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            foreach (BackgroundInfoType infoType in ContextOwnedStageSystemCatalog.ResolveClientMakeBackInfoTypeLookupOrder(entry))
            {
                if (!ContextOwnedStageBackPieceExists(entry.BackgroundSet, infoType, entry.Number))
                {
                    continue;
                }

                BackgroundInfo backgroundInfo = BackgroundInfo.Get(
                    _DxDeviceManager?.GraphicsDevice,
                    entry.BackgroundSet,
                    infoType,
                    entry.Number);
                if (backgroundInfo != null)
                {
                    return backgroundInfo;
                }
            }

            return null;
        }

        private static bool ContextOwnedStageBackPieceExists(string backgroundSet, BackgroundInfoType infoType, string number)
        {
            if (string.IsNullOrWhiteSpace(backgroundSet) || string.IsNullOrWhiteSpace(number))
            {
                return false;
            }

            WzImage backgroundSetImage = Program.InfoManager.GetBackgroundSet(backgroundSet);
            return backgroundSetImage?[infoType.ToPropertyString()]?[number] != null;
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
                if (background.front)
                {
                    backgrounds_front.Add(bgItem);
                }
                else
                {
                    backgrounds_back.Add(bgItem);
                }
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
            return catalog?.ResolveAffectedMaps(
                period,
                questStateProvider,
                CalculateContextOwnedStagePeriodElapsedMilliseconds(currTickCount, _contextOwnedStagePeriodStartTick)) ?? new HashSet<int>();
        }

        internal static int CalculateContextOwnedStagePeriodElapsedMilliseconds(int currentTick, int startTick)
        {
            // Mirror client-like tick arithmetic across GetTickCount/Environment.TickCount wrap.
            uint elapsed = unchecked((uint)currentTick - (uint)startTick);
            return elapsed > int.MaxValue
                ? int.MaxValue
                : (int)elapsed;
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
            string cacheSummary = $"stageBacks={_contextOwnedStageCurrentBackImages.Count.ToString(CultureInfo.InvariantCulture)} backColor={backColorText} keywords={_contextOwnedStageActiveKeywords.Count.ToString(CultureInfo.InvariantCulture)} quests={_contextOwnedStageActiveQuestIds.Count.ToString(CultureInfo.InvariantCulture)} affectedMaps={_contextOwnedStageAffectedMapIds.Count.ToString(CultureInfo.InvariantCulture)} themeModes={_contextOwnedStagePeriodCache.Count.ToString(CultureInfo.InvariantCulture)} elapsedMs={CalculateContextOwnedStagePeriodElapsedMilliseconds(currTickCount, _contextOwnedStagePeriodStartTick).ToString(CultureInfo.InvariantCulture)} applyToCurrentMap={ShouldApplyContextOwnedStageBackData(mapId)}";
            string inboxSummary = "stageperiod inbox adapter-only; listener fallback retired.";
            return $"{_contextOwnedStagePeriodRuntime.DescribeStatus()}{Environment.NewLine}{cacheSummary}{Environment.NewLine}{inboxSummary}{Environment.NewLine}{_contextStagePeriodPacketInbox.LastStatus}";
        }

        private void EnsureContextOwnedStagePeriodInboxState(bool shouldRun)
        {
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

        internal static string ResolveContextOwnedStageSystemPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageSystemStringPoolId, StageSystemFallbackPath);
        }

        internal static string ResolveContextOwnedStageKeywordPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageKeywordStringPoolId, StageKeywordFallbackPath);
        }

        internal static string ResolveContextOwnedStageAffectedMapPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageAffectedMapStringPoolId, StageAffectedMapFallbackPath);
        }
    }
}
