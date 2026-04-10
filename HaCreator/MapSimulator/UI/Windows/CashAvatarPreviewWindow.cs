using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashAvatarPreviewWindow : UIWindowBase
    {
        private const int ClientWearInfoCapacity = 60;
        private const int FrameWidth = 248;
        private const int FrameHeight = 266;
        private const int PreviewX = 24;
        private const int PreviewY = 40;
        private const int PreviewWidth = 212;
        private const int PreviewFeetOffsetY = 150;
        private const int BuyButtonX = 17;
        private const int DefaultButtonX = 101;
        private const int TakeoffButtonX = 187;
        private const int ButtonY = 237;

        private readonly IDXObject _windowBackgroundLayer;
        private readonly Point _windowBackgroundOffset;
        private readonly IDXObject _windowOverlayLayer;
        private readonly Point _windowOverlayOffset;
        private readonly IDXObject _windowContentLayer;
        private readonly Point _windowContentOffset;
        private readonly Texture2D[] _previewBackgrounds;
        private readonly UIObject _buyAvatarButton;
        private readonly UIObject _defaultAvatarButton;
        private readonly UIObject _takeoffAvatarButton;

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private Func<AdminShopAvatarPreviewSelection> _selectionProvider;
        private Func<string> _shopRequestHandler;
        private LoginAvatarLook _initialAvatarLook;
        private CharacterBuild _initialAvatarBuild;
        private CharacterBuild _previewBuild;
        private CharacterAssembler _previewAssembler;
        private CharacterBuild _previewSourceBuild;
        private Func<int, CharacterPart> _equipmentLoader;
        private readonly int[] _previewWearItemIds = new int[ClientWearInfoCapacity];
        private string _selectionSignature = string.Empty;
        private string _statusMessage = "CCSWnd_Char preview idle.";
        private string _previewOwnerState = "CUserPreview / physical-space seam idle.";
        private string _previewPhysicalSpaceState = "CWvsPhysicalSpace2D seam idle.";
        private string _previewResetState = "AvatarLook snapshot idle.";
        private AdminShopAvatarPreviewSelection _currentSelection;
        private EquipSlot? _lastPreviewedSlot;
        private bool _lastPreviewedWeaponSticker;
        private bool _lastPreviewedPet;
        private int _previewPetItemId;
        private int _previewWearMutationCount;
        private string _previewMutationState = "No staged preview mutation.";

        public CashAvatarPreviewWindow(
            GraphicsDevice device,
            IDXObject windowBackgroundLayer,
            Point windowBackgroundOffset,
            IDXObject windowOverlayLayer,
            Point windowOverlayOffset,
            IDXObject windowContentLayer,
            Point windowContentOffset,
            Texture2D[] previewBackgrounds,
            UIObject buyAvatarButton,
            UIObject defaultAvatarButton,
            UIObject takeoffAvatarButton)
            : base(CreateFrame(device))
        {
            _windowBackgroundLayer = windowBackgroundLayer;
            _windowBackgroundOffset = windowBackgroundOffset;
            _windowOverlayLayer = windowOverlayLayer;
            _windowOverlayOffset = windowOverlayOffset;
            _windowContentLayer = windowContentLayer;
            _windowContentOffset = windowContentOffset;
            _previewBackgrounds = previewBackgrounds ?? Array.Empty<Texture2D>();
            _buyAvatarButton = buyAvatarButton;
            _defaultAvatarButton = defaultAvatarButton;
            _takeoffAvatarButton = takeoffAvatarButton;

            SupportsDragging = true;

            if (_buyAvatarButton != null)
            {
                _buyAvatarButton.X = BuyButtonX;
                _buyAvatarButton.Y = ButtonY;
                _buyAvatarButton.ButtonClickReleased += _ => HandleBuyAvatar();
                AddButton(_buyAvatarButton);
            }

            if (_defaultAvatarButton != null)
            {
                _defaultAvatarButton.X = DefaultButtonX;
                _defaultAvatarButton.Y = ButtonY;
                _defaultAvatarButton.ButtonClickReleased += _ => HandleDefaultAvatar();
                AddButton(_defaultAvatarButton);
            }

            if (_takeoffAvatarButton != null)
            {
                _takeoffAvatarButton.X = TakeoffButtonX;
                _takeoffAvatarButton.Y = ButtonY;
                _takeoffAvatarButton.ButtonClickReleased += _ => HandleTakeoffAvatar();
                AddButton(_takeoffAvatarButton);
            }
        }
        public override string WindowName => MapSimulatorWindowNames.CashAvatarPreview;
        public override bool CapturesKeyboardInput => IsVisible;
        public Func<string> PersonalShopRequested { get; set; }
        public Func<string> EntrustedShopRequested { get; set; }
        public Func<string> TradingRoomRequested { get; set; }
        public Func<string> WeatherRequested { get; set; }
        public Func<int, CharacterPart> EquipmentLoader
        {
            get => _equipmentLoader;
            set => _equipmentLoader = value;
        }

        public void SetSelectionProvider(Func<AdminShopAvatarPreviewSelection> selectionProvider)
        {
            _selectionProvider = selectionProvider;
            _selectionSignature = string.Empty;
        }

        public void SetShopRequestHandler(Func<string> shopRequestHandler)
        {
            _shopRequestHandler = shopRequestHandler;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _selectionSignature = string.Empty;
            SyncPreviewBuild(forceReset: true);
            RefreshSelectionState(force: true);
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                HandleBuyAvatar();
            }
            else if (Pressed(keyboardState, Keys.Home))
            {
                HandleDefaultAvatar();
            }
            else if (Pressed(keyboardState, Keys.Delete))
            {
                HandleTakeoffAvatar();
            }

            _previousKeyboardState = keyboardState;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            RefreshSelectionState();
            SyncPreviewBuild();

            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _windowBackgroundLayer, _windowBackgroundOffset, drawReflectionInfo);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _windowOverlayLayer, _windowOverlayOffset, drawReflectionInfo);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _windowContentLayer, _windowContentOffset, drawReflectionInfo);

            Texture2D background = ResolveBackgroundTexture();
            if (background != null)
            {
                sprite.Draw(background, new Vector2(Position.X + PreviewX, Position.Y + PreviewY), Color.White);
            }

            if (_previewAssembler != null)
            {
                AssembledFrame previewFrame = _previewAssembler.GetFrameAtTime("stand1", TickCount);
                if (previewFrame != null)
                {
                    int anchorX = Position.X + PreviewX + (PreviewWidth / 2);
                    int anchorY = Position.Y + PreviewY + PreviewFeetOffsetY;
                    previewFrame.Draw(sprite, skeletonMeshRenderer, anchorX, anchorY, false, Color.White);
                }
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, "CCSWnd_Char preview", new Vector2(Position.X + 12, Position.Y + 10), Color.White);
            sprite.DrawString(_font, ResolveSelectionLabel(), new Vector2(Position.X + 12, Position.Y + 24), new Color(232, 222, 188));
            sprite.DrawString(_font, TrimForDisplay(_previewOwnerState, 34), new Vector2(Position.X + 12, Position.Y + 38), new Color(200, 200, 200));
            sprite.DrawString(_font, TrimForDisplay(_previewPhysicalSpaceState, 34), new Vector2(Position.X + 12, Position.Y + 52), new Color(176, 176, 176));
            sprite.DrawString(_font, TrimForDisplay(_previewResetState, 34), new Vector2(Position.X + 12, Position.Y + 66), new Color(176, 176, 176));

            float lineY = Position.Y + 184;
            foreach (string line in WrapText(_statusMessage, 222f))
            {
                sprite.DrawString(_font, line, new Vector2(Position.X + 12, lineY), new Color(255, 225, 157));
                lineY += 14f;
            }
        }

        private void HandleBuyAvatar()
        {
            RefreshSelectionState(force: true);
            if (_currentSelection == null)
            {
                _statusMessage = "CCSWnd_Char::OnWear has no selected Cash Shop row to apply.";
                return;
            }

            if (TryExecuteServiceAction(out string serviceMessage))
            {
                _statusMessage = serviceMessage;
                return;
            }

            string requestMessage = _shopRequestHandler?.Invoke();
            if (!string.IsNullOrWhiteSpace(requestMessage))
            {
                _statusMessage = requestMessage;
                return;
            }

            _statusMessage = $"CCSWnd_Char::OnWear could not submit {_currentSelection.Title}.";
        }

        private void HandleDefaultAvatar()
        {
            if (CharacterBuild == null)
            {
                _statusMessage = "CCSWnd_Char::OnDefaultAvatar is unavailable without a live character build.";
                UpdateButtonStates();
                return;
            }

            ResetPreviewBuild();
            _lastPreviewedSlot = null;
            _lastPreviewedWeaponSticker = false;
            _lastPreviewedPet = false;
            _buyAvatarButton?.SetEnabled(true);
            _previewMutationState = "Default-avatar snapshot restored.";
            _statusMessage = "CCSWnd_Char::OnDefaultAvatar restored the avatar-look snapshot, pet/riding state, and preview objects.";
            UpdateButtonStates();
        }

        private void HandleTakeoffAvatar()
        {
            SyncPreviewBuild();
            if (_previewBuild == null || (!_lastPreviewedSlot.HasValue && !_lastPreviewedPet))
            {
                _statusMessage = "CCSWnd_Char::OnTakeoffAvatar has no previewed cash equip to remove.";
                UpdateButtonStates();
                return;
            }

            if (_lastPreviewedPet)
            {
                RestoreInitialPetPreview();
                _previewAssembler = new CharacterAssembler(_previewBuild);
                _statusMessage = "CCSWnd_Char::OnTakeoffAvatar removed the staged pet preview and restored the avatar-look pet snapshot.";
                _lastPreviewedSlot = null;
                _lastPreviewedWeaponSticker = false;
                _lastPreviewedPet = false;
                _previewOwnerState = "CUserPreview kept the avatar-preview layer active after the pet takeoff mutation.";
                _previewMutationState = "Takeoff mutation restored the original pet runtime.";
                RefreshPreviewRuntimeState();
                UpdateButtonStates();
                return;
            }

            CharacterPart removedPart;
            if (_lastPreviewedWeaponSticker)
            {
                removedPart = _previewBuild.WeaponSticker;
                _previewBuild.WeaponSticker = null;
            }
            else
            {
                removedPart = _previewBuild.Unequip(_lastPreviewedSlot.Value);
            }

            _previewAssembler = new CharacterAssembler(_previewBuild);
            _statusMessage = removedPart == null
                ? "CCSWnd_Char::OnTakeoffAvatar found no previewed equip on the selected slot."
                : $"CCSWnd_Char::OnTakeoffAvatar removed {removedPart.Name}.";
            _lastPreviewedSlot = null;
            _lastPreviewedWeaponSticker = false;
            _lastPreviewedPet = false;
            _previewOwnerState = "CUserPreview kept the avatar-preview layer active after the takeoff mutation.";
            _previewMutationState = removedPart == null
                ? "Takeoff mutation found no staged equip."
                : $"Takeoff mutation cleared {removedPart.Slot}.";
            ClearPreviewWearInfo(removedPart?.Slot ?? EquipSlot.None);
            RefreshPreviewRuntimeState();
            UpdateButtonStates();
        }

        private bool TryExecuteServiceAction(out string message)
        {
            message = null;
            if (_currentSelection == null)
            {
                return false;
            }

            string title = _currentSelection.Title ?? string.Empty;
            string detail = _currentSelection.Detail ?? string.Empty;
            int itemGroup = _currentSelection.RewardItemId > 0 ? _currentSelection.RewardItemId / 10000 : 0;
            string combinedText = $"{title} {detail}";

            if (itemGroup == 512 || combinedText.Contains("weather", StringComparison.OrdinalIgnoreCase))
            {
                string weatherMessage = WeatherRequested?.Invoke();
                message = string.IsNullOrWhiteSpace(weatherMessage)
                    ? "CCSWnd_Char::BlowWeather acknowledged the selected cash-weather preview action."
                    : weatherMessage;
                return true;
            }

            if (_currentSelection.IsUserListing)
            {
                string tradeMessage = TradingRoomRequested?.Invoke();
                message = string.IsNullOrWhiteSpace(tradeMessage)
                    ? "CCSWnd_Char preview handed the selected listing to the trading-room seam."
                    : tradeMessage;
                return true;
            }

            if (combinedText.Contains("personal shop", StringComparison.OrdinalIgnoreCase))
            {
                string shopMessage = PersonalShopRequested?.Invoke();
                message = string.IsNullOrWhiteSpace(shopMessage)
                    ? "CCSWnd_Char::ShowPersonalShop acknowledged the selected permit preview."
                    : shopMessage;
                return true;
            }

            if (combinedText.Contains("entrusted", StringComparison.OrdinalIgnoreCase))
            {
                string entrustedMessage = EntrustedShopRequested?.Invoke();
                message = string.IsNullOrWhiteSpace(entrustedMessage)
                    ? "CCSWnd_Char::ShowEntrustedShop acknowledged the selected permit preview."
                    : entrustedMessage;
                return true;
            }

            if (title.Contains("Pet", StringComparison.OrdinalIgnoreCase) || detail.Contains("pet", StringComparison.OrdinalIgnoreCase))
            {
                message = $"CCSWnd_Char::SetPet staged {title} as a pet-side preview action.";
                return true;
            }

            if (itemGroup is 503 or 504 or 505 or 515 or 522 or 568)
            {
                message = $"CCSWnd_Char::ShowMessageBox would own the confirmation flow for {title}.";
                return true;
            }

            return false;
        }

        private void RefreshSelectionState(bool force = false)
        {
            AdminShopAvatarPreviewSelection selection = _selectionProvider?.Invoke();
            string nextSignature = selection == null
                ? string.Empty
                : $"{selection.RewardItemId}|{selection.RewardInventoryType}|{selection.Title}|{selection.IsUserListing}";
            if (!force && string.Equals(_selectionSignature, nextSignature, StringComparison.Ordinal))
            {
                return;
            }

            _selectionSignature = nextSignature;
            _currentSelection = selection;
            if (selection == null)
            {
                RestorePreviewAvatarSnapshot();
                _statusMessage = "CCSWnd_Char preview waiting for a Cash Shop row.";
                _lastPreviewedSlot = null;
                UpdateButtonStates();
                return;
            }

            ApplySelectionPreview(selection);
            UpdateButtonStates();
        }

        private void ApplySelectionPreview(AdminShopAvatarPreviewSelection selection)
        {
            SyncPreviewBuild();
            if (_previewBuild == null)
            {
                _statusMessage = "CCSWnd_Char preview has no character build to render.";
                UpdateButtonStates();
                return;
            }

            if (_equipmentLoader == null || selection.RewardInventoryType != InventoryType.EQUIP)
            {
                _lastPreviewedSlot = null;
                _lastPreviewedWeaponSticker = false;
                _lastPreviewedPet = false;
                if (selection.Title.Contains("Pet", StringComparison.OrdinalIgnoreCase) || selection.Detail.Contains("pet", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyPetPreview(selection);
                }
                else
                {
                    RestorePreviewAvatarSnapshot();
                    _previewOwnerState = "CUserPreview stayed on the current avatar because the selected cash row is non-equip.";
                    _statusMessage = $"CCSWnd_Char kept the current avatar while highlighting {selection.Title}.";
                }

                UpdateButtonStates();
                return;
            }

            CharacterPart loadedPart = _equipmentLoader(selection.RewardItemId)?.Clone();
            if (loadedPart == null)
            {
                RestorePreviewAvatarSnapshot();
                _lastPreviewedSlot = null;
                _lastPreviewedWeaponSticker = false;
                _lastPreviewedPet = false;
                _statusMessage = $"CCSWnd_Char could not resolve an equip preview for {selection.Title}.";
                UpdateButtonStates();
                return;
            }

            ApplyWearPreview(selection, loadedPart);
            UpdateButtonStates();
        }

        private void SyncPreviewBuild(bool forceReset = false)
        {
            if (CharacterBuild == null)
            {
                _previewSourceBuild = null;
                _previewBuild = null;
                _previewAssembler = null;
                UpdateButtonStates();
                return;
            }

            if (forceReset || !ReferenceEquals(_previewSourceBuild, CharacterBuild) || _previewBuild == null)
            {
                _initialAvatarLook = CharacterBuild == null ? null : LoginAvatarLookCodec.CreateLook(CharacterBuild);
                _initialAvatarBuild = CharacterBuild?.Clone();
                ResetPreviewBuild();
            }
        }

        private void ResetPreviewBuild()
        {
            _previewSourceBuild = CharacterBuild;
            _previewBuild = (_initialAvatarBuild ?? CharacterBuild)?.Clone();
            _previewAssembler = _previewBuild == null ? null : new CharacterAssembler(_previewBuild);
            _previewPetItemId = _initialAvatarLook?.PetIds?.Count > 0 ? _initialAvatarLook.PetIds[0] : 0;
            Array.Clear(_previewWearItemIds, 0, _previewWearItemIds.Length);
            _previewWearMutationCount = 0;
            _lastPreviewedPet = false;
            _previewOwnerState = _previewBuild == null
                ? "CUserPreview / physical-space seam unavailable."
                : "CCSWnd_Char created the CUserPreview actor over the 24,40 212x165 preview space.";
            _previewPhysicalSpaceState = _previewBuild == null
                ? "CWvsPhysicalSpace2D unavailable."
                : "CWvsPhysicalSpace2D loaded field / ladderRope and bound the preview layer origin.";
            _previewMutationState = "AvatarLook snapshot seeded into the preview runtime.";
            ApplyClientDefaultAvatarState();
        }

        private void ApplyClientDefaultAvatarState()
        {
            Array.Clear(_previewWearItemIds, 0, _previewWearItemIds.Length);
            _previewWearMutationCount = 0;

            if (_initialAvatarLook?.VisibleEquipmentByBodyPart != null)
            {
                foreach (KeyValuePair<byte, int> entry in _initialAvatarLook.VisibleEquipmentByBodyPart)
                {
                    int slotIndex = entry.Key;
                    if (slotIndex > 0 && slotIndex < _previewWearItemIds.Length)
                    {
                        _previewWearItemIds[slotIndex] = entry.Value;
                    }
                }
            }

            if (_initialAvatarLook?.WeaponStickerItemId > 0)
            {
                RecordPreviewWearInfo(EquipSlot.Weapon, _initialAvatarLook.WeaponStickerItemId);
                _previewWearMutationCount = 0;
            }

            RefreshPreviewRuntimeState();
            _previewResetState = $"AvatarLook snapshot restored with pet {_previewPetItemId} and buy-button re-enabled.";
        }

        private void RefreshPreviewRuntimeState()
        {
            int activeWearEntries = 0;
            for (int i = 0; i < _previewWearItemIds.Length; i++)
            {
                if (_previewWearItemIds[i] > 0)
                {
                    activeWearEntries++;
                }
            }

            int equippedCount = _previewBuild?.Equipment?.Count ?? 0;
            string ridingState = _previewBuild?.HasMonsterRiding == true ? "ride on" : "ride off";
            string effectState = _previewBuild?.WeaponSticker != null ? "weapon sticker active" : "weapon sticker idle";
            string petState = _previewPetItemId > 0
                ? $"pet {_previewPetItemId.ToString()}"
                : "pet idle";
            _previewResetState =
                $"WearInfo {activeWearEntries}/{ClientWearInfoCapacity}  Equip {equippedCount}  Mutations {_previewWearMutationCount}  {ridingState}  {effectState}  {petState}";
            _previewPhysicalSpaceState = $"CWvsPhysicalSpace2D kept the 24,40 212x165 preview lane live after {_previewMutationState}.";
        }

        private void RecordPreviewWearInfo(CharacterPart part)
        {
            if (part == null)
            {
                return;
            }

            RecordPreviewWearInfo(part.Slot, part.ItemId);
        }

        private void RecordPreviewWearInfo(EquipSlot slot, int itemId)
        {
            int slotIndex = (int)slot;
            if (slotIndex > 0 && slotIndex < _previewWearItemIds.Length)
            {
                _previewWearItemIds[slotIndex] = itemId;
            }

            _previewWearMutationCount++;
        }

        private void ClearPreviewWearInfo(EquipSlot slot)
        {
            int slotIndex = (int)slot;
            if (slotIndex > 0 && slotIndex < _previewWearItemIds.Length)
            {
                _previewWearItemIds[slotIndex] = 0;
                _previewWearMutationCount++;
            }
        }

        private Texture2D ResolveBackgroundTexture()
        {
            int previewIndex = ResolvePreviewIndex();
            return previewIndex >= 0 && previewIndex < _previewBackgrounds.Length
                ? _previewBackgrounds[previewIndex]
                : null;
        }

        private void DrawLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            layer?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private int ResolvePreviewIndex()
        {
            int jobId = CharacterBuild?.Job ?? 0;
            int subJob = CharacterBuild?.SubJob ?? 0;
            if (jobId / 1000 == 1 || jobId / 100 == 21 || jobId / 100 == 22 || jobId == 2000 || jobId == 2001)
            {
                return 1;
            }

            if (jobId / 1000 == 3 || (jobId / 1000 == 0 && subJob == 1))
            {
                return 2;
            }

            return 0;
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private string ResolveSelectionLabel()
        {
            if (_currentSelection == null)
            {
                return "Preview owner idle.";
            }

            return _currentSelection.IsUserListing
                ? $"User-side preview: {_currentSelection.Title}"
                : $"NPC-side preview: {_currentSelection.Title}";
        }

        private void ApplyPetPreview(AdminShopAvatarPreviewSelection selection)
        {
            if (_previewBuild == null)
            {
                _statusMessage = "CCSWnd_Char::SetPet has no preview build to mutate.";
                return;
            }

            _previewPetItemId = selection.RewardItemId;
            _previewBuild.RemotePetItemIds = new[] { selection.RewardItemId, 0, 0 };
            _previewAssembler = new CharacterAssembler(_previewBuild);
            _lastPreviewedSlot = null;
            _lastPreviewedWeaponSticker = false;
            _lastPreviewedPet = true;
            _previewOwnerState = $"CUserPreview::SetPet staged pet {selection.RewardItemId.ToString()} on the preview actor.";
            _previewMutationState = $"pet mutation {selection.RewardItemId.ToString()}";
            RefreshPreviewRuntimeState();
            _statusMessage = $"CCSWnd_Char::SetPet staged {selection.Title} on the dedicated preview actor.";
        }

        private void ApplyWearPreview(AdminShopAvatarPreviewSelection selection, CharacterPart loadedPart)
        {
            if (_previewBuild == null || loadedPart == null)
            {
                _statusMessage = $"CCSWnd_Char::OnWear could not preview {selection.Title}.";
                return;
            }

            if (loadedPart.Slot == EquipSlot.Shield
                && EquipSlotStateResolver.ResolveVisualState(_previewBuild, EquipSlot.Shield).Reason == EquipSlotDisableReason.TwoHandedWeapon)
            {
                RestorePreviewAvatarSnapshot();
                _previewOwnerState = "CUserPreview rejected the shield preview because the base avatar still owns a two-handed weapon.";
                _statusMessage = $"CCSWnd_Char::OnWear blocked {selection.Title} while a two-handed weapon is active.";
                UpdateButtonStates();
                return;
            }

            if (loadedPart.Slot == EquipSlot.Weapon && loadedPart.IsCash)
            {
                ApplyWeaponPreview(selection, loadedPart);
                return;
            }

            if (loadedPart.Slot == EquipSlot.Longcoat)
            {
                _previewBuild.Unequip(EquipSlot.Pants);
            }
            else if (loadedPart.Slot == EquipSlot.Pants)
            {
                _previewBuild.Unequip(EquipSlot.Longcoat);
            }

            _previewBuild.PlaceEquipment(loadedPart, loadedPart.Slot);
            if (loadedPart is WeaponPart weaponPart && weaponPart.IsTwoHanded)
            {
                _previewBuild.Unequip(EquipSlot.Shield);
            }

            _previewAssembler = new CharacterAssembler(_previewBuild);
            _lastPreviewedSlot = loadedPart.Slot;
            _lastPreviewedWeaponSticker = false;
            _lastPreviewedPet = false;
            _buyAvatarButton?.SetEnabled(true);
            _previewOwnerState = $"CUserPreview::SetAvatarLook refreshed body-part {(int)loadedPart.Slot} with client-style coat or shield conflict rules.";
            _previewMutationState = $"wear mutation {(int)loadedPart.Slot}";
            RecordPreviewWearInfo(loadedPart);
            RefreshPreviewRuntimeState();
            _statusMessage = $"CCSWnd_Char::OnWear previewed {selection.Title} on {loadedPart.Slot}.";
        }

        private void ApplyWeaponPreview(AdminShopAvatarPreviewSelection selection, CharacterPart loadedPart)
        {
            _previewBuild.WeaponSticker = loadedPart;
            CharacterPart visibleWeapon = EquipSlotStateResolver.GetEquippedPart(_previewBuild, EquipSlot.Weapon);
            CharacterPart underlyingWeapon = EquipSlotStateResolver.ResolveUnderlyingPart(_previewBuild, EquipSlot.Weapon) ?? visibleWeapon;
            if (underlyingWeapon is WeaponPart weaponPart && weaponPart.IsTwoHanded)
            {
                _previewBuild.Unequip(EquipSlot.Shield);
            }

            _previewAssembler = new CharacterAssembler(_previewBuild);
            _lastPreviewedSlot = EquipSlot.Weapon;
            _lastPreviewedWeaponSticker = true;
            _lastPreviewedPet = false;
            _buyAvatarButton?.SetEnabled(true);
            string visibleWeaponLabel = underlyingWeapon?.Name ?? "base weapon";
            _previewOwnerState = $"CCSWnd_Char kept {visibleWeaponLabel} as the avatar weapon and routed the cash item through nWeaponStickerID.";
            _previewMutationState = $"weapon-sticker mutation {selection.RewardItemId.ToString()}";
            RecordPreviewWearInfo(EquipSlot.Weapon, selection.RewardItemId);
            RefreshPreviewRuntimeState();
            _statusMessage = $"CCSWnd_Char::OnWear previewed weapon sticker {selection.Title}.";
        }

        private void UpdateButtonStates()
        {
            bool hasCharacter = CharacterBuild != null;
            bool hasSelection = _currentSelection != null;
            bool hasTakeoffMutation = _lastPreviewedSlot.HasValue || _lastPreviewedPet;

            _buyAvatarButton?.SetEnabled(hasCharacter && hasSelection);
            _defaultAvatarButton?.SetEnabled(hasCharacter);
            _takeoffAvatarButton?.SetEnabled(hasCharacter && hasTakeoffMutation);
        }

        private void RestorePreviewAvatarSnapshot()
        {
            if (_initialAvatarBuild == null && CharacterBuild == null)
            {
                return;
            }

            _previewBuild = (_initialAvatarBuild ?? CharacterBuild)?.Clone();
            _previewAssembler = _previewBuild == null ? null : new CharacterAssembler(_previewBuild);
            _lastPreviewedSlot = null;
            _lastPreviewedWeaponSticker = false;
            _lastPreviewedPet = false;
            ApplyClientDefaultAvatarState();
        }

        private void RestoreInitialPetPreview()
        {
            if (_previewBuild == null)
            {
                return;
            }

            int[] restoredPetIds = new int[3];
            if (_initialAvatarLook?.PetIds != null)
            {
                for (int i = 0; i < restoredPetIds.Length && i < _initialAvatarLook.PetIds.Count; i++)
                {
                    restoredPetIds[i] = _initialAvatarLook.PetIds[i];
                }
            }

            _previewPetItemId = restoredPetIds[0];
            _previewBuild.RemotePetItemIds = restoredPetIds;
        }

        private static string TrimForDisplay(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static IDXObject CreateFrame(GraphicsDevice device)
        {
            Texture2D texture = new Texture2D(device, FrameWidth, FrameHeight);
            Color[] pixels = new Color[FrameWidth * FrameHeight];
            Array.Fill(pixels, Color.Transparent);
            texture.SetData(pixels);
            return new DXObject(0, 0, texture, 0);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                yield break;
            }

            List<string> lines = new();
            string current = string.Empty;
            foreach (string word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (!string.IsNullOrEmpty(current) && _font.MeasureString(candidate).X > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            foreach (string line in lines)
            {
                yield return line;
            }
        }
    }
}
