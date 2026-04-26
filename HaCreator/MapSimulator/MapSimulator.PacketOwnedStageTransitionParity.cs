using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
            if (ShouldConsumeSharedExclusiveRequestStateFromStageTransitionPacketType(packetType))
            {
                // Stage transition response owners clear CWvsContext shared
                // transfer/portal exclusive-request sent state.
                ConsumeSharedExclusiveRequestStateFromTransferResponseLifecycle();
            }

            if (packetType == 141
                && PacketStageTransitionRuntime.TryDecodeOfficialSetFieldPayload(payload, out PacketSetFieldPacket setFieldPacket, out _))
            {
                UpdateRemoteDropPacketServerClockFromSetField(setFieldPacket);
                UpdatePacketOwnedMovePathRandomCounterOptionFromSetField(setFieldPacket);
                UpdatePacketOwnedFollowRequestOptionFromSetField(setFieldPacket);
                UpdatePacketOwnedAuthoritativeCharacterDataFromSetField(setFieldPacket);
                UpdatePacketOwnedLogoutGiftConfigFromSetField(setFieldPacket);
                UpdatePacketOwnedMapTransferBootstrapFromSetField(setFieldPacket);
            }

            return _packetStageTransitionRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                BuildPacketOwnedStageTransitionCallbacks(),
                out message);
        }

        internal static bool ShouldConsumeSharedExclusiveRequestStateFromStageTransitionPacketType(int packetType)
        {
            // CStage transition response families that own transfer/session handoff.
            return packetType is 141 or 142 or 143;
        }

        private bool TryRelayLoginOwnedStageTransitionPacket(LoginPacketType packetType, string[] args, out bool applied, out string message)
        {
            applied = false;
            message = null;
            if (!TryResolveLoginOwnedStageTransitionPacketType(packetType, out int stagePacketType))
            {
                return false;
            }

            byte[] payload = stagePacketType is 142 or 143 or 146
                ? Array.Empty<byte>()
                : null;
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

            applied = TryApplyPacketOwnedStageTransitionPacket(stagePacketType, payload, out string detail);
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

        private void UpdatePacketOwnedAuthoritativeCharacterDataFromSetField(PacketSetFieldPacket packet)
        {
            PacketCharacterDataSnapshot snapshot = packet.CharacterDataSnapshot;
            if (!packet.HasCharacterData || snapshot == null)
            {
                return;
            }

            SyncPacketOwnedScriptSelectablePetsFromCharacterData(snapshot);
            ApplyPacketOwnedCharacterInventorySnapshot(snapshot);
            ApplyPacketOwnedCharacterSkillSnapshot(snapshot);
            ApplyPacketOwnedCharacterQuestRecordSnapshot(snapshot);
            CharacterBuild activeBuild = _playerManager?.Player?.Build;
            if (activeBuild != null)
            {
                ApplyPacketOwnedCharacterDataSnapshot(activeBuild, snapshot);
            }

            LoginCharacterRosterEntry selectedEntry = _loginCharacterRoster.SelectedEntry;
            if (selectedEntry?.Build == null ||
                ReferenceEquals(selectedEntry.Build, activeBuild) ||
                (snapshot.CharacterId > 0 && selectedEntry.Build.Id > 0 && selectedEntry.Build.Id != snapshot.CharacterId))
            {
                return;
            }

            ApplyPacketOwnedCharacterDataSnapshot(selectedEntry.Build, snapshot);
        }

        private void ApplyPacketOwnedCharacterQuestRecordSnapshot(PacketCharacterDataSnapshot snapshot)
        {
            ApplyPacketOwnedNpcQuestSelectionRecords(_questRuntime, snapshot);
        }

        internal static void ApplyPacketOwnedNpcQuestSelectionRecords(
            QuestRuntimeManager questRuntime,
            PacketCharacterDataSnapshot snapshot)
        {
            if (questRuntime == null || snapshot == null)
            {
                return;
            }

            questRuntime.ApplyPacketOwnedQuestStateSnapshot(snapshot.QuestRecordValues, snapshot.QuestCompleteRecords);
            questRuntime.ApplyPacketOwnedQuestRecordSnapshot(snapshot.QuestRecordValues);
            questRuntime.ApplyPacketOwnedQuestRecordSnapshot(snapshot.QuestExRecordValues);
            questRuntime.ApplyPacketOwnedQuestRecordSnapshot(snapshot.VisitorQuestRecords);
        }

        private void UpdatePacketOwnedFollowRequestOptionFromSetField(PacketSetFieldPacket packet)
        {
            if (packet.ClientOptions == null
                || !packet.ClientOptions.TryGetValue(FollowRequestClientOptionId, out int rawValue)
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.OptionMenu) is not OptionMenuWindow optionMenuWindow)
            {
                return;
            }

            optionMenuWindow.SetCommittedClientOptionValue(FollowRequestClientOptionId, rawValue != 0);
        }

        private void UpdatePacketOwnedMovePathRandomCounterOptionFromSetField(PacketSetFieldPacket packet)
        {
            if (packet.ClientOptions == null
                || !packet.ClientOptions.TryGetValue(MovePathRandomCounterClientOptionId, out int rawValue))
            {
                return;
            }

            _packetOwnedMovePathRandomCounterOptionEnabled = rawValue != 0;
        }

        private void ApplyPacketOwnedCharacterDataSnapshot(CharacterBuild targetBuild, PacketCharacterDataSnapshot snapshot)
        {
            if (targetBuild == null || snapshot == null)
            {
                return;
            }

            targetBuild.Id = snapshot.CharacterId > 0 ? snapshot.CharacterId : targetBuild.Id;
            if (!string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                targetBuild.Name = snapshot.CharacterName;
            }

            targetBuild.Gender = ResolvePacketOwnedCharacterGender(snapshot.Gender, targetBuild.Gender);
            targetBuild.Skin = ResolvePacketOwnedSkinColor(snapshot.Skin, targetBuild.Skin);
            targetBuild.Level = Math.Max(1, (int)snapshot.Level);
            targetBuild.Job = snapshot.JobId;
            targetBuild.SubJob = snapshot.SubJob;
            targetBuild.JobName = SkillDataLoader.GetJobName(snapshot.JobId);
            targetBuild.Fame = Math.Max(0, (int)snapshot.Fame);
            targetBuild.Exp = Math.Max(0L, snapshot.Experience);
            targetBuild.HP = Math.Max(0, snapshot.Hp);
            targetBuild.MaxHP = Math.Max(1, snapshot.MaxHp);
            targetBuild.MP = Math.Max(0, snapshot.Mp);
            targetBuild.MaxMP = Math.Max(0, snapshot.MaxMp);
            targetBuild.STR = Math.Max(0, (int)snapshot.Strength);
            targetBuild.DEX = Math.Max(0, (int)snapshot.Dexterity);
            targetBuild.INT = Math.Max(0, (int)snapshot.Intelligence);
            targetBuild.LUK = Math.Max(0, (int)snapshot.Luck);
            targetBuild.AP = Math.Max(0, (int)snapshot.AbilityPoints);

            CharacterLoader loader = _playerManager?.Loader;
            if (loader == null)
            {
                return;
            }

            if (snapshot.AvatarLook != null)
            {
                CharacterBuild avatarLookBuild = loader.LoadFromAvatarLook(LoginAvatarLookCodec.CloneLook(snapshot.AvatarLook), targetBuild);
                if (avatarLookBuild != null)
                {
                    targetBuild.Gender = avatarLookBuild.Gender;
                    targetBuild.Skin = avatarLookBuild.Skin;
                    targetBuild.Body = avatarLookBuild.Body ?? targetBuild.Body;
                    targetBuild.Head = avatarLookBuild.Head ?? targetBuild.Head;
                    targetBuild.Face = avatarLookBuild.Face ?? targetBuild.Face;
                    targetBuild.Hair = avatarLookBuild.Hair ?? targetBuild.Hair;
                    targetBuild.WeaponSticker = avatarLookBuild.WeaponSticker;
                    targetBuild.Equipment = avatarLookBuild.Equipment ?? new Dictionary<EquipSlot, CharacterPart>();
                    targetBuild.HiddenEquipment = avatarLookBuild.HiddenEquipment ?? new Dictionary<EquipSlot, CharacterPart>();
                    targetBuild.RemotePetItemIds = avatarLookBuild.RemotePetItemIds ?? Array.Empty<int>();
                    return;
                }
            }

            targetBuild.Body = loader.LoadBody(targetBuild.Skin) ?? targetBuild.Body;
            targetBuild.Head = loader.LoadHead(targetBuild.Skin) ?? targetBuild.Head;
            if (snapshot.FaceId > 0)
            {
                targetBuild.Face = loader.LoadFace(snapshot.FaceId) ?? targetBuild.Face;
            }

            if (snapshot.HairId > 0)
            {
                targetBuild.Hair = loader.LoadHair(snapshot.HairId) ?? targetBuild.Hair;
            }
        }

        private void ApplyPacketOwnedCharacterInventorySnapshot(PacketCharacterDataSnapshot snapshot)
        {
            if (snapshot == null || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return;
            }

            if (snapshot.Meso.HasValue)
            {
                inventoryWindow.MesoCount = Math.Max(0, snapshot.Meso.Value);
            }

            IReadOnlyDictionary<InventoryType, int> slotLimits = snapshot.InventorySlotLimits;
            if (slotLimits == null)
            {
                inventoryWindow.ApplyCashItemSerialMetadata(Array.Empty<PacketCharacterDataItemSlot>());
            }
            else
            {
                foreach ((InventoryType inventoryType, int slotLimit) in slotLimits)
                {
                    if (inventoryType == InventoryType.NONE)
                    {
                        continue;
                    }

                    inventoryWindow.SetSlotLimit(inventoryType, slotLimit);
                }
            }

            IReadOnlyList<PacketCharacterDataItemSlot> cashItems = Array.Empty<PacketCharacterDataItemSlot>();
            if (snapshot.InventoryItemsByType != null
                && snapshot.InventoryItemsByType.TryGetValue(InventoryType.CASH, out IReadOnlyList<PacketCharacterDataItemSlot> decodedCashItems)
                && decodedCashItems != null)
            {
                cashItems = decodedCashItems;
            }

            inventoryWindow.ApplyCashItemSerialMetadata(cashItems);
        }

        private void ApplyPacketOwnedCharacterSkillSnapshot(PacketCharacterDataSnapshot snapshot)
        {
            if (_playerManager?.Skills == null || snapshot == null)
            {
                return;
            }

            IReadOnlyDictionary<int, int> skillRecords = snapshot.SkillRecords;
            if (skillRecords != null)
            {
                _playerManager.Skills.ApplyAuthoritativeSkillRecordSnapshot(skillRecords);
            }

            IReadOnlyDictionary<int, int> masterLevels = snapshot.SkillMasterLevels;
            if (masterLevels != null)
            {
                _playerManager.Skills.ApplyAuthoritativeSkillMasterLevelSnapshot(masterLevels);
            }

            IReadOnlyDictionary<int, int> cooldowns = snapshot.SkillCooldownRemainingSecondsBySkillId;
            if (cooldowns != null)
            {
                _playerManager.Skills.ApplyAuthoritativeCooldownSnapshot(cooldowns, currTickCount);
            }
        }

        private static CharacterGender ResolvePacketOwnedCharacterGender(byte genderValue, CharacterGender fallback)
        {
            return Enum.IsDefined(typeof(CharacterGender), (int)genderValue)
                ? (CharacterGender)genderValue
                : fallback;
        }

        private static SkinColor ResolvePacketOwnedSkinColor(byte skinValue, SkinColor fallback)
        {
            return Enum.IsDefined(typeof(SkinColor), (int)skinValue)
                ? (SkinColor)skinValue
                : fallback;
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
            ResetPacketOwnedLogoutGiftRuntimeState(clearConfig: true, hideWindow: true, summary: "Packet-owned logout-gift owner cleared with stage-transition state.");
        }

        private void ResetPacketOwnedStageTransitionRuntimeState()
        {
            _packetStageTransitionObjectVisibility.Clear();
            RestorePacketOwnedBackEffect();
            _packetStageTransitionRuntime.Clear();
            ClearPacketOwnedScriptSelectablePets();
            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow)
            {
                inventoryWindow.ClearCashItemSerialMetadata();
            }
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
            return $"{_packetStageTransitionRuntime.DescribeStatus()}{Environment.NewLine}{DescribeStageTransitionPacketInboxStatus()} {_stageTransitionPacketInbox.LastStatus}";
        }

        private string DescribeStageTransitionPacketInboxStatus()
        {
            return "Stage-transition packet inbox adapter-only, proxy-required, listener-fallback retired.";
        }

        private void EnsureStageTransitionPacketInboxState(bool shouldRun)
        {
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
