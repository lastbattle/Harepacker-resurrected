using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class VegaSpellUI : UIWindowBase
    {
        private const int CancelButtonX = 159;
        private const int CancelButtonY = 5;
        private const int StartButtonX = 67;
        private const int StartButtonY = 182;
        private const int OkButtonX = 67;
        private const int OkButtonY = 182;
        private const int PrevButtonX = 7;
        private const int PrevButtonY = 100;
        private const int NextButtonX = 63;
        private const int NextButtonY = 100;
        private const int LeftIconX = 29;
        private const int LeftIconY = 94;
        private const int RightIconX = 117;
        private const int RightIconY = 94;
        private const int ResultWindowX = 13;
        private const int ResultWindowY = 40;
        private const int ResultIconX = 60;
        private const int ResultIconY = 66;
        private const int EffectCenterX = 89;
        private const int EffectCenterY = 111;
        private const int StatusTextX = 13;
        private const int StatusTextY = 205;

        private readonly GraphicsDevice _device;
        private IDXObject _frame10;
        private IDXObject _frame60;
        private Texture2D _successWindow;
        private Texture2D _failWindow;
        private Texture2D[] _digitTextures = Array.Empty<Texture2D>();
        private readonly Dictionary<int, Texture2D> _itemIconCache = new Dictionary<int, Texture2D>();
        private IReadOnlyList<VegaAnimationFrame> _twinklingFrames = Array.Empty<VegaAnimationFrame>();
        private IReadOnlyList<VegaAnimationFrame> _spellingFrames = Array.Empty<VegaAnimationFrame>();
        private IReadOnlyList<VegaAnimationFrame> _arrowFrames = Array.Empty<VegaAnimationFrame>();
        private IReadOnlyList<VegaAnimationFrame> _successFrames = Array.Empty<VegaAnimationFrame>();
        private IReadOnlyList<VegaAnimationFrame> _failFrames = Array.Empty<VegaAnimationFrame>();
        private SpriteFont _font;
        private ItemUpgradeUI _itemUpgradeBackend;
        private ProductionEnhancementAnimationDisplayer _productionEnhancementAnimationDisplayer;
        private int? _modifierItemId;
        private int _selectedIndex;
        private string _statusMessage = "Select equipment and cast Vega's Spell.";
        private VegaAnimationState _state = VegaAnimationState.Idle;
        private ItemUpgradeUI.ItemUpgradeAttemptResult _pendingResult;
        private int _stateElapsedMs;
        private bool _sharedResultPreludeStarted;
        private bool _sharedResultPopupStarted;
        private UIObject _startButton;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private UIObject _prevButton;
        private UIObject _nextButton;

        public VegaSpellUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;
            _frame10 = frame;
            _frame60 = frame;
        }

        public override string WindowName => MapSimulatorWindowNames.VegaSpell;

        public override CharacterBuild CharacterBuild
        {
            get;
            set;
        }

        public Func<VegaOwnerRequest, bool> StartSpellCastRequested
        {
            get;
            set;
        }

        public Action<VegaOwnerValidationFailure> ValidationFailed
        {
            get;
            set;
        }

        public Action ResultAcknowledged
        {
            get;
            set;
        }

        public Action ResultPreludeStarted
        {
            get;
            set;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public void SetFrames(IDXObject frame10, IDXObject frame60)
        {
            _frame10 = frame10 ?? _frame10;
            _frame60 = frame60 ?? _frame60;
            RefreshFrame();
        }

        public void SetResultTextures(Texture2D successWindow, Texture2D failWindow)
        {
            _successWindow = successWindow;
            _failWindow = failWindow;
        }

        public void SetDigitTextures(Texture2D[] digitTextures)
        {
            _digitTextures = digitTextures ?? Array.Empty<Texture2D>();
        }

        public void SetEffectFrames(
            IReadOnlyList<VegaAnimationFrame> twinklingFrames,
            IReadOnlyList<VegaAnimationFrame> spellingFrames,
            IReadOnlyList<VegaAnimationFrame> arrowFrames,
            IReadOnlyList<VegaAnimationFrame> successFrames,
            IReadOnlyList<VegaAnimationFrame> failFrames)
        {
            _twinklingFrames = twinklingFrames ?? Array.Empty<VegaAnimationFrame>();
            _spellingFrames = spellingFrames ?? Array.Empty<VegaAnimationFrame>();
            _arrowFrames = arrowFrames ?? Array.Empty<VegaAnimationFrame>();
            _successFrames = successFrames ?? Array.Empty<VegaAnimationFrame>();
            _failFrames = failFrames ?? Array.Empty<VegaAnimationFrame>();
        }

        public void SetItemUpgradeBackend(ItemUpgradeUI itemUpgradeBackend)
        {
            _itemUpgradeBackend = itemUpgradeBackend;
        }

        internal void SetProductionEnhancementAnimationDisplayer(ProductionEnhancementAnimationDisplayer animationDisplayer)
        {
            _productionEnhancementAnimationDisplayer = animationDisplayer;
        }

        public void InitializeButtons(UIObject startButton, UIObject okButton, UIObject cancelButton, UIObject prevButton, UIObject nextButton)
        {
            _startButton = startButton;
            _okButton = okButton;
            _cancelButton = cancelButton;
            _prevButton = prevButton;
            _nextButton = nextButton;

            if (_startButton != null)
            {
                _startButton.X = StartButtonX;
                _startButton.Y = StartButtonY;
                AddButton(_startButton);
                _startButton.ButtonClickReleased += _ => BeginSpellCast();
            }

            if (_okButton != null)
            {
                _okButton.X = OkButtonX;
                _okButton.Y = OkButtonY;
                AddButton(_okButton);
                _okButton.ButtonClickReleased += _ => ResetResultState();
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = CancelButtonX;
                _cancelButton.Y = CancelButtonY;
                AddButton(_cancelButton);
                _cancelButton.ButtonClickReleased += _ => Hide();
            }

            if (_prevButton != null)
            {
                _prevButton.X = PrevButtonX;
                _prevButton.Y = PrevButtonY;
                AddButton(_prevButton);
                _prevButton.ButtonClickReleased += _ => MoveSelection(-1);
            }

            if (_nextButton != null)
            {
                _nextButton.X = NextButtonX;
                _nextButton.Y = NextButtonY;
                AddButton(_nextButton);
                _nextButton.ButtonClickReleased += _ => MoveSelection(1);
            }

            UpdateButtonStates();
        }

        public void PrepareModifierSelection(int modifierItemId)
        {
            _modifierItemId = modifierItemId;
            _selectedIndex = 0;
            _state = VegaAnimationState.Idle;
            _stateElapsedMs = 0;
            _pendingResult = default;
            _sharedResultPreludeStarted = false;
            _sharedResultPopupStarted = false;
            ClampSelection();
            RefreshFrame();
            _statusMessage = BuildReadyMessage();
            UpdateButtonStates();
        }

        public void SetOwnerStatusMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
                UpdateButtonStates();
            }
        }

        public void RestoreReadyStatusMessage()
        {
            if (_state == VegaAnimationState.Idle)
            {
                _statusMessage = BuildReadyMessage();
                UpdateButtonStates();
            }
        }

        public int GetCastingDurationMs()
        {
            return ResolveCastingDuration();
        }

        public void StartSpellCast(string statusMessage)
        {
            _state = VegaAnimationState.Casting;
            _stateElapsedMs = 0;
            _pendingResult = default;
            _sharedResultPreludeStarted = false;
            _sharedResultPopupStarted = false;
            _statusMessage = string.IsNullOrWhiteSpace(statusMessage)
                ? $"Casting {ResolveModifierName()}..."
                : statusMessage;
            _productionEnhancementAnimationDisplayer?.PlayVegaCasting(Environment.TickCount);
            UpdateButtonStates();
        }

        public void ApplyResolvedSpellResult(ItemUpgradeUI.ItemUpgradeAttemptResult result)
        {
            _pendingResult = result;
            PromotePendingResultIfReady();
            UpdateButtonStates();
        }

        public bool TryBuildSelectedRequest(out VegaOwnerRequest request)
        {
            return TryBuildSelectedRequest(out request, out _);
        }

        public override void Show()
        {
            base.Show();
            RefreshFrame();
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            ClampSelection();

            if (_state != VegaAnimationState.Idle)
            {
                _stateElapsedMs += (int)gameTime.ElapsedGameTime.TotalMilliseconds;
            }

            PromotePendingResultIfReady();

            UpdateButtonStates();
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
            int windowX = Position.X;
            int windowY = Position.Y;
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();

            DrawRates(sprite, windowX, windowY);
            DrawShadowedText(sprite, _statusMessage, new Vector2(windowX + StatusTextX, windowY + StatusTextY), ResolveStatusColor());

            if (candidates.Count == 0)
            {
                DrawShadowedText(sprite, "No eligible equipment is available.", new Vector2(windowX + 14, windowY + 166), Color.White);
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            CharacterPart selectedPart = selection.Value;
            DrawItemIcon(sprite, selectedPart?.IconRaw ?? selectedPart?.Icon, windowX + LeftIconX, windowY + LeftIconY);

            if (_modifierItemId.HasValue &&
                _itemUpgradeBackend != null &&
                _itemUpgradeBackend.TryGetModifierPreview(selection.Key, _modifierItemId.Value, out ItemUpgradeUI.ModifierPreview preview))
            {
                DrawModifierPreview(sprite, windowX, windowY, preview);
            }
            else
            {
                DrawShadowedText(sprite, "No compatible scroll", new Vector2(windowX + 95, windowY + 106), new Color(255, 190, 190));
            }

            DrawShadowedText(sprite, ResolveItemName(selectedPart), new Vector2(windowX + 14, windowY + 151), new Color(255, 232, 150));
            DrawShadowedText(sprite, $"{selection.Key} [{_selectedIndex + 1}/{candidates.Count}]", new Vector2(windowX + 14, windowY + 166), Color.White);

            if (_state == VegaAnimationState.Casting)
            {
                if (_productionEnhancementAnimationDisplayer == null)
                {
                    DrawCastingEffects(sprite, windowX, windowY);
                }
            }
            else if (_state == VegaAnimationState.ResultPrelude)
            {
                if (_productionEnhancementAnimationDisplayer == null)
                {
                    DrawResultPrelude(sprite, windowX, windowY);
                }
            }
            else if (_state == VegaAnimationState.Result)
            {
                DrawResult(sprite, windowX, windowY, selectedPart);
            }
        }

        private void DrawRates(SpriteBatch sprite, int windowX, int windowY)
        {
            (int baseRate, int modifiedRate) = ResolveDisplayedRates();
            DrawNumber(sprite, windowX + 34, windowY + 32, baseRate);
            DrawNumber(sprite, windowX + 124, windowY + 32, modifiedRate);
        }

        private void DrawModifierPreview(SpriteBatch sprite, int windowX, int windowY, ItemUpgradeUI.ModifierPreview preview)
        {
            Texture2D scrollIcon = TryResolveItemIcon(preview.ConsumableItemId);
            DrawItemIcon(sprite, scrollIcon, windowX + RightIconX, windowY + RightIconY);

            string countText = $"x{preview.ConsumableCount}";
            DrawShadowedText(sprite, countText, new Vector2(windowX + 128, windowY + 128), new Color(255, 243, 190));

            string scrollName = preview.ConsumableName.Length > 15
                ? preview.ConsumableName.Substring(0, 15)
                : preview.ConsumableName;
            DrawShadowedText(sprite, scrollName, new Vector2(windowX + 82, windowY + 151), new Color(205, 225, 255));
        }

        private void DrawCastingEffects(SpriteBatch sprite, int windowX, int windowY)
        {
            int elapsed = _stateElapsedMs;
            int twinklingDuration = GetAnimationDuration(_twinklingFrames);
            int spellingDuration = GetAnimationDuration(_spellingFrames);
            int arrowDuration = GetAnimationDuration(_arrowFrames);

            DrawAnimation(sprite, _twinklingFrames, elapsed, windowX, windowY);
            if (elapsed >= twinklingDuration)
            {
                DrawAnimation(sprite, _spellingFrames, elapsed - twinklingDuration, windowX, windowY);
            }

            if (elapsed >= twinklingDuration + Math.Max(500, spellingDuration / 2))
            {
                DrawAnimation(sprite, _arrowFrames, elapsed - twinklingDuration - Math.Max(500, spellingDuration / 2), windowX, windowY);
            }
        }

        private void DrawResultPrelude(SpriteBatch sprite, int windowX, int windowY)
        {
            int elapsed = _stateElapsedMs;
            DrawAnimation(sprite, _twinklingFrames, elapsed, windowX, windowY);
            DrawAnimation(sprite, _arrowFrames, elapsed, windowX, windowY);
        }

        private void DrawResult(SpriteBatch sprite, int windowX, int windowY, CharacterPart selectedPart)
        {
            Texture2D resultWindow = _pendingResult.Success == true ? _successWindow : _failWindow;
            if (resultWindow != null)
            {
                sprite.Draw(resultWindow, new Vector2(windowX + ResultWindowX, windowY + ResultWindowY), Color.White);
            }

            DrawItemIcon(sprite, selectedPart?.IconRaw ?? selectedPart?.Icon, windowX + ResultIconX, windowY + ResultIconY);
            if (_productionEnhancementAnimationDisplayer == null)
            {
                DrawAnimation(sprite, _pendingResult.Success == true ? _successFrames : _failFrames, _stateElapsedMs, windowX, windowY);
            }
        }

        private void DrawAnimation(SpriteBatch sprite, IReadOnlyList<VegaAnimationFrame> frames, int elapsedMs, int windowX, int windowY)
        {
            VegaAnimationFrame frame = SelectFrame(frames, elapsedMs);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position = new Vector2(
                windowX + EffectCenterX + frame.Offset.X,
                windowY + EffectCenterY + frame.Offset.Y);
            sprite.Draw(frame.Texture, position, Color.White);
        }

        private VegaAnimationFrame SelectFrame(IReadOnlyList<VegaAnimationFrame> frames, int elapsedMs)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int animationDuration = GetAnimationDuration(frames);
            if (animationDuration <= 0)
            {
                return frames[0];
            }

            int localElapsed = Math.Max(0, elapsedMs) % animationDuration;
            int running = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                running += Math.Max(1, frames[i].DelayMs);
                if (localElapsed < running)
                {
                    return frames[i];
                }
            }

            return frames[^1];
        }

        private static int GetAnimationDuration(IReadOnlyList<VegaAnimationFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int duration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                duration += Math.Max(1, frames[i].DelayMs);
            }

            return duration;
        }

        private int ResolveCastingDuration()
        {
            return GetAnimationDuration(_twinklingFrames) +
                   GetAnimationDuration(_spellingFrames) +
                   Math.Max(500, GetAnimationDuration(_arrowFrames));
        }

        private void DrawNumber(SpriteBatch sprite, int x, int y, int value)
        {
            string text = Math.Max(0, value).ToString();
            int drawX = x;
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit >= _digitTextures.Length || _digitTextures[digit] == null)
                {
                    DrawShadowedText(sprite, text, new Vector2(x, y), Color.White);
                    return;
                }

                Texture2D texture = _digitTextures[digit];
                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width - 1;
            }
        }

        private void DrawItemIcon(SpriteBatch sprite, IDXObject icon, int x, int y)
        {
            icon?.DrawBackground(sprite, null, null, x, y, Color.White, false, null);
        }

        private void DrawItemIcon(SpriteBatch sprite, Texture2D icon, int x, int y)
        {
            if (icon != null)
            {
                sprite.Draw(icon, new Rectangle(x, y, 32, 32), Color.White);
            }
        }

        private Texture2D TryResolveItemIcon(int itemId)
        {
            if (_device == null || HaCreator.Program.InfoManager?.ItemIconCache == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedIcon))
            {
                return cachedIcon;
            }

            if (!HaCreator.Program.InfoManager.ItemIconCache.TryGetValue(itemId, out var canvas) || canvas == null)
            {
                _itemIconCache[itemId] = null;
                return null;
            }

            Texture2D texture = canvas.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(_device);
            _itemIconCache[itemId] = texture;
            return texture;
        }

        private void BeginSpellCast()
        {
            if (!TryBuildSelectedRequest(out VegaOwnerRequest request, out VegaOwnerValidationFailure failure))
            {
                ValidationFailed?.Invoke(failure);
                return;
            }

            if (StartSpellCastRequested != null)
            {
                StartSpellCastRequested(request);
                return;
            }

            _itemUpgradeBackend.PrepareEquipmentSelection(request.Slot);
            _itemUpgradeBackend.PrepareConsumableSelection(request.ModifierItemId);
            ItemUpgradeUI.ItemUpgradeAttemptResult result = _itemUpgradeBackend.TryApplyPreparedUpgrade();
            if (!result.Success.HasValue)
            {
                _statusMessage = result.StatusMessage;
                return;
            }

            StartSpellCast($"Casting {ResolveModifierName()}...");
            ApplyResolvedSpellResult(result);
        }

        private void ResetResultState()
        {
            ResultAcknowledged?.Invoke();
            _state = VegaAnimationState.Idle;
            _stateElapsedMs = 0;
            _pendingResult = default;
            _sharedResultPreludeStarted = false;
            _sharedResultPopupStarted = false;
            _statusMessage = BuildReadyMessage();
            UpdateButtonStates();
        }

        private void MoveSelection(int delta)
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0 || _state != VegaAnimationState.Idle)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + delta) % candidates.Count;
            if (_selectedIndex < 0)
            {
                _selectedIndex += candidates.Count;
            }

            _statusMessage = BuildReadyMessage();
        }

        private void ClampSelection()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            _selectedIndex = Math.Clamp(_selectedIndex, 0, candidates.Count - 1);
        }

        private void UpdateButtonStates()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            bool idle = _state == VegaAnimationState.Idle;
            bool hasCandidates = candidates.Count > 0;
            bool hasCompatiblePreview = false;
            if (idle &&
                hasCandidates &&
                _modifierItemId.HasValue &&
                _itemUpgradeBackend != null)
            {
                hasCompatiblePreview = _itemUpgradeBackend.TryGetModifierPreview(candidates[_selectedIndex].Key, _modifierItemId.Value, out _);
            }

            if (_startButton != null)
            {
                _startButton.SetEnabled(idle && hasCompatiblePreview);
                _startButton.ButtonVisible = idle;
            }

            if (_okButton != null)
            {
                _okButton.SetEnabled(_state == VegaAnimationState.Result);
                _okButton.ButtonVisible = _state == VegaAnimationState.Result;
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetEnabled(idle);
            }

            bool canCycle = idle && candidates.Count > 1;
            if (_prevButton != null)
            {
                _prevButton.SetEnabled(canCycle);
                _prevButton.ButtonVisible = canCycle;
            }

            if (_nextButton != null)
            {
                _nextButton.SetEnabled(canCycle);
                _nextButton.ButtonVisible = canCycle;
            }
        }

        private void RefreshFrame()
        {
            Frame = _modifierItemId == 5610001 ? _frame60 : _frame10;
        }

        private IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> GetCandidates()
        {
            if (CharacterBuild?.Equipment == null || CharacterBuild.Equipment.Count == 0)
            {
                return Array.Empty<KeyValuePair<EquipSlot, CharacterPart>>();
            }

            return CharacterBuild.Equipment
                .Where(entry => entry.Value != null &&
                                entry.Key != EquipSlot.None &&
                                ItemUpgradeUI.CanUpgrade(entry.Key, entry.Value) &&
                                (!_modifierItemId.HasValue ||
                                 _itemUpgradeBackend == null ||
                                 _itemUpgradeBackend.TryGetModifierPreview(entry.Key, _modifierItemId.Value, out _)))
                .OrderBy(entry => entry.Key)
                .ToArray();
        }

        private string BuildReadyMessage()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return "No eligible equipment is available for Vega's Spell.";
            }

            CharacterPart selectedPart = candidates[_selectedIndex].Value;
            return $"{ResolveModifierName()} is ready for {ResolveItemName(selectedPart)}.";
        }

        private string ResolveModifierName()
        {
            if (_modifierItemId.HasValue &&
                InventoryItemMetadataResolver.TryResolveItemName(_modifierItemId.Value, out string itemName) &&
                !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }

            return _modifierItemId.HasValue
                ? (_modifierItemId.Value == 5610001 ? "Vega's Spell(60%)" : "Vega's Spell(10%)")
                : "Vega's Spell";
        }

        private (int baseRate, int modifiedRate) ResolveDisplayedRates()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count > 0 &&
                _modifierItemId.HasValue &&
                _itemUpgradeBackend != null &&
                _itemUpgradeBackend.TryGetModifierPreview(candidates[_selectedIndex].Key, _modifierItemId.Value, out ItemUpgradeUI.ModifierPreview preview))
            {
                return ((int)Math.Round(preview.BaseSuccessRate * 100f), (int)Math.Round(preview.ModifiedSuccessRate * 100f));
            }

            if (_modifierItemId.HasValue &&
                ItemUpgradeUI.TryResolveVegaModifierRatePreview(_modifierItemId.Value, out int requiredBasePercent, out int modifiedPercent))
            {
                return (requiredBasePercent, modifiedPercent);
            }

            return _modifierItemId == 5610001 ? (60, 90) : (10, 30);
        }

        private Color ResolveStatusColor()
        {
            return _state switch
            {
                VegaAnimationState.Casting => new Color(255, 232, 150),
                VegaAnimationState.ResultPrelude when _pendingResult.Success == true => new Color(160, 255, 160),
                VegaAnimationState.ResultPrelude => new Color(255, 170, 170),
                VegaAnimationState.Result when _pendingResult.Success == true => new Color(160, 255, 160),
                VegaAnimationState.Result => new Color(255, 170, 170),
                _ => new Color(220, 220, 220)
            };
        }

        private static string ResolveItemName(CharacterPart part)
        {
            return string.IsNullOrWhiteSpace(part?.Name) ? $"Equip {part?.ItemId ?? 0}" : part.Name;
        }

        private bool TryBuildSelectedRequest(out VegaOwnerRequest request, out VegaOwnerValidationFailure failure)
        {
            request = default;
            failure = VegaOwnerValidationFailure.MissingSelection;

            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0 || !_modifierItemId.HasValue || _itemUpgradeBackend == null)
            {
                _statusMessage = "No eligible equipment is available for Vega's Spell.";
                return false;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            if (!_itemUpgradeBackend.TryGetVegaRequestPreview(selection.Key, _modifierItemId.Value, out ItemUpgradeUI.VegaRequestPreview preview))
            {
                failure = VegaOwnerValidationFailure.IncompatiblePair;
                _statusMessage = "No compatible enhancement scroll is available for the selected Vega modifier.";
                return false;
            }

            CharacterPart selectedPart = selection.Value;
            request = new VegaOwnerRequest(
                selection.Key,
                selectedPart?.ItemId ?? 0,
                ResolveItemName(selectedPart),
                _modifierItemId.Value,
                ResolveModifierName(),
                preview.ConsumableItemId,
                preview.ConsumableName,
                preview.ConsumableCount,
                (int)Math.Round(preview.BaseSuccessRate * 100f),
                (int)Math.Round(preview.ModifiedSuccessRate * 100f),
                preview.RequiresDestroyWarning);
            return true;
        }

        private void PromotePendingResultIfReady()
        {
            if (_state != VegaAnimationState.Casting ||
                !_pendingResult.Success.HasValue ||
                _stateElapsedMs < ResolveCastingDuration())
            {
                PromoteResultPopupIfReady();
                return;
            }

            _state = VegaAnimationState.ResultPrelude;
            _stateElapsedMs = 0;
            _statusMessage = _pendingResult.StatusMessage;
            if (!_sharedResultPreludeStarted)
            {
                _sharedResultPreludeStarted = true;
                ResultPreludeStarted?.Invoke();
                _productionEnhancementAnimationDisplayer?.PlayVegaResultPrelude(Environment.TickCount);
            }

            PromoteResultPopupIfReady();
        }

        private void PromoteResultPopupIfReady()
        {
            if (_state != VegaAnimationState.ResultPrelude ||
                _stateElapsedMs < ResolveResultPreludeDuration())
            {
                return;
            }

            _state = VegaAnimationState.Result;
            _stateElapsedMs = 0;
            if (!_sharedResultPopupStarted)
            {
                _sharedResultPopupStarted = true;
                _productionEnhancementAnimationDisplayer?.PlayVegaResultPopup(_pendingResult.Success == true, Environment.TickCount);
            }
        }

        private int ResolveResultPreludeDuration()
        {
            return _productionEnhancementAnimationDisplayer?.GetVegaResultPreludeDurationMs()
                ?? Math.Max(GetAnimationDuration(_twinklingFrames), GetAnimationDuration(_arrowFrames));
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            ClientTextDrawing.DrawShadowed(sprite, text, position, color, _font);
        }

        private enum VegaAnimationState
        {
            Idle,
            Casting,
            ResultPrelude,
            Result
        }

        public sealed class VegaAnimationFrame
        {
            public VegaAnimationFrame(Texture2D texture, Point offset, int delayMs)
            {
                Texture = texture;
                Offset = offset;
                DelayMs = delayMs;
            }

            public Texture2D Texture { get; }
            public Point Offset { get; }
            public int DelayMs { get; }
        }

        public readonly struct VegaOwnerRequest
        {
            public VegaOwnerRequest(
                EquipSlot slot,
                int equipItemId,
                string equipName,
                int modifierItemId,
                string modifierName,
                int scrollItemId,
                string scrollName,
                int scrollCount,
                int baseSuccessRate,
                int modifiedSuccessRate,
                bool requiresWhiteScrollPrompt)
            {
                Slot = slot;
                EquipItemId = equipItemId;
                EquipName = equipName ?? string.Empty;
                ModifierItemId = modifierItemId;
                ModifierName = modifierName ?? string.Empty;
                ScrollItemId = scrollItemId;
                ScrollName = scrollName ?? string.Empty;
                ScrollCount = Math.Max(0, scrollCount);
                BaseSuccessRate = Math.Max(0, baseSuccessRate);
                ModifiedSuccessRate = Math.Max(0, modifiedSuccessRate);
                RequiresWhiteScrollPrompt = requiresWhiteScrollPrompt;
            }

            public EquipSlot Slot { get; }
            public int EquipItemId { get; }
            public string EquipName { get; }
            public int ModifierItemId { get; }
            public string ModifierName { get; }
            public int ScrollItemId { get; }
            public string ScrollName { get; }
            public int ScrollCount { get; }
            public int BaseSuccessRate { get; }
            public int ModifiedSuccessRate { get; }
            public bool RequiresWhiteScrollPrompt { get; }
        }

        public enum VegaOwnerValidationFailure
        {
            MissingSelection,
            IncompatiblePair
        }
    }
}
