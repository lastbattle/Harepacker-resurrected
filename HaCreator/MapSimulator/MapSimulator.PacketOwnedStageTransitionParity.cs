using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
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

        private PacketStageTransitionCallbacks BuildPacketOwnedStageTransitionCallbacks()
        {
            return new PacketStageTransitionCallbacks
            {
                ApplyBackEffect = ApplyPacketOwnedBackEffect,
                ApplyMapObjectVisibility = ApplyPacketOwnedMapObjectVisibility,
                ClearBackEffect = ClearPacketOwnedBackEffect,
                OpenCashShop = OpenPacketOwnedCashShopStage,
                OpenItc = OpenPacketOwnedItcStage,
                QueueFieldTransfer = QueueMapTransfer
            };
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
                ApplyPacketOwnedBackAlpha(_packetStageTransitionBackEffectTargetAlpha);
                _packetStageTransitionBackEffectStartTick = int.MinValue;
                return;
            }

            float progress = Math.Clamp((currentTick - _packetStageTransitionBackEffectStartTick) / (float)durationMs, 0f, 1f);
            byte alpha = (byte)Math.Clamp(
                (int)Math.Round(_packetStageTransitionBackEffectStartAlpha
                    + ((_packetStageTransitionBackEffectTargetAlpha - _packetStageTransitionBackEffectStartAlpha) * progress)),
                byte.MinValue,
                byte.MaxValue);
            ApplyPacketOwnedBackAlpha(alpha);
            if (progress >= 1f)
            {
                _packetStageTransitionBackEffectStartTick = int.MinValue;
            }
        }

        private string OpenPacketOwnedCashShopStage()
        {
            ShowCashShopWindow();
            return "CStage::OnSetCashShop opened the Cash Shop owner and avatar preview windows.";
        }

        private string OpenPacketOwnedItcStage()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShop);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashAvatarPreview);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Mts);
            return "CStage::OnSetITC opened the ITC/MTS owner window and hid Cash Shop-owned UI.";
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
            _packetStageTransitionBackEffectStartAlpha = ResolvePacketOwnedCurrentBackAlpha();
            _packetStageTransitionBackEffectTargetAlpha = targetAlpha;
            if (_packetStageTransitionBackEffectDurationMs == 0)
            {
                ApplyPacketOwnedBackAlpha(targetAlpha);
                _packetStageTransitionBackEffectStartTick = int.MinValue;
            }

            string direction = packet.Effect == 0 ? "fade-in" : "fade-out";
            return $"CMapLoadable::OnSetBackEffect applied {direction} to back backgrounds for map {_packetStageTransitionBackEffectMapId.ToString(CultureInfo.InvariantCulture)} page {packet.PageId.ToString(CultureInfo.InvariantCulture)} over {Math.Max(0, packet.DurationMs).ToString(CultureInfo.InvariantCulture)} ms. Page-scoped alpha is approximated across all back layers.";
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

        private void ApplyPacketOwnedBackAlpha(byte alpha)
        {
            foreach (BackgroundItem background in backgrounds_back)
            {
                background?.SetAlpha(alpha);
            }
        }

        private byte ResolvePacketOwnedCurrentBackAlpha()
        {
            BackgroundItem firstBackground = backgrounds_back.FirstOrDefault();
            return firstBackground?.Color.A ?? byte.MaxValue;
        }
    }
}
