using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Info;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketStageTransitionRuntime _packetStageTransitionRuntime = new();
        private readonly PacketOwnedClientOptionManager _packetOwnedClientOptions = new();
        private readonly StageTransitionPacketInboxManager _stageTransitionPacketInbox = new();
        private readonly Dictionary<string, List<BaseDXDrawableItem>> _packetStageTransitionNamedObjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BaseDXDrawableItem, PacketOwnedNamedObjectStateMetadata> _packetStageTransitionNamedObjectMetadata = new();
        private readonly Dictionary<BaseDXDrawableItem, Dictionary<int, BaseDXDrawableItem>> _packetStageTransitionAuthoredStateBranchItems = new();
        private readonly Dictionary<BaseDXDrawableItem, bool> _packetStageTransitionObjectVisibility = new();
        private readonly Dictionary<BaseDXDrawableItem, PacketOwnedNamedObjectMovingState> _packetStageTransitionNamedObjectMovingStates = new();
        private readonly Dictionary<BaseDXDrawableItem, PacketOwnedNamedObjectSideLaneLifecycleSnapshot> _packetStageTransitionNamedObjectSideLaneLifecycle = new();
        private readonly Dictionary<BaseDXDrawableItem, PacketOwnedNamedObjectLayerLifecycleSnapshot> _packetStageTransitionNamedObjectLayerLifecycle = new();
        private readonly Dictionary<BaseDXDrawableItem, PacketOwnedNamedObjectAlphaPlaybackState> _packetStageTransitionNamedObjectAlphaStates = new();
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
                ConsumeSharedExclusiveRequestStateFromStageTransitionLifecycle();
            }

            if (packetType == 141
                && PacketStageTransitionRuntime.TryDecodeOfficialSetFieldPayload(payload, out PacketSetFieldPacket setFieldPacket, out _))
            {
                UpdatePacketOwnedClientOptionsFromSetField(setFieldPacket);
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

            if (TryRelayLoginOwnedCharacterSalePacket(packetType, stagePacketType, out applied, out message))
            {
                return true;
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
                LoginPacketType.CharacterSaleCheckDuplicatedIdResult => 413,
                LoginPacketType.CharacterSaleCreateNewCharacterResult => 414,
                _ => 0
            };
            return stagePacketType != 0;
        }

        private bool TryRelayLoginOwnedCharacterSalePacket(
            LoginPacketType packetType,
            int stagePacketType,
            out bool applied,
            out string message)
        {
            applied = false;
            message = null;
            if (stagePacketType is not 413 and not 414)
            {
                return false;
            }

            if (stagePacketType == 413)
            {
                applied = TryGetLoginCheckDuplicatedIdPacketProfile(out LoginAccountDialogPacketProfile duplicatedIdProfile) &&
                          duplicatedIdProfile?.ResultCode != null;
                message = applied
                    ? "CField::OnCharacterSale forwarded packet 413 to CUICharacterSaleDlg::OnCheckDuplicatedIDResult and updated the staged duplicate-name result."
                    : "CField::OnCharacterSale forwarded packet 413 to CUICharacterSaleDlg::OnCheckDuplicatedIDResult, but no duplicate-name result payload was available.";
                return true;
            }

            applied = _loginPacketCreateNewCharacterResultProfile != null;
            message = applied
                ? "CField::OnCharacterSale forwarded packet 414 to CUICharacterSaleDlg::OnCreateNewCharacterResult and reused the login create-result roster mutation path."
                : "CField::OnCharacterSale forwarded packet 414 to CUICharacterSaleDlg::OnCreateNewCharacterResult, but no create-character result payload was available.";
            return true;
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

        private void UpdatePacketOwnedClientOptionsFromSetField(PacketSetFieldPacket packet)
        {
            _packetOwnedClientOptions.DecodeOpt(packet.ClientOptions);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.OptionMenu) is OptionMenuWindow optionMenuWindow)
            {
                optionMenuWindow.ApplyCommittedClientOptionValues(_packetOwnedClientOptions.Snapshot);
            }
        }

        private void ApplyCommittedClientOptionValuesFromOptionMenu(IReadOnlyDictionary<int, bool> committedOptions)
        {
            if (committedOptions == null)
            {
                return;
            }

            foreach (KeyValuePair<int, bool> option in committedOptions)
            {
                _packetOwnedClientOptions.SetOpt(option.Key, option.Value ? 1 : 0);
            }

            if (_packetOwnedClientOptions.TryGetOpt(MovePathRandomCounterClientOptionId, out int randomCounterRawValue))
            {
                _packetOwnedMovePathRandomCounterOptionEnabled = randomCounterRawValue != 0;
            }
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
            questRuntime.ApplyPacketOwnedQuestExRecordSnapshot(snapshot.QuestExRecordValues);
            questRuntime.ApplyPacketOwnedQuestRecordSnapshot(snapshot.VisitorQuestRecords);
        }

        private void UpdatePacketOwnedFollowRequestOptionFromSetField(PacketSetFieldPacket packet)
        {
            if (!_packetOwnedClientOptions.TryGetOpt(FollowRequestClientOptionId, out int rawValue)
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.OptionMenu) is not OptionMenuWindow optionMenuWindow)
            {
                return;
            }

            optionMenuWindow.SetCommittedClientOptionValue(FollowRequestClientOptionId, rawValue != 0);
        }

        private void UpdatePacketOwnedMovePathRandomCounterOptionFromSetField(PacketSetFieldPacket packet)
        {
            if (!_packetOwnedClientOptions.TryGetOpt(MovePathRandomCounterClientOptionId, out int rawValue))
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
            if (objInst.BaseInfo is ObjectInfo objectInfo)
            {
                _packetStageTransitionNamedObjectMetadata[mapItem] = new PacketOwnedNamedObjectStateMetadata(
                    objectName,
                    objectInfo.oS,
                    objectInfo.l0,
                    objectInfo.l1,
                    objectInfo.l2,
                    objInst.X,
                    objInst.Y,
                    objInst.Z,
                    objInst.PlatformNumber,
                    objInst.Dynamic,
                    (byte)objInst.flow,
                    objInst.rx,
                    objInst.ry,
                    objInst.cx,
                    objInst.cy,
                    ResolvePacketOwnedNamedObjectStateSfx(objectInfo),
                    ResolvePacketOwnedNamedObjectAuthoredStateSfxByIndex(objectInfo?.ParentObject as WzImageProperty),
                    ResolvePacketOwnedNamedObjectAuthoredStateRepeatByIndex(objectInfo?.ParentObject as WzImageProperty),
                    PacketOwnedNamedObjectMotionProfile.FromMapObject(
                        (byte)objInst.flow,
                        objInst.rx,
                        objInst.ry,
                        objInst.cx,
                        objInst.cy),
                    ResolvePacketOwnedNamedObjectAuthoredStateMotionByIndex(objectInfo?.ParentObject as WzImageProperty),
                    PacketOwnedNamedObjectVectorAnimationProfile.FromWzProperty(objectInfo?.ParentObject as WzImageProperty),
                    ResolvePacketOwnedNamedObjectAuthoredStateVectorAnimationByIndex(objectInfo?.ParentObject as WzImageProperty),
                    PacketOwnedNamedObjectAlphaProfile.FromWzProperty(objectInfo?.ParentObject as WzImageProperty),
                    ResolvePacketOwnedNamedObjectAuthoredStateAlphaByIndex(objectInfo?.ParentObject as WzImageProperty),
                    ResolvePacketOwnedNamedObjectAuthoredStateMetadataLanesByIndex(objectInfo?.ParentObject as WzImageProperty),
                    ResolvePacketOwnedNamedObjectMetadataLanesForPacketParity(
                        objInst.Dynamic,
                        objInst.r,
                        objInst.flow,
                        objInst.rx,
                        objInst.ry,
                        objInst.cx,
                        objInst.cy,
                        objInst.QuestInfo?.Count > 0),
                    ResolvePacketOwnedNamedObjectAuthoredStateIndexes(objectInfo?.ParentObject as WzImageProperty));
            }
        }

        private IReadOnlyList<BaseDXDrawableItem> CreatePacketOwnedStageTransitionAuthoredStateBranchItems(
            BaseDXDrawableItem mapItem,
            LayeredItem sourceItem,
            ConcurrentBag<WzObject> usedProps,
            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects)
        {
            if (mapItem == null ||
                sourceItem is not ObjectInstance objInst ||
                objInst.BaseInfo is not ObjectInfo objectInfo ||
                objectInfo.ParentObject is not WzImageProperty objectProperty ||
                _DxDeviceManager?.GraphicsDevice == null)
            {
                return Array.Empty<BaseDXDrawableItem>();
            }

            IReadOnlySet<int> authoredStateIndexes = ResolvePacketOwnedNamedObjectAuthoredStateIndexes(objectProperty);
            if (authoredStateIndexes == null || authoredStateIndexes.Count == 0)
            {
                return Array.Empty<BaseDXDrawableItem>();
            }

            List<BaseDXDrawableItem> branchItems = new();
            Dictionary<int, BaseDXDrawableItem> branchesByState = new();
            foreach (int stateIndex in authoredStateIndexes.OrderBy(static state => state))
            {
                if (stateIndex <= 0)
                {
                    continue;
                }

                WzImageProperty branchProperty = WzInfoTools.GetRealProperty(objectProperty[$"s{stateIndex}"]);
                if (branchProperty == null)
                {
                    continue;
                }

                List<IDXObject> branchFrames = MapSimulatorLoader.LoadFrames(
                    _texturePool,
                    branchProperty,
                    objInst.X,
                    objInst.Y,
                    _DxDeviceManager.GraphicsDevice,
                    usedProps,
                    fallbackDelay: 100);
                if (branchFrames == null || branchFrames.Count == 0)
                {
                    continue;
                }

                BaseDXDrawableItem branchItem = new(branchFrames, objInst.Flip);
                branchesByState[stateIndex] = branchItem;
                branchItems.Add(branchItem);
                _packetStageTransitionObjectVisibility[branchItem] = false;
                if (_packetStageTransitionNamedObjectMetadata.TryGetValue(mapItem, out PacketOwnedNamedObjectStateMetadata metadata))
                {
                    _packetStageTransitionNamedObjectMetadata[branchItem] = metadata;
                }

                ObjectInstanceQuest[] branchQuestInfo = ResolvePacketOwnedNamedObjectQuestInfo(branchProperty);
                QuestGatedMapObjectState? questState = BuildQuestGatedMapObjectState(
                    objInst,
                    branchQuestInfo.Length > 0 ? branchQuestInfo : null);
                if (questState.HasValue)
                {
                    questGatedMapObjects[branchItem] = questState.Value;
                }
            }

            if (branchesByState.Count > 0)
            {
                _packetStageTransitionAuthoredStateBranchItems[mapItem] = branchesByState;
            }

            return branchItems;
        }

        private static QuestGatedMapObjectState? BuildQuestGatedMapObjectState(
            ObjectInstance objInst,
            ObjectInstanceQuest[] questInfoOverride = null)
        {
            if (objInst == null)
            {
                return null;
            }

            bool hiddenByMap = objInst.hide == true;
            ObjectInstanceQuest[] questInfo = questInfoOverride ?? objInst.QuestInfo?.ToArray();
            bool hasQuestInfo = questInfo != null && questInfo.Length > 0;
            string[] dynamicTags = ParseObjectTags(objInst.tags);
            bool hasDynamicTags = dynamicTags.Length > 0;
            return hiddenByMap || hasQuestInfo || hasDynamicTags
                ? new QuestGatedMapObjectState(questInfo, dynamicTags, hiddenByMap)
                : null;
        }

        internal static ObjectInstanceQuest[] ResolvePacketOwnedNamedObjectQuestInfo(WzImageProperty objectProperty)
        {
            if (objectProperty?["quest"] is not WzImageProperty questProperty ||
                questProperty.WzProperties == null ||
                questProperty.WzProperties.Count == 0)
            {
                return Array.Empty<ObjectInstanceQuest>();
            }

            List<ObjectInstanceQuest> questInfo = new();
            foreach (WzImageProperty child in questProperty.WzProperties)
            {
                if (child == null ||
                    !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int questId) ||
                    questId <= 0 ||
                    !TryReadPacketOwnedNamedObjectIntProperty(questProperty, child.Name, out int rawState))
                {
                    continue;
                }

                QuestStateType state = Enum.IsDefined(typeof(QuestStateType), rawState)
                    ? (QuestStateType)rawState
                    : QuestStateType.Not_Started;
                questInfo.Add(new ObjectInstanceQuest(questId, state));
            }

            return questInfo.Count == 0 ? Array.Empty<ObjectInstanceQuest>() : questInfo.ToArray();
        }

        private static string ResolvePacketOwnedNamedObjectStateSfx(ObjectInfo objectInfo)
        {
            if (objectInfo?.ParentObject is not WzImageProperty objectProperty)
            {
                return string.Empty;
            }

            return (objectProperty["sfx"] as WzStringProperty)?.Value?.Trim() ?? string.Empty;
        }

        internal static IReadOnlyDictionary<int, string> ResolvePacketOwnedNamedObjectAuthoredStateSfxByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, string> stateSfxByIndex = new();
            if (objectProperty == null)
            {
                return stateSfxByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                string stateSfx = (child["sfx"] as WzStringProperty)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(stateSfx))
                {
                    stateSfxByIndex[stateIndex] = stateSfx;
                }
            }

            return stateSfxByIndex;
        }

        internal static IReadOnlyDictionary<int, int> ResolvePacketOwnedNamedObjectAuthoredStateRepeatByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, int> repeatByIndex = new();
            if (objectProperty == null)
            {
                return repeatByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                WzImageProperty realChild = WzInfoTools.GetRealProperty(child);
                WzIntProperty repeatProperty = realChild?["repeat"] as WzIntProperty;
                if (repeatProperty != null)
                {
                    repeatByIndex[stateIndex] = repeatProperty.Value;
                }
            }

            return repeatByIndex;
        }

        internal static IReadOnlyDictionary<int, PacketOwnedNamedObjectMotionProfile> ResolvePacketOwnedNamedObjectAuthoredStateMotionByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, PacketOwnedNamedObjectMotionProfile> motionByIndex = new();
            if (objectProperty == null)
            {
                return motionByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                WzImageProperty realChild = WzInfoTools.GetRealProperty(child);
                PacketOwnedNamedObjectMotionProfile motionProfile = PacketOwnedNamedObjectMotionProfile.FromWzProperty(realChild);
                if (motionProfile != null)
                {
                    motionByIndex[stateIndex] = motionProfile;
                }
            }

            return motionByIndex;
        }

        internal static IReadOnlyDictionary<int, PacketOwnedNamedObjectVectorAnimationProfile> ResolvePacketOwnedNamedObjectAuthoredStateVectorAnimationByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, PacketOwnedNamedObjectVectorAnimationProfile> vectorAnimationByIndex = new();
            if (objectProperty == null)
            {
                return vectorAnimationByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                PacketOwnedNamedObjectVectorAnimationProfile vectorProfile =
                    PacketOwnedNamedObjectVectorAnimationProfile.FromWzProperty(child);
                if (vectorProfile != null)
                {
                    vectorAnimationByIndex[stateIndex] = vectorProfile;
                }
            }

            return vectorAnimationByIndex;
        }

        internal static IReadOnlyDictionary<int, PacketOwnedNamedObjectAlphaProfile> ResolvePacketOwnedNamedObjectAuthoredStateAlphaByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, PacketOwnedNamedObjectAlphaProfile> alphaByIndex = new();
            if (objectProperty == null)
            {
                return alphaByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                PacketOwnedNamedObjectAlphaProfile alphaProfile = PacketOwnedNamedObjectAlphaProfile.FromWzProperty(child);
                if (alphaProfile != null)
                {
                    alphaByIndex[stateIndex] = alphaProfile;
                }
            }

            return alphaByIndex;
        }

        internal static IReadOnlyDictionary<int, PacketOwnedNamedObjectMetadataLane> ResolvePacketOwnedNamedObjectAuthoredStateMetadataLanesByIndex(WzImageProperty objectProperty)
        {
            Dictionary<int, PacketOwnedNamedObjectMetadataLane> lanesByIndex = new();
            if (objectProperty == null)
            {
                return lanesByIndex;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (!TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    continue;
                }

                WzImageProperty realChild = WzInfoTools.GetRealProperty(child);
                PacketOwnedNamedObjectMotionProfile motionProfile = PacketOwnedNamedObjectMotionProfile.FromWzProperty(realChild);
                PacketOwnedNamedObjectVectorAnimationProfile vectorProfile =
                    PacketOwnedNamedObjectVectorAnimationProfile.FromWzProperty(realChild);
                bool dynamicObject = TryReadPacketOwnedNamedObjectIntProperty(realChild, "dynamic", out int dynamicValue) &&
                    dynamicValue != 0;
                PacketOwnedNamedObjectMetadataLane lanes = ResolvePacketOwnedNamedObjectMetadataLanesForPacketParity(
                    hasChangingObjectMetadata: dynamicObject || motionProfile != null || vectorProfile != null,
                    hasReflectionMetadata: false,
                    hasQuestVisibleMetadata: realChild?["quest"] is WzImageProperty);
                if (lanes != PacketOwnedNamedObjectMetadataLane.None)
                {
                    lanesByIndex[stateIndex] = lanes;
                }
            }

            return lanesByIndex;
        }

        internal static IReadOnlySet<int> ResolvePacketOwnedNamedObjectAuthoredStateIndexes(WzImageProperty objectProperty)
        {
            HashSet<int> stateIndexes = new();
            if (objectProperty == null)
            {
                return stateIndexes;
            }

            foreach (WzImageProperty child in objectProperty.WzProperties)
            {
                if (TryResolvePacketOwnedNamedObjectAuthoredStateIndex(child?.Name, out int stateIndex))
                {
                    stateIndexes.Add(stateIndex);
                }
            }

            return stateIndexes;
        }

        internal static bool TryResolvePacketOwnedNamedObjectAuthoredStateIndex(string branchName, out int stateIndex)
        {
            stateIndex = 0;
            if (string.IsNullOrWhiteSpace(branchName) ||
                branchName.Length < 2 ||
                branchName[0] != 's')
            {
                return false;
            }

            return int.TryParse(
                branchName.Substring(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out stateIndex) &&
                stateIndex >= 0;
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
            _packetStageTransitionNamedObjectMetadata.Clear();
            _packetStageTransitionAuthoredStateBranchItems.Clear();
            _packetStageTransitionNamedObjectSideLaneLifecycle.Clear();
            _packetStageTransitionNamedObjectLayerLifecycle.Clear();
            ResetPacketOwnedLogoutGiftRuntimeState(clearConfig: true, hideWindow: true, summary: "Packet-owned logout-gift owner cleared with stage-transition state.");
        }

        private void ResetPacketOwnedStageTransitionRuntimeState()
        {
            _packetStageTransitionObjectVisibility.Clear();
            _packetStageTransitionNamedObjectMovingStates.Clear();
            _packetStageTransitionNamedObjectSideLaneLifecycle.Clear();
            _packetStageTransitionNamedObjectLayerLifecycle.Clear();
            foreach (BaseDXDrawableItem mapObject in _packetStageTransitionNamedObjectAlphaStates.Keys.ToArray())
            {
                mapObject?.SetLayerAlpha(byte.MaxValue);
            }

            foreach (BaseDXDrawableItem mapObject in _packetStageTransitionNamedObjectMetadata.Keys.ToArray())
            {
                mapObject?.SetLayerRotationDegrees(0f);
            }

            _packetStageTransitionNamedObjectAlphaStates.Clear();
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
            UpdatePacketOwnedNamedObjectMovingStates(currentTick);
            UpdatePacketOwnedNamedObjectAlphaStates(currentTick);

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

        private void UpdatePacketOwnedNamedObjectAlphaStates(int currentTick)
        {
            if (_packetStageTransitionNamedObjectAlphaStates.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<BaseDXDrawableItem, PacketOwnedNamedObjectAlphaPlaybackState> entry in _packetStageTransitionNamedObjectAlphaStates.ToArray())
            {
                if (entry.Key == null)
                {
                    continue;
                }

                entry.Value.Apply(entry.Key, currentTick);
            }
        }

        private void UpdatePacketOwnedNamedObjectMovingStates(int currentTick)
        {
            if (_packetStageTransitionNamedObjectMovingStates.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<BaseDXDrawableItem, PacketOwnedNamedObjectMovingState> entry in _packetStageTransitionNamedObjectMovingStates.ToArray())
            {
                if (entry.Key == null)
                {
                    continue;
                }

                entry.Value.Apply(entry.Key, currentTick);
            }
        }

        internal static bool TryResolvePacketOwnedNamedObjectMoveVector(
            int objectX,
            int objectY,
            byte flow,
            int? rx,
            int? ry,
            int? cx,
            int? cy,
            out int targetOffsetX,
            out int targetOffsetY)
        {
            targetOffsetX = 0;
            targetOffsetY = 0;
            if (flow == 0)
            {
                return false;
            }

            if (rx.HasValue || ry.HasValue)
            {
                targetOffsetX = rx.HasValue ? rx.Value - objectX : 0;
                targetOffsetY = ry.HasValue ? ry.Value - objectY : 0;
                return targetOffsetX != 0 || targetOffsetY != 0;
            }

            if (cx.HasValue || cy.HasValue)
            {
                targetOffsetX = cx.HasValue ? cx.Value - objectX : 0;
                targetOffsetY = cy.HasValue ? cy.Value - objectY : 0;
                return targetOffsetX != 0 || targetOffsetY != 0;
            }

            return false;
        }

        private static bool TryBuildPacketOwnedNamedObjectMovingState(
            PacketOwnedNamedObjectStateMetadata metadata,
            int stateIndex,
            int currentTick,
            out PacketOwnedNamedObjectMovingState movingState)
        {
            movingState = null;
            PacketOwnedNamedObjectMotionProfile motionProfile = metadata?.ResolveMotionProfile(stateIndex);
            if (metadata == null)
            {
                return false;
            }

            if (motionProfile != null &&
                TryResolvePacketOwnedNamedObjectMoveVector(
                    metadata.X,
                    metadata.Y,
                    motionProfile.Flow,
                    motionProfile.Rx,
                    motionProfile.Ry,
                    motionProfile.Cx,
                    motionProfile.Cy,
                    out int targetOffsetX,
                    out int targetOffsetY))
            {
                movingState = new PacketOwnedNamedObjectMovingState(
                    currentTick,
                    PacketOwnedNamedObjectMovingState.DefaultDurationMs,
                    0,
                    0,
                    targetOffsetX,
                    targetOffsetY);
                return true;
            }

            PacketOwnedNamedObjectVectorAnimationProfile vectorProfile = metadata.ResolveVectorAnimationProfile(stateIndex);
            if (vectorProfile == null)
            {
                return false;
            }

            bool hasMoveVector = vectorProfile.TryResolveMoveVector(out targetOffsetX, out targetOffsetY, out int moveDurationMs);
            bool hasRotation = vectorProfile.TryResolveRotation(out float targetRotationDegrees, out int rotateDurationMs);
            if (!hasMoveVector && !hasRotation)
            {
                return false;
            }

            int durationMs = hasMoveVector && hasRotation
                ? Math.Max(1, Math.Min(moveDurationMs, rotateDurationMs))
                : hasMoveVector ? moveDurationMs : rotateDurationMs;
            movingState = new PacketOwnedNamedObjectMovingState(
                currentTick,
                durationMs,
                0,
                0,
                targetOffsetX,
                targetOffsetY,
                targetRotationDegrees,
                vectorProfile.UsesEllipticalMove,
                vectorProfile.EllipticalClockwise);
            return true;
        }

        internal static void ResolvePacketOwnedNamedObjectVectorMotionOffset(
            float phase,
            int startX,
            int startY,
            int targetX,
            int targetY,
            bool usesEllipticalMove,
            bool ellipticalClockwise,
            out int x,
            out int y,
            out float progress)
        {
            phase -= MathF.Floor(phase);
            if (!usesEllipticalMove)
            {
                progress = phase <= 0.5f
                    ? phase * 2f
                    : (1f - phase) * 2f;
                x = (int)Math.Round(startX + ((targetX - startX) * progress));
                y = (int)Math.Round(startY + ((targetY - startY) * progress));
                return;
            }

            float direction = ellipticalClockwise ? 1f : -1f;
            float angle = phase * MathF.PI * 2f * direction;
            x = startX + (int)Math.Round(targetX * MathF.Sin(angle));
            y = startY + (int)Math.Round(targetY * (1f - MathF.Cos(angle)));
            progress = phase <= 0.5f
                ? phase * 2f
                : (1f - phase) * 2f;
        }

        private sealed record PacketOwnedNamedObjectMovingState(
            int StartTick,
            int DurationMs,
            int StartX,
            int StartY,
            int TargetX,
            int TargetY,
            float TargetRotationDegrees = 0f,
            bool UsesEllipticalMove = false,
            bool EllipticalClockwise = true)
        {
            public const int DefaultDurationMs = 4000;

            public void Apply(BaseDXDrawableItem item, int currentTick)
            {
                if (item == null)
                {
                    return;
                }

                int duration = Math.Max(1, DurationMs);
                float phase = ((currentTick - StartTick) % duration) / (float)duration;
                if (phase < 0f)
                {
                    phase += 1f;
                }

                ResolvePacketOwnedNamedObjectVectorMotionOffset(
                    phase,
                    StartX,
                    StartY,
                    TargetX,
                    TargetY,
                    UsesEllipticalMove,
                    EllipticalClockwise,
                    out int x,
                    out int y,
                    out float rotationProgress);
                item.Position = new Microsoft.Xna.Framework.Point(x, y);
                item.SetLayerRotationDegrees(TargetRotationDegrees * rotationProgress);
            }
        }

        private sealed record PacketOwnedNamedObjectAlphaPlaybackState(
            PacketOwnedNamedObjectAlphaProfile AlphaProfile,
            int StartTick)
        {
            public void Apply(BaseDXDrawableItem item, int currentTick)
            {
                if (item == null || AlphaProfile == null)
                {
                    return;
                }

                int frameIndex = item.GetCurrentAnimationFrameIndex(currentTick);
                int frameElapsedMs = item.GetCurrentAnimationFrameElapsed(currentTick);
                int frameDelayMs = item.GetCurrentAnimationFrameDelay(currentTick);
                item.SetLayerAlpha(ResolvePacketOwnedNamedObjectLayerAlpha(AlphaProfile, frameIndex, frameElapsedMs, frameDelayMs));
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
