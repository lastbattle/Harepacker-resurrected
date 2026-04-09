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

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly ContextOwnedStagePeriodRuntime _contextOwnedStagePeriodRuntime = new();
        private readonly ContextStagePeriodPacketInboxManager _contextStagePeriodPacketInbox = new();

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
                ApplyStagePeriodChange = ApplyContextOwnedStagePeriodChange
            };
        }

        private string ApplyContextOwnedStagePeriodChange(PacketStagePeriodChangePacket packet, int currentTick)
        {
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            if (mapId <= 0 || _mapBoard?.BoardItems == null)
            {
                return $"CWvsContext::OnStageChange cached '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)}, but no live field stage was active for CStage::FadeOut -> CMapLoadable::ReloadBack -> CStage::FadeIn.";
            }

            int authoredBackCount = _mapBoard.BoardItems.BackBackgrounds?.Count ?? 0;
            _screenEffects?.FadeOut(
                PORTAL_FADE_DURATION_MS,
                currentTick,
                () =>
                {
                    ReloadContextOwnedStagePeriodBackLayers();
                    _screenEffects?.FadeIn(PORTAL_FADE_DURATION_MS, Environment.TickCount);
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
    }
}
