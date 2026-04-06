using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketStageTransitionRuntime _packetStageTransitionRuntime = new();
        private readonly StageTransitionPacketInboxManager _stageTransitionPacketInbox = new();
        private readonly Dictionary<string, List<BaseDXDrawableItem>> _packetStageTransitionNamedObjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BaseDXDrawableItem, bool> _packetStageTransitionObjectVisibility = new();
        private int _packetStageTransitionBackEffectStartTick = int.MinValue;
        private int _packetStageTransitionBackEffectDurationMs;
        private byte _packetStageTransitionBackEffectStartAlpha = byte.MaxValue;
        private byte _packetStageTransitionBackEffectTargetAlpha = byte.MaxValue;
        private int _packetStageTransitionBackEffectMapId;
        private byte _packetStageTransitionBackEffectPageId;

        private bool TryApplyPacketOwnedStageTransitionPacket(int packetType, byte[] payload, out string message)
        {
            _packetStageTransitionRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return _packetStageTransitionRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                BuildPacketOwnedStageTransitionCallbacks(),
                out message);
        }

        private bool TryRelayLoginOwnedStageTransitionPacket(LoginPacketType packetType, string[] args, out string message)
        {
            message = null;
            if (!TryResolveLoginOwnedStageTransitionPacketType(packetType, out int stagePacketType))
            {
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (stagePacketType is not 142 and not 143 and not 146)
            {
                string payloadError = null;
                foreach (string arg in args ?? Array.Empty<string>())
                {
                    if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string candidateError))
                    {
                        payload = payloadBytes;
                        payloadError = null;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(candidateError))
                    {
                        payloadError = candidateError;
                    }
                }

                if (payload == null)
                {
                    message = $"CLogin::OnPacket forwarded {packetType}, but the relay payload was missing or invalid. {payloadError ?? "Stage-transition payloads must use payloadhex=.. or payloadb64=.."}";
                    return true;
                }
            }

            bool applied = TryApplyPacketOwnedStageTransitionPacket(stagePacketType, payload, out string detail);
            message = string.IsNullOrWhiteSpace(detail)
                ? $"CLogin::OnPacket forwarded {packetType} to the stage-transition runtime."
                : $"CLogin::OnPacket forwarded {packetType} to the stage-transition runtime. {detail}";
            return true;
        }

        private static bool TryResolveLoginOwnedStageTransitionPacketType(LoginPacketType packetType, out int stagePacketType)
        {
            stagePacketType = packetType switch
            {
                LoginPacketType.SetField => 141,
                LoginPacketType.SetITC => 142,
                LoginPacketType.SetCashShop => 143,
                LoginPacketType.SetBackEffect => 144,
                LoginPacketType.SetMapObjectVisible => 145,
                LoginPacketType.ClearBackEffect => 146,
                _ => 0
            };
            return stagePacketType != 0;
        }

        private PacketStageTransitionCallbacks BuildPacketOwnedStageTransitionCallbacks()
        {
            return new PacketStageTransitionCallbacks
            {
                ApplyBackEffect = ApplyPacketOwnedBackEffect,
                ApplyMapObjectVisibility = ApplyPacketOwnedMapObjectVisibility,
                ClearBackEffect = ClearPacketOwnedBackEffect,
                OpenCashShop = OpenPacketOwnedCashShopStage,
                OpenItc = OpenPacketOwnedItcStage,
                QueueFieldTransfer = QueuePacketOwnedFieldTransfer
            };
        }

        private bool QueuePacketOwnedFieldTransfer(PacketStageFieldTransferRequest request)
        {
            if (request.MapId <= 0)
            {
                return false;
            }

            return QueueMapTransfer(request.MapId, request.PortalName, request.PortalIndex);
        }

        private void RegisterPacketOwnedStageTransitionObject(BaseDXDrawableItem mapItem, ObjectInstance objInst)
        {
            if (mapItem == null || objInst == null)
            {
                return;
            }

            string objectName = objInst.Name?.Trim();
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            if (!_packetStageTransitionNamedObjects.TryGetValue(objectName, out List<BaseDXDrawableItem> items))
            {
                items = new List<BaseDXDrawableItem>();
                _packetStageTransitionNamedObjects[objectName] = items;
            }

            items.Add(mapItem);
        }

        private void BindPacketOwnedStageTransitionMapState()
        {
            ResetPacketOwnedStageTransitionRuntimeState();
            _packetStageTransitionRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
        }

        private void ClearPacketOwnedStageTransitionState()
        {
            ResetPacketOwnedStageTransitionRuntimeState();
            _packetStageTransitionNamedObjects.Clear();
        }

        private void ResetPacketOwnedStageTransitionRuntimeState()
        {
            _packetStageTransitionObjectVisibility.Clear();
            RestorePacketOwnedBackEffect();
            _packetStageTransitionRuntime.Clear();
        }

        private void UpdatePacketOwnedStageTransitionState(int currentTick)
        {
            if (_packetStageTransitionBackEffectStartTick == int.MinValue)
            {
                return;
            }

            int durationMs = Math.Max(0, _packetStageTransitionBackEffectDurationMs);
            if (durationMs == 0)
            {
                ApplyPacketOwnedBackAlpha(_packetStageTransitionBackEffectTargetAlpha, _packetStageTransitionBackEffectPageId);
                _packetStageTransitionBackEffectStartTick = int.MinValue;
                return;
            }

            float progress = Math.Clamp((currentTick - _packetStageTransitionBackEffectStartTick) / (float)durationMs, 0f, 1f);
            byte alpha = (byte)Math.Clamp(
                (int)Math.Round(_packetStageTransitionBackEffectStartAlpha
                    + ((_packetStageTransitionBackEffectTargetAlpha - _packetStageTransitionBackEffectStartAlpha) * progress)),
                byte.MinValue,
                byte.MaxValue);
            ApplyPacketOwnedBackAlpha(alpha, _packetStageTransitionBackEffectPageId);
            if (progress >= 1f)
            {
                _packetStageTransitionBackEffectStartTick = int.MinValue;
            }
        }

        private string OpenPacketOwnedCashShopStage()
        {
            ShowCashShopWindow();
            return "CStage::OnSetCashShop opened the Cash Shop stage, child owners, and avatar preview windows.";
        }

        private string OpenPacketOwnedItcStage()
        {
            OpenCashServiceOwnerFamily(UI.CashServiceStageKind.ItemTradingCenter, resetStageSession: true);
            return "CStage::OnSetITC opened the ITC stage owner and hid Cash Shop-owned UI.";
        }

        private string ApplyPacketOwnedBackEffect(PacketBackEffectPacket packet, int currentTick)
        {
            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
            if (packet.FieldId > 0 && currentMapId > 0 && packet.FieldId != currentMapId)
            {
                return $"Ignored CMapLoadable::OnSetBackEffect for map {packet.FieldId.ToString(CultureInfo.InvariantCulture)} while bound to map {currentMapId.ToString(CultureInfo.InvariantCulture)}.";
            }

            if (backgrounds_back.Count == 0)
            {
                return "CMapLoadable::OnSetBackEffect routed, but the current map has no back backgrounds to fade.";
            }

            IReadOnlyList<BackgroundItem> targets = PacketStageTransitionBackEffectPageResolver.SelectTargets(backgrounds_back, packet.PageId);
            if (targets.Count == 0)
            {
                RestorePacketOwnedBackEffect();
                return $"CMapLoadable::OnSetBackEffect targeted page {packet.PageId.ToString(CultureInfo.InvariantCulture)}, but the current map has no authored back backgrounds on that page.";
            }

            byte targetAlpha = packet.Effect switch
            {
                0 => byte.MaxValue,
                1 => byte.MinValue,
                _ => byte.MaxValue
            };

            if (packet.Effect is not 0 and not 1)
            {
                RestorePacketOwnedBackEffect();
                return $"CMapLoadable::OnSetBackEffect effect {packet.Effect.ToString(CultureInfo.InvariantCulture)} is not modeled; restored authored back-background alpha.";
            }

            _packetStageTransitionBackEffectMapId = currentMapId;
            _packetStageTransitionBackEffectPageId = packet.PageId;
            _packetStageTransitionBackEffectStartTick = currentTick;
            _packetStageTransitionBackEffectDurationMs = Math.Max(0, packet.DurationMs);
            _packetStageTransitionBackEffectStartAlpha = ResolvePacketOwnedCurrentBackAlpha(targets);
            _packetStageTransitionBackEffectTargetAlpha = targetAlpha;
            if (_packetStageTransitionBackEffectDurationMs == 0)
            {
                ApplyPacketOwnedBackAlpha(targetAlpha, _packetStageTransitionBackEffectPageId);
                _packetStageTransitionBackEffectStartTick = int.MinValue;
            }

            string direction = packet.Effect == 0 ? "fade-in" : "fade-out";
            return $"CMapLoadable::OnSetBackEffect applied {direction} to {targets.Count.ToString(CultureInfo.InvariantCulture)} back background(s) for map {_packetStageTransitionBackEffectMapId.ToString(CultureInfo.InvariantCulture)} page {packet.PageId.ToString(CultureInfo.InvariantCulture)} over {Math.Max(0, packet.DurationMs).ToString(CultureInfo.InvariantCulture)} ms.";
        }

        private string ClearPacketOwnedBackEffect()
        {
            RestorePacketOwnedBackEffect();
            return "CMapLoadable::OnClearBackEffect restored authored back-background alpha.";
        }

        private int ApplyPacketOwnedMapObjectVisibility(string name, bool visible)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            if (!_packetStageTransitionNamedObjects.TryGetValue(name.Trim(), out List<BaseDXDrawableItem> objects))
            {
                return 0;
            }

            int applied = 0;
            foreach (BaseDXDrawableItem mapObject in objects)
            {
                if (mapObject == null)
                {
                    continue;
                }

                _packetStageTransitionObjectVisibility[mapObject] = visible;
                applied++;
            }

            return applied;
        }

        private void RestorePacketOwnedBackEffect()
        {
            _packetStageTransitionBackEffectStartTick = int.MinValue;
            _packetStageTransitionBackEffectDurationMs = 0;
            _packetStageTransitionBackEffectStartAlpha = byte.MaxValue;
            _packetStageTransitionBackEffectTargetAlpha = byte.MaxValue;
            _packetStageTransitionBackEffectMapId = 0;
            _packetStageTransitionBackEffectPageId = 0;

            foreach (BackgroundItem background in backgrounds_back)
            {
                if (background == null)
                {
                    continue;
                }

                background.SetAlpha(background.DefaultAlpha);
            }
        }

        private void ApplyPacketOwnedBackAlpha(byte alpha, int pageId)
        {
            foreach (BackgroundItem background in PacketStageTransitionBackEffectPageResolver.SelectTargets(backgrounds_back, pageId))
            {
                background?.SetAlpha(alpha);
            }
        }

        private byte ResolvePacketOwnedCurrentBackAlpha(IReadOnlyList<BackgroundItem> targets)
        {
            return PacketStageTransitionBackEffectPageResolver.ResolveCurrentAlpha(targets);
        }

        private string DescribePacketOwnedStageTransitionStatus()
        {
            _packetStageTransitionRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return $"{_packetStageTransitionRuntime.DescribeStatus()}{Environment.NewLine}{_stageTransitionPacketInbox.LastStatus}";
        }

        private void EnsureStageTransitionPacketInboxState(bool shouldRun)
        {
            if (!shouldRun)
            {
                if (_stageTransitionPacketInbox.IsRunning)
                {
                    _stageTransitionPacketInbox.Stop();
                }

                return;
            }

            if (_stageTransitionPacketInbox.IsRunning)
            {
                return;
            }

            try
            {
                _stageTransitionPacketInbox.Start();
            }
            catch (Exception ex)
            {
                _stageTransitionPacketInbox.Stop();
                _chat?.AddErrorMessage($"Stage-transition packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainStageTransitionPacketInbox()
        {
            while (_stageTransitionPacketInbox.TryDequeue(out StageTransitionPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedStageTransitionPacket(message.PacketType, message.Payload, out string detail);
                _stageTransitionPacketInbox.RecordDispatchResult(message, applied, detail);
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
