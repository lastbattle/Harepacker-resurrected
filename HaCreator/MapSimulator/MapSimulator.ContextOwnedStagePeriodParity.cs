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

        private readonly ContextOwnedStagePeriodRuntime _contextOwnedStagePeriodRuntime = new();
        private readonly ContextStagePeriodPacketInboxManager _contextStagePeriodPacketInbox = new();
        private Dictionary<string, HashSet<byte>> _contextOwnedStageThemeModes;

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
            if (!TryGetContextOwnedStageThemeModes(out Dictionary<string, HashSet<byte>> stageThemeModes, out string error))
            {
                return ContextOwnedStagePeriodValidationResult.Rejected(error);
            }

            if (!stageThemeModes.TryGetValue(packet.StagePeriod, out HashSet<byte> validModes))
            {
                return ContextOwnedStagePeriodValidationResult.Rejected(
                    $"CWvsContext::OnStageChange rejected '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} because the client stage-system cache does not contain that stage theme in {ResolveContextOwnedStageSystemPath()}.");
            }

            if (!validModes.Contains(packet.Mode))
            {
                return ContextOwnedStagePeriodValidationResult.Rejected(
                    $"CWvsContext::OnStageChange rejected '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} because CStageSystem::BuildCacheData has no matching period entry for that stage theme.");
            }

            return ContextOwnedStagePeriodValidationResult.Accepted();
        }

        private string ApplyContextOwnedStagePeriodChange(PacketStagePeriodChangePacket packet, int currentTick)
        {
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            if (mapId <= 0 || _mapBoard?.BoardItems == null)
            {
                return $"CWvsContext::OnStageChange cached '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)}, but no live field stage was active for CStage::FadeOut -> CMapLoadable::ReloadBack -> CStage::FadeIn.";
            }

            int authoredBackCount = _mapBoard.BoardItems.BackBackgrounds?.Count ?? 0;
            _screenEffects?.StageTransitionFadeOut(
                PORTAL_FADE_DURATION_MS,
                currentTick,
                () =>
                {
                    ReloadContextOwnedStagePeriodBackLayers();
                    _screenEffects?.StageTransitionFadeIn(PORTAL_FADE_DURATION_MS, Environment.TickCount);
                });

            return $"CWvsContext::OnStageChange cached '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} and scheduled CStage::FadeOut -> CMapLoadable::ReloadBack -> CStage::FadeIn for {authoredBackCount.ToString(CultureInfo.InvariantCulture)} authored back background(s) on map {mapId.ToString(CultureInfo.InvariantCulture)}.";
        }

        private void ReloadContextOwnedStagePeriodBackLayers()
        {
            RestorePacketOwnedBackEffect();
            backgrounds_back.Clear();

            if (_mapBoard?.BoardItems?.BackBackgrounds == null)
            {
                _backgroundsBackArray = Array.Empty<BackgroundItem>();
                RefreshMobRenderArray();
                return;
            }

            ConcurrentBag<WzObject> usedProps = new();
            foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
            {
                if (background?.BaseInfo?.ParentObject is not WzImageProperty bgParent)
                {
                    continue;
                }

                BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(
                    _texturePool,
                    bgParent,
                    background,
                    _DxDeviceManager.GraphicsDevice,
                    usedProps,
                    background.Flip);
                if (bgItem != null)
                {
                    backgrounds_back.Add(bgItem);
                }
            }

            _backgroundsBackArray = backgrounds_back.ToArray();
            RefreshMobRenderArray();
        }

        private string DescribeContextOwnedStagePeriodStatus()
        {
            _contextOwnedStagePeriodRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return $"{_contextOwnedStagePeriodRuntime.DescribeStatus()}{Environment.NewLine}{_contextStagePeriodPacketInbox.LastStatus}";
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

        private bool TryGetContextOwnedStageThemeModes(out Dictionary<string, HashSet<byte>> stageThemeModes, out string error)
        {
            stageThemeModes = _contextOwnedStageThemeModes;
            if (stageThemeModes != null)
            {
                error = null;
                return true;
            }

            stageThemeModes = BuildContextOwnedStageThemeModes(out error);
            if (stageThemeModes == null)
            {
                return false;
            }

            _contextOwnedStageThemeModes = stageThemeModes;
            return true;
        }

        private static Dictionary<string, HashSet<byte>> BuildContextOwnedStageThemeModes(out string error)
        {
            error = null;
            string stageSystemPath = ResolveContextOwnedStageSystemPath();
            if (!TrySplitContextOwnedStageSystemPath(stageSystemPath, out string category, out string imageName))
            {
                error = $"Context-owned stage-period validation could not resolve the client stage-system path '{stageSystemPath}'.";
                return null;
            }

            WzImage stageSystemImage = Program.FindImage(category, imageName);
            if (stageSystemImage == null)
            {
                error = $"Context-owned stage-period validation could not load {stageSystemPath}, so the simulator cannot mirror CStageSystem::BuildCacheData acceptance yet.";
                return null;
            }

            stageSystemImage.ParseImage();
            Dictionary<string, HashSet<byte>> stageThemeModes = new(StringComparer.Ordinal);
            foreach (WzImageProperty property in stageSystemImage.WzProperties.OfType<WzImageProperty>())
            {
                HashSet<byte> modes = CollectContextOwnedStageThemeModes(property);
                if (modes.Count > 0)
                {
                    stageThemeModes[property.Name] = modes;
                }
            }

            if (stageThemeModes.Count == 0)
            {
                error = $"Context-owned stage-period validation loaded {stageSystemPath}, but no client stage themes with numeric period entries were discovered.";
                return null;
            }

            return stageThemeModes;
        }

        private static HashSet<byte> CollectContextOwnedStageThemeModes(WzImageProperty themeProperty)
        {
            HashSet<byte> modes = new();
            AppendNumericContextOwnedStageThemeModes(themeProperty, modes);

            if (themeProperty["stageList"] is WzImageProperty stageList)
            {
                AppendNumericContextOwnedStageThemeModes(stageList, modes);
            }

            foreach (WzImageProperty child in themeProperty.WzProperties.OfType<WzImageProperty>())
            {
                if (child.Name.EndsWith("List", StringComparison.OrdinalIgnoreCase)
                    || child.Name.IndexOf("stage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppendNumericContextOwnedStageThemeModes(child, modes);
                }
            }

            return modes;
        }

        private static void AppendNumericContextOwnedStageThemeModes(WzImageProperty property, HashSet<byte> modes)
        {
            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out byte mode))
                {
                    modes.Add(mode);
                }
            }
        }

        private static string ResolveContextOwnedStageSystemPath()
        {
            return MapleStoryStringPool.GetOrFallback(StageSystemStringPoolId, StageSystemFallbackPath);
        }

        private static bool TrySplitContextOwnedStageSystemPath(string path, out string category, out string imageName)
        {
            category = null;
            imageName = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            category = parts[0];
            imageName = parts[^1];
            return !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(imageName);
        }
    }
}
