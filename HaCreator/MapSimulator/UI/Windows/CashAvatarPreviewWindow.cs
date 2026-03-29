using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashAvatarPreviewWindow : UIWindowBase
    {
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

        private readonly Texture2D[] _previewBackgrounds;
        private readonly UIObject _buyAvatarButton;
        private readonly UIObject _defaultAvatarButton;
        private readonly UIObject _takeoffAvatarButton;

        private SpriteFont _font;
        private Func<AdminShopAvatarPreviewSelection> _selectionProvider;
        private Func<bool> _shopRequestHandler;
        private CharacterBuild _previewBuild;
        private CharacterAssembler _previewAssembler;
        private CharacterBuild _previewSourceBuild;
        private Func<int, CharacterPart> _equipmentLoader;
        private string _selectionSignature = string.Empty;
        private string _statusMessage = "CCSWnd_Char preview idle.";
        private AdminShopAvatarPreviewSelection _currentSelection;
        private EquipSlot? _lastPreviewedSlot;

        public CashAvatarPreviewWindow(
            GraphicsDevice device,
            Texture2D[] previewBackgrounds,
            UIObject buyAvatarButton,
            UIObject defaultAvatarButton,
            UIObject takeoffAvatarButton)
            : base(CreateFrame(device))
        {
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

        public void SetShopRequestHandler(Func<bool> shopRequestHandler)
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
            _selectionSignature = string.Empty;
            SyncPreviewBuild(forceReset: true);
            RefreshSelectionState(force: true);
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

            float lineY = Position.Y + 210;
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

            if (_shopRequestHandler?.Invoke() == true)
            {
                _statusMessage = $"CCSWnd_Char::OnWear submitted {_currentSelection.Title} through the Cash Shop request seam.";
                return;
            }

            _statusMessage = $"CCSWnd_Char::OnWear could not submit {_currentSelection.Title}.";
        }

        private void HandleDefaultAvatar()
        {
            if (CharacterBuild == null)
            {
                _statusMessage = "CCSWnd_Char::OnDefaultAvatar is unavailable without a live character build.";
                return;
            }

            ResetPreviewBuild();
            _lastPreviewedSlot = null;
            _statusMessage = "CCSWnd_Char::OnDefaultAvatar restored the live character appearance.";
        }

        private void HandleTakeoffAvatar()
        {
            SyncPreviewBuild();
            if (_previewBuild == null || !_lastPreviewedSlot.HasValue)
            {
                _statusMessage = "CCSWnd_Char::OnTakeoffAvatar has no previewed cash equip to remove.";
                return;
            }

            CharacterPart removedPart = _previewBuild.Unequip(_lastPreviewedSlot.Value);
            _previewAssembler = new CharacterAssembler(_previewBuild);
            _statusMessage = removedPart == null
                ? "CCSWnd_Char::OnTakeoffAvatar found no previewed equip on the selected slot."
                : $"CCSWnd_Char::OnTakeoffAvatar removed {removedPart.Name}.";
            _lastPreviewedSlot = null;
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
                _statusMessage = "CCSWnd_Char preview waiting for a Cash Shop row.";
                _lastPreviewedSlot = null;
                return;
            }

            ApplySelectionPreview(selection);
        }

        private void ApplySelectionPreview(AdminShopAvatarPreviewSelection selection)
        {
            SyncPreviewBuild();
            if (_previewBuild == null)
            {
                _statusMessage = "CCSWnd_Char preview has no character build to render.";
                return;
            }

            if (_equipmentLoader == null || selection.RewardInventoryType != InventoryType.EQUIP)
            {
                _lastPreviewedSlot = null;
                if (selection.Title.Contains("Pet", StringComparison.OrdinalIgnoreCase) || selection.Detail.Contains("pet", StringComparison.OrdinalIgnoreCase))
                {
                    _statusMessage = $"CCSWnd_Char::SetPet staged {selection.Title} without changing avatar equipment.";
                }
                else
                {
                    _statusMessage = $"CCSWnd_Char kept the current avatar while highlighting {selection.Title}.";
                }

                return;
            }

            CharacterPart loadedPart = _equipmentLoader(selection.RewardItemId)?.Clone();
            if (loadedPart == null)
            {
                _lastPreviewedSlot = null;
                _statusMessage = $"CCSWnd_Char could not resolve an equip preview for {selection.Title}.";
                return;
            }

            _previewBuild.PlaceEquipment(loadedPart, loadedPart.Slot);
            _previewAssembler = new CharacterAssembler(_previewBuild);
            _lastPreviewedSlot = loadedPart.Slot;
            _statusMessage = $"CCSWnd_Char::OnWear previewed {selection.Title} on {loadedPart.Slot}.";
        }

        private void SyncPreviewBuild(bool forceReset = false)
        {
            if (CharacterBuild == null)
            {
                _previewSourceBuild = null;
                _previewBuild = null;
                _previewAssembler = null;
                return;
            }

            if (forceReset || !ReferenceEquals(_previewSourceBuild, CharacterBuild) || _previewBuild == null)
            {
                ResetPreviewBuild();
            }
        }

        private void ResetPreviewBuild()
        {
            _previewSourceBuild = CharacterBuild;
            _previewBuild = CharacterBuild?.Clone();
            _previewAssembler = _previewBuild == null ? null : new CharacterAssembler(_previewBuild);
        }

        private Texture2D ResolveBackgroundTexture()
        {
            int previewIndex = ResolvePreviewIndex();
            return previewIndex >= 0 && previewIndex < _previewBackgrounds.Length
                ? _previewBackgrounds[previewIndex]
                : null;
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
