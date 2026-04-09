using System;
using System.Collections.Generic;
using System.Text;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NpcInteractionOverlay
    {
        private const int WindowWidth = 560;
        private const int WindowHeight = 286;
        private const int Padding = 18;
        private const int CloseButtonSize = 22;
        private const int ButtonWidth = 84;
        private const int ButtonHeight = 28;
        private const int ButtonGap = 10;
        private const int EntryListWidth = 172;
        private const int ChoiceButtonWidth = 94;

        private readonly Texture2D _pixel;
        private readonly List<NpcInteractionEntry> _entries = new();
        private readonly Stack<PageContext> _pageContextStack = new();
        private readonly Dictionary<TextRenderCacheKey, Texture2D> _textTextureCache = new();
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _fallbackFont;
        private readonly float _fallbackLineHeight;
        private bool _packetQuestResultVisualAssetsLoaded;
        private Texture2D _packetQuestResultTopTexture;
        private Texture2D _packetQuestResultCenterTexture;
        private Texture2D _packetQuestResultBottomTexture;
        private Texture2D _packetQuestResultSeparatorTexture;
        private Texture2D _packetQuestResultSpeakerBarTexture;
        private UtilDialogButtonTextures _packetQuestResultPrevButtonTextures;
        private UtilDialogButtonTextures _packetQuestResultNextButtonTextures;
        private UtilDialogButtonTextures _packetQuestResultOkButtonTextures;
        private Texture2D _packetQuestResultSpeakerTexture;
        private int _packetQuestResultSpeakerTemplateId;

        private SpriteFont _font;
        private string _npcName = "NPC";
        private NpcInteractionPresentationStyle _presentationStyle;
        private int _selectedEntryIndex;
        private int _currentPage;
        private IReadOnlyList<NpcInteractionPage> _currentPages = Array.Empty<NpcInteractionPage>();
        private string _inputValue = string.Empty;
        private string _compositionText = string.Empty;
        private bool _isPrevButtonHovered;
        private bool _isNextButtonHovered;
        private bool _isPrevButtonPressed;
        private bool _isNextButtonPressed;
        private bool _isPacketQuestResultPrevButtonFocused;

        private readonly struct PageContext
        {
            public PageContext(IReadOnlyList<NpcInteractionPage> pages, int pageIndex)
            {
                Pages = pages;
                PageIndex = pageIndex;
            }

            public IReadOnlyList<NpcInteractionPage> Pages { get; }
            public int PageIndex { get; }
        }

        private readonly struct TextRenderCacheKey : IEquatable<TextRenderCacheKey>
        {
            public TextRenderCacheKey(string text, XnaColor color)
            {
                Text = text ?? string.Empty;
                Color = color.PackedValue;
            }

            public string Text { get; }
            public uint Color { get; }

            public bool Equals(TextRenderCacheKey other)
            {
                return Color == other.Color && string.Equals(Text, other.Text, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TextRenderCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Text, Color);
            }
        }

        private readonly struct UtilDialogButtonTextures
        {
            public UtilDialogButtonTextures(
                Texture2D normal,
                Texture2D disabled,
                Texture2D mouseOver,
                Texture2D pressed,
                Texture2D keyFocused)
            {
                Normal = normal;
                Disabled = disabled;
                MouseOver = mouseOver;
                Pressed = pressed;
                KeyFocused = keyFocused;
            }

            public Texture2D Normal { get; }
            public Texture2D Disabled { get; }
            public Texture2D MouseOver { get; }
            public Texture2D Pressed { get; }
            public Texture2D KeyFocused { get; }
        }

        public NpcInteractionOverlay(GraphicsDevice device)
        {
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _measureBitmap = new SD.Bitmap(1, 1);
            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;
            _fallbackFont = new SD.Font("Segoe UI", 13f, SD.FontStyle.Regular, SD.GraphicsUnit.Point);
            _fallbackLineHeight = MeasureFallbackText("Ag").Y;
        }

        public bool IsVisible { get; private set; }
        public bool CapturesKeyboardInput => IsVisible;

        public NpcInteractionEntry SelectedEntry =>
            _selectedEntryIndex >= 0 && _selectedEntryIndex < _entries.Count ? _entries[_selectedEntryIndex] : null;

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Open(NpcInteractionState state)
        {
            ClearTextTextureCache();
            ResetPacketQuestResultButtonInteractionState();
            ResetPacketQuestResultKeyboardFocus();
            _npcName = string.IsNullOrWhiteSpace(state?.NpcName) ? "NPC" : state.NpcName;
            _presentationStyle = state?.PresentationStyle ?? NpcInteractionPresentationStyle.Default;
            EnsurePacketQuestResultVisualAssetsLoaded();
            EnsurePacketQuestResultSpeakerVisualLoaded(state?.SpeakerTemplateId ?? 0);
            _entries.Clear();

            if (state?.Entries != null)
            {
                for (int i = 0; i < state.Entries.Count; i++)
                {
                    if (state.Entries[i] != null)
                    {
                        _entries.Add(state.Entries[i]);
                    }
                }
            }

            if (_entries.Count == 0)
            {
                _entries.Add(new NpcInteractionEntry
                {
                    EntryId = 0,
                    Kind = NpcInteractionEntryKind.Talk,
                    Title = "Talk",
                    Pages = new[]
                    {
                        new NpcInteractionPage
                        {
                            Text = "The NPC does not have dialogue text in the loaded data."
                        }
                    }
                });
            }

            _selectedEntryIndex = 0;
            if (state != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].EntryId == state.SelectedEntryId)
                    {
                        _selectedEntryIndex = i;
                        break;
                    }
                }
            }

            ResetCurrentPages();
            IsVisible = true;
        }

        public void Close()
        {
            IsVisible = false;
            _inputValue = string.Empty;
            _compositionText = string.Empty;
            ResetPacketQuestResultButtonInteractionState();
            ResetPacketQuestResultKeyboardFocus();
        }

        public bool ContainsPoint(int x, int y, int renderWidth, int renderHeight)
        {
            return IsVisible && GetWindowRectangle(renderWidth, renderHeight).Contains(x, y);
        }

        public NpcInteractionOverlayResult HandleMouse(MouseState mouseState, MouseState previousMouseState, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return default;
            }

            Rectangle windowRect = GetWindowRectangle(renderWidth, renderHeight);
            Point mousePoint = new Point(mouseState.X, mouseState.Y);
            UpdatePacketQuestResultButtonInteractionState(mouseState, previousMouseState, windowRect, mousePoint);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftReleased)
            {
                return default;
            }

            if (!windowRect.Contains(mousePoint))
            {
                if (NpcInteractionPresentationProfile.ShouldCloseWhenClickingOutside(_presentationStyle))
                {
                    Close();
                    return new NpcInteractionOverlayResult(true, null, closeKind: NpcInteractionOverlayCloseKind.Dismissed);
                }

                return new NpcInteractionOverlayResult(true, null);
            }

            if (NpcInteractionPresentationProfile.ShouldDrawCloseButton(_presentationStyle) &&
                GetCloseButtonRectangle(windowRect).Contains(mousePoint))
            {
                Close();
                return new NpcInteractionOverlayResult(true, null, closeKind: NpcInteractionOverlayCloseKind.Dismissed);
            }

            Rectangle entryListRect = GetEntryListRectangle(windowRect);
            if (entryListRect.Contains(mousePoint))
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!GetEntryRectangle(entryListRect, i).Contains(mousePoint))
                    {
                        continue;
                    }

                    _selectedEntryIndex = i;
                    ResetCurrentPages();
                    return new NpcInteractionOverlayResult(true, null);
                }
            }

            Rectangle[] choiceRects = GetChoiceButtonRectangles(windowRect, GetCurrentChoices().Count);
            for (int i = 0; i < choiceRects.Length; i++)
            {
                if (!choiceRects[i].Contains(mousePoint))
                {
                    continue;
                }

                NpcInteractionChoice choice = GetCurrentChoices()[i];
                if (choice.SubmitSelection)
                {
                    Close();
                    return new NpcInteractionOverlayResult(true, null, BuildChoiceSubmission(choice));
                }

                if (choice.Pages.Count == 0)
                {
                    continue;
                }

                _pageContextStack.Push(new PageContext(_currentPages, _currentPage));
                _currentPages = choice.Pages;
                _currentPage = 0;
                return new NpcInteractionOverlayResult(true, null);
            }

            if (GetPrevButtonRectangle(windowRect).Contains(mousePoint))
            {
                FocusPacketQuestResultPrevButton();
                if (_currentPage > 0)
                {
                    _currentPage--;
                }
                else if (_pageContextStack.Count > 0)
                {
                    PageContext context = _pageContextStack.Pop();
                    _currentPages = context.Pages;
                    _currentPage = context.PageIndex;
                }

                return new NpcInteractionOverlayResult(true, null);
            }

            if (GetNextButtonRectangle(windowRect).Contains(mousePoint))
            {
                FocusPacketQuestResultNextButton();
                if (_currentPage < GetCurrentPages().Count - 1)
                {
                    _currentPage++;
                }
                else
                {
                    Close();
                    return new NpcInteractionOverlayResult(
                        true,
                        null,
                        closeKind: _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                            ? NpcInteractionOverlayCloseKind.Completed
                            : NpcInteractionOverlayCloseKind.Dismissed);
                }

                return new NpcInteractionOverlayResult(true, null);
            }

            Rectangle primaryRect = GetPrimaryButtonRectangle(windowRect);
            if (!string.IsNullOrEmpty(GetPrimaryButtonText()) && primaryRect.Contains(mousePoint))
            {
                if (GetCurrentInputRequest() != null)
                {
                    if (TryBuildCurrentInputSubmission(out NpcInteractionInputSubmission submission))
                    {
                        Close();
                        return new NpcInteractionOverlayResult(true, null, submission);
                    }

                    return new NpcInteractionOverlayResult(true, null);
                }

                return new NpcInteractionOverlayResult(true, SelectedEntry?.PrimaryActionEnabled == true ? SelectedEntry : null);
            }

            return new NpcInteractionOverlayResult(true, null);
        }

        public NpcInteractionOverlayResult HandleKeyboard(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (!IsVisible)
            {
                return default;
            }

            if (IsKeyReleased(Keys.Escape, keyboardState, previousKeyboardState))
            {
                Close();
                return new NpcInteractionOverlayResult(true, null, closeKind: NpcInteractionOverlayCloseKind.Dismissed);
            }

            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                NpcInteractionOverlayResult packetQuestResult = HandlePacketQuestResultKeyboard(keyboardState, previousKeyboardState);
                if (packetQuestResult.Consumed)
                {
                    return packetQuestResult;
                }
            }

            NpcInteractionInputRequest inputRequest = GetCurrentInputRequest();
            if (inputRequest == null)
            {
                return default;
            }

            bool shiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            if (IsKeyReleased(Keys.Back, keyboardState, previousKeyboardState) && _inputValue.Length > 0)
            {
                _inputValue = _inputValue[..^1];
                return new NpcInteractionOverlayResult(true, null);
            }

            if (inputRequest.Kind == NpcInteractionInputKind.MultiLineText &&
                shiftPressed &&
                IsKeyReleased(Keys.Enter, keyboardState, previousKeyboardState) &&
                CanAppendCharacter(inputRequest, '\n'))
            {
                _inputValue += '\n';
                return new NpcInteractionOverlayResult(true, null);
            }

            if (IsKeyReleased(Keys.Enter, keyboardState, previousKeyboardState) &&
                TryBuildCurrentInputSubmission(out NpcInteractionInputSubmission submission))
            {
                Close();
                return new NpcInteractionOverlayResult(true, null, submission);
            }

            return inputRequest.Kind != NpcInteractionInputKind.None
                ? new NpcInteractionOverlayResult(true, null)
                : default;
        }

        public void HandleCommittedText(string text)
        {
            NpcInteractionInputRequest inputRequest = GetCurrentInputRequest();
            if (inputRequest == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (char.IsControl(character) || !CanAppendCharacter(inputRequest, character))
                {
                    continue;
                }

                if (inputRequest.Kind == NpcInteractionInputKind.Number &&
                    !char.IsDigit(character) &&
                    !(character == '-' && _inputValue.Length == 0 && inputRequest.MinValue < 0))
                {
                    continue;
                }

                _inputValue += character;
            }
        }

        public void HandleCompositionText(string text)
        {
            _compositionText = GetCurrentInputRequest() == null ? string.Empty : text ?? string.Empty;
        }

        public void ClearCompositionText()
        {
            _compositionText = string.Empty;
        }

        public void Draw(SpriteBatch spriteBatch, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return;
            }

            Rectangle windowRect = GetWindowRectangle(renderWidth, renderHeight);
            if (ShouldUsePacketQuestResultWindowArt())
            {
                DrawPacketQuestResultWindow(spriteBatch, windowRect);
            }
            else
            {
                DrawPanel(spriteBatch, windowRect, ResolveWindowFillColor(), ResolveWindowBorderColor());

                Rectangle titleBar = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 38);
                spriteBatch.Draw(_pixel, titleBar, ResolveTitleFillColor());

                DrawText(spriteBatch, _npcName, new Vector2(windowRect.X + Padding, windowRect.Y + 10), Color.White);

                if (NpcInteractionPresentationProfile.ShouldDrawCloseButton(_presentationStyle))
                {
                    Rectangle closeRect = GetCloseButtonRectangle(windowRect);
                    DrawPanel(spriteBatch, closeRect, new Color(130, 51, 51, 255), new Color(255, 220, 220));
                    DrawCenteredText(spriteBatch, "X", closeRect, Color.White);
                }
            }

            Rectangle entryListRect = GetEntryListRectangle(windowRect);
            if (ShouldDrawEntryList())
            {
                DrawPanel(spriteBatch, entryListRect, new Color(27, 35, 49, 220), new Color(112, 126, 153));
                DrawEntryList(spriteBatch, entryListRect);
            }

            Rectangle textRect = GetTextRectangle(windowRect, entryListRect);
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                DrawPacketQuestResultSpeakerPane(spriteBatch, windowRect, textRect);
            }

            if (NpcInteractionPresentationProfile.ShouldDrawEntryHeader(_presentationStyle))
            {
                DrawEntryHeader(spriteBatch, textRect);
            }

            NpcInteractionInputRequest inputRequest = GetCurrentInputRequest();
            Rectangle bodyRect = new Rectangle(
                textRect.X,
                textRect.Y + (NpcInteractionPresentationProfile.ShouldDrawEntryHeader(_presentationStyle) ? 38 : 0),
                textRect.Width,
                textRect.Height - (NpcInteractionPresentationProfile.ShouldDrawEntryHeader(_presentationStyle) ? 38 : 0) - GetInputPanelHeight(inputRequest));
            DrawWrappedText(spriteBatch, GetCurrentPageText(), bodyRect, ResolveBodyTextColor());
            DrawInputPanel(spriteBatch, bodyRect, inputRequest);
            if (NpcInteractionPresentationProfile.ShouldDrawPageIndicator(_presentationStyle))
            {
                DrawPageIndicator(spriteBatch, windowRect);
            }

            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            Rectangle nextRect = GetNextButtonRectangle(windowRect);
            Rectangle primaryRect = GetPrimaryButtonRectangle(windowRect);

            IReadOnlyList<NpcInteractionChoice> choices = GetCurrentChoices();
            Rectangle[] choiceRects = GetChoiceButtonRectangles(windowRect, choices.Count);
            for (int i = 0; i < choiceRects.Length; i++)
            {
                DrawButton(spriteBatch, choiceRects[i], choices[i].Label, true);
            }

            DrawNavigationButton(
                spriteBatch,
                prevRect,
                GetPrevButtonText(),
                _currentPage > 0 || _pageContextStack.Count > 0,
                ResolvePrevButtonTextures(),
                _isPrevButtonHovered,
                _isPrevButtonPressed,
                IsPacketQuestResultPrevButtonFocused());
            DrawNavigationButton(
                spriteBatch,
                nextRect,
                GetNextButtonText(),
                true,
                ResolveNextButtonTextures(),
                _isNextButtonHovered,
                _isNextButtonPressed,
                IsPacketQuestResultNextButtonFocused());

            string primaryButtonText = GetPrimaryButtonText();
            if (NpcInteractionPresentationProfile.ShouldDrawPrimaryButton(_presentationStyle, primaryButtonText))
            {
                DrawButton(spriteBatch, primaryRect, primaryButtonText, IsPrimaryActionEnabled());
            }
        }

        private void ResetCurrentPages()
        {
            _pageContextStack.Clear();
            _currentPages = SelectedEntry?.Pages ?? Array.Empty<NpcInteractionPage>();
            _currentPage = 0;
            _inputValue = GetCurrentInputRequest()?.DefaultValue ?? string.Empty;
            _compositionText = string.Empty;
            ResetPacketQuestResultKeyboardFocus();
        }

        private IReadOnlyList<NpcInteractionPage> GetCurrentPages()
        {
            return _currentPages;
        }

        private string GetCurrentPageText()
        {
            IReadOnlyList<NpcInteractionPage> pages = GetCurrentPages();
            if (_currentPage < 0 || _currentPage >= pages.Count)
            {
                return string.Empty;
            }

            return pages[_currentPage].Text;
        }

        private IReadOnlyList<NpcInteractionChoice> GetCurrentChoices()
        {
            IReadOnlyList<NpcInteractionPage> pages = GetCurrentPages();
            if (_currentPage < 0 || _currentPage >= pages.Count)
            {
                return Array.Empty<NpcInteractionChoice>();
            }

            return pages[_currentPage].Choices ?? Array.Empty<NpcInteractionChoice>();
        }

        private string GetPrimaryButtonText()
        {
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                return string.Empty;
            }

            return GetCurrentInputRequest() == null
                ? SelectedEntry?.PrimaryActionLabel ?? string.Empty
                : "Send";
        }

        private NpcInteractionInputRequest GetCurrentInputRequest()
        {
            IReadOnlyList<NpcInteractionPage> pages = GetCurrentPages();
            if (_currentPage < 0 || _currentPage >= pages.Count)
            {
                return null;
            }

            return pages[_currentPage].InputRequest;
        }

        private bool IsPrimaryActionEnabled()
        {
            return GetCurrentInputRequest() == null
                ? SelectedEntry?.PrimaryActionEnabled == true
                : TryValidateCurrentInput(out _);
        }

        private bool ShouldDrawEntryList()
        {
            return NpcInteractionPresentationProfile.ShouldDrawEntryList(_presentationStyle, _entries.Count);
        }

        private bool ShouldUsePacketQuestResultWindowArt()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                && _packetQuestResultTopTexture != null
                && _packetQuestResultCenterTexture != null
                && _packetQuestResultBottomTexture != null;
        }

        private void EnsurePacketQuestResultVisualAssetsLoaded()
        {
            if (_packetQuestResultVisualAssetsLoaded)
            {
                return;
            }

            _packetQuestResultVisualAssetsLoaded = true;
            WzImage uiWindow2Image = global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage uiWindow1Image = global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            EnsureParsed(uiWindow2Image);
            EnsureParsed(uiWindow1Image);

            _packetQuestResultTopTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/t") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/t") as WzCanvasProperty);
            _packetQuestResultCenterTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/c") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/c") as WzCanvasProperty);
            _packetQuestResultBottomTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/s") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/s") as WzCanvasProperty);
            _packetQuestResultSeparatorTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/line") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/line") as WzCanvasProperty);
            _packetQuestResultSpeakerBarTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/bar") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/bar") as WzCanvasProperty);
            _packetQuestResultPrevButtonTextures = LoadButtonTextures(uiWindow2Image, uiWindow1Image, "BtPrev");
            _packetQuestResultNextButtonTextures = LoadButtonTextures(uiWindow2Image, uiWindow1Image, "BtNext");
            _packetQuestResultOkButtonTextures = LoadButtonTextures(uiWindow2Image, uiWindow1Image, "BtOK");
        }

        private void EnsurePacketQuestResultSpeakerVisualLoaded(int speakerTemplateId)
        {
            if (_packetQuestResultSpeakerTemplateId == speakerTemplateId)
            {
                return;
            }

            _packetQuestResultSpeakerTemplateId = speakerTemplateId;
            _packetQuestResultSpeakerTexture?.Dispose();
            _packetQuestResultSpeakerTexture = null;

            if (speakerTemplateId <= 0)
            {
                return;
            }

            WzImage npcImage = global::HaCreator.Program.FindImage("Npc", $"{speakerTemplateId:D7}.img");
            EnsureParsed(npcImage);
            _packetQuestResultSpeakerTexture = LoadCanvasTexture(WzInfoTools.GetNpcImage(npcImage));
        }

        private UtilDialogButtonTextures LoadButtonTextures(WzImage primaryImage, WzImage fallbackImage, string buttonPath)
        {
            Texture2D normal = LoadButtonStateTexture(primaryImage, fallbackImage, buttonPath, "normal");
            Texture2D disabled = LoadButtonStateTexture(primaryImage, fallbackImage, buttonPath, "disabled");
            Texture2D mouseOver = LoadButtonStateTexture(primaryImage, fallbackImage, buttonPath, "mouseOver");
            Texture2D pressed = LoadButtonStateTexture(primaryImage, fallbackImage, buttonPath, "pressed");
            Texture2D keyFocused = LoadButtonStateTexture(primaryImage, fallbackImage, buttonPath, "keyFocused");
            return new UtilDialogButtonTextures(normal, disabled, mouseOver, pressed, keyFocused);
        }

        private Texture2D LoadButtonStateTexture(WzImage primaryImage, WzImage fallbackImage, string buttonPath, string statePath)
        {
            return LoadCanvasTexture(ResolveProperty(primaryImage, $"UtilDlgEx/{buttonPath}/{statePath}/0") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(primaryImage, $"UtilDlgEx/{buttonPath}/{statePath}") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(fallbackImage, $"UtilDlgEx/{buttonPath}/{statePath}/0") as WzCanvasProperty)
                ?? LoadCanvasTexture(ResolveProperty(fallbackImage, $"UtilDlgEx/{buttonPath}/{statePath}") as WzCanvasProperty);
        }

        private Color ResolveWindowFillColor()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketScriptUtilDialog
                || _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                ? new Color(35, 31, 28, 236)
                : new Color(18, 25, 39, 235);
        }

        private Color ResolveWindowBorderColor()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketScriptUtilDialog
                || _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                ? new Color(212, 181, 120)
                : new Color(235, 218, 170);
        }

        private Color ResolveTitleFillColor()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketScriptUtilDialog
                || _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                ? new Color(92, 66, 37, 255)
                : new Color(53, 79, 117, 255);
        }

        private Color ResolveBodyTextColor()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                ? new Color(63, 42, 21)
                : new Color(246, 244, 238);
        }

        private Rectangle GetTextRectangle(Rectangle windowRect, Rectangle entryListRect)
        {
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                return PacketQuestResultUtilDialogLayout.GetBodyTextRectangle(
                    windowRect,
                    hasSpeakerPortrait: _packetQuestResultSpeakerTexture != null);
            }

            return new Rectangle(
                ShouldDrawEntryList() ? entryListRect.Right + Padding : windowRect.X + Padding,
                windowRect.Y + 54,
                ShouldDrawEntryList() ? windowRect.Width - EntryListWidth - (Padding * 3) : windowRect.Width - (Padding * 2),
                windowRect.Height - 116);
        }

        private static int GetInputPanelHeight(NpcInteractionInputRequest inputRequest)
        {
            if (inputRequest == null)
            {
                return 0;
            }

            return inputRequest.Kind == NpcInteractionInputKind.MultiLineText ? 76 : 42;
        }

        private void DrawInputPanel(SpriteBatch spriteBatch, Rectangle bodyRect, NpcInteractionInputRequest inputRequest)
        {
            if (inputRequest == null)
            {
                return;
            }

            int height = GetInputPanelHeight(inputRequest) - 10;
            Rectangle inputRect = new(bodyRect.X, bodyRect.Bottom + 8, bodyRect.Width, height);
            DrawPanel(spriteBatch, inputRect, new Color(23, 23, 26, 230), ResolveWindowBorderColor());

            string previewText = string.IsNullOrEmpty(_inputValue) && string.IsNullOrEmpty(_compositionText)
                ? "(type here)"
                : $"{_inputValue}{_compositionText}";
            DrawWrappedText(spriteBatch, previewText, new Rectangle(inputRect.X + 8, inputRect.Y + 6, inputRect.Width - 16, inputRect.Height - 12), new Color(245, 240, 225));
        }

        private bool CanAppendCharacter(NpcInteractionInputRequest inputRequest, char character)
        {
            if (inputRequest == null)
            {
                return false;
            }

            if (_inputValue.Length >= inputRequest.MaxLength)
            {
                return false;
            }

            if (character == '\n' && inputRequest.Kind != NpcInteractionInputKind.MultiLineText)
            {
                return false;
            }

            if (character == '\n')
            {
                int lineCount = 1;
                for (int i = 0; i < _inputValue.Length; i++)
                {
                    if (_inputValue[i] == '\n')
                    {
                        lineCount++;
                    }
                }

                return lineCount < Math.Max(1, inputRequest.LineCount);
            }

            return true;
        }

        private bool TryValidateCurrentInput(out NpcInteractionInputSubmission submission)
        {
            submission = null;
            NpcInteractionInputRequest inputRequest = GetCurrentInputRequest();
            if (inputRequest == null)
            {
                return false;
            }

            string value = _inputValue ?? string.Empty;
            switch (inputRequest.Kind)
            {
                case NpcInteractionInputKind.Text:
                case NpcInteractionInputKind.MultiLineText:
                    if (value.Length < inputRequest.MinLength || value.Length > inputRequest.MaxLength)
                    {
                        return false;
                    }

                    submission = new NpcInteractionInputSubmission
                    {
                        EntryId = SelectedEntry?.EntryId ?? 0,
                        EntryTitle = SelectedEntry?.Title ?? string.Empty,
                        NpcName = _npcName,
                        PresentationStyle = _presentationStyle,
                        Kind = inputRequest.Kind,
                        Value = value
                    };
                    return true;

                case NpcInteractionInputKind.Number:
                    if (!int.TryParse(value, out int numericValue) ||
                        numericValue < inputRequest.MinValue ||
                        numericValue > inputRequest.MaxValue)
                    {
                        return false;
                    }

                    submission = new NpcInteractionInputSubmission
                    {
                        EntryId = SelectedEntry?.EntryId ?? 0,
                        EntryTitle = SelectedEntry?.Title ?? string.Empty,
                        NpcName = _npcName,
                        PresentationStyle = _presentationStyle,
                        Kind = NpcInteractionInputKind.Number,
                        Value = value,
                        NumericValue = numericValue
                    };
                    return true;

                default:
                    return false;
            }
        }

        private bool TryBuildCurrentInputSubmission(out NpcInteractionInputSubmission submission)
        {
            return TryValidateCurrentInput(out submission);
        }

        private NpcInteractionInputSubmission BuildChoiceSubmission(NpcInteractionChoice choice)
        {
            return new NpcInteractionInputSubmission
            {
                EntryId = SelectedEntry?.EntryId ?? 0,
                EntryTitle = SelectedEntry?.Title ?? string.Empty,
                NpcName = _npcName,
                PresentationStyle = _presentationStyle,
                Kind = choice?.SubmissionKind ?? NpcInteractionInputKind.None,
                Value = choice?.SubmissionValue ?? string.Empty,
                NumericValue = choice?.SubmissionNumericValue
            };
        }

        private static bool IsKeyReleased(Keys key, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            return keyboardState.IsKeyUp(key) && previousKeyboardState.IsKeyDown(key);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color fill, Color border)
        {
            spriteBatch.Draw(_pixel, rect, fill);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
        }

        private void DrawPacketQuestResultWindow(SpriteBatch spriteBatch, Rectangle rect)
        {
            if (!ShouldUsePacketQuestResultWindowArt())
            {
                DrawPanel(spriteBatch, rect, ResolveWindowFillColor(), ResolveWindowBorderColor());
                return;
            }

            int topHeight = _packetQuestResultTopTexture.Height;
            int centerHeight = _packetQuestResultCenterTexture.Height;
            int bottomHeight = _packetQuestResultBottomTexture.Height;
            int centerY = rect.Y + topHeight;
            int remainingHeight = Math.Max(0, rect.Height - topHeight - bottomHeight);

            spriteBatch.Draw(_packetQuestResultTopTexture, new Vector2(rect.X, rect.Y), Color.White);
            while (remainingHeight > 0)
            {
                int drawHeight = Math.Min(centerHeight, remainingHeight);
                Rectangle destination = new(rect.X, centerY, rect.Width, drawHeight);
                Rectangle source = new(0, 0, _packetQuestResultCenterTexture.Width, drawHeight);
                spriteBatch.Draw(_packetQuestResultCenterTexture, destination, source, Color.White);
                centerY += drawHeight;
                remainingHeight -= drawHeight;
            }

            spriteBatch.Draw(_packetQuestResultBottomTexture, new Vector2(rect.X, rect.Bottom - bottomHeight), Color.White);
        }

        private void DrawPacketQuestResultSpeakerPane(SpriteBatch spriteBatch, Rectangle windowRect, Rectangle bodyTextRect)
        {
            if (_packetQuestResultSpeakerTexture == null)
            {
                return;
            }

            Rectangle portraitBounds = PacketQuestResultUtilDialogLayout.GetSpeakerPortraitBounds(windowRect, bodyTextRect);
            if (portraitBounds.Width <= 0 || portraitBounds.Height <= 0)
            {
                return;
            }

            Rectangle destination = FitInside(
                portraitBounds,
                _packetQuestResultSpeakerTexture.Width,
                _packetQuestResultSpeakerTexture.Height);
            if (destination.Width > 0 && destination.Height > 0)
            {
                spriteBatch.Draw(_packetQuestResultSpeakerTexture, destination, Color.White);
            }

            if (_packetQuestResultSeparatorTexture != null)
            {
                int separatorX = bodyTextRect.X - _packetQuestResultSeparatorTexture.Width - 6;
                int separatorY = windowRect.Y + 56;
                spriteBatch.Draw(_packetQuestResultSeparatorTexture, new Vector2(separatorX, separatorY), Color.White);
            }

            string speakerName = string.IsNullOrWhiteSpace(_npcName) ? "NPC" : _npcName;
            Rectangle nameBarBounds = PacketQuestResultUtilDialogLayout.GetSpeakerNameBarBounds(
                portraitBounds,
                _packetQuestResultSpeakerBarTexture?.Width ?? 0,
                _packetQuestResultSpeakerBarTexture?.Height ?? 0);
            if (_packetQuestResultSpeakerBarTexture != null && !nameBarBounds.IsEmpty)
            {
                spriteBatch.Draw(_packetQuestResultSpeakerBarTexture, new Vector2(nameBarBounds.X, nameBarBounds.Y), Color.White);
                DrawCenteredText(spriteBatch, speakerName, nameBarBounds, new Color(71, 48, 24));
                return;
            }

            Vector2 speakerPosition = new Vector2(portraitBounds.X + 2, portraitBounds.Bottom - 26);
            DrawText(spriteBatch, speakerName, speakerPosition, new Color(71, 48, 24));
        }

        private static Rectangle FitInside(Rectangle bounds, int sourceWidth, int sourceHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            float scale = Math.Min(bounds.Width / (float)sourceWidth, bounds.Height / (float)sourceHeight);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = bounds.X + ((bounds.Width - width) / 2);
            int y = bounds.Y + ((bounds.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool enabled)
        {
            Color fill = enabled ? new Color(71, 104, 149, 255) : new Color(70, 70, 70, 200);
            Color border = enabled ? new Color(228, 216, 188) : new Color(130, 130, 130);

            DrawPanel(spriteBatch, rect, fill, border);
            DrawCenteredText(spriteBatch, label, rect, Color.White);
        }

        private void DrawNavigationButton(
            SpriteBatch spriteBatch,
            Rectangle rect,
            string label,
            bool enabled,
            UtilDialogButtonTextures textures,
            bool isHovered,
            bool isPressed,
            bool isKeyFocused)
        {
            Texture2D texture = ResolveNavigationButtonTexture(
                textures,
                PacketQuestResultUtilDialogLayout.ResolveButtonVisualState(enabled, isPressed, isHovered, isKeyFocused));
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog && texture != null)
            {
                spriteBatch.Draw(texture, new Vector2(rect.X, rect.Y), Color.White);
                return;
            }

            DrawButton(spriteBatch, rect, label, enabled);
        }

        private static Texture2D ResolveNavigationButtonTexture(
            UtilDialogButtonTextures textures,
            PacketQuestResultUtilDialogButtonVisualState visualState)
        {
            return visualState switch
            {
                PacketQuestResultUtilDialogButtonVisualState.Disabled => textures.Disabled
                    ?? textures.Normal,
                PacketQuestResultUtilDialogButtonVisualState.Pressed => textures.Pressed
                    ?? textures.MouseOver
                    ?? textures.KeyFocused
                    ?? textures.Normal,
                PacketQuestResultUtilDialogButtonVisualState.MouseOver => textures.MouseOver
                    ?? textures.KeyFocused
                    ?? textures.Normal,
                PacketQuestResultUtilDialogButtonVisualState.KeyFocused => textures.KeyFocused
                    ?? textures.MouseOver
                    ?? textures.Normal,
                _ => textures.Normal
            };
        }

        private void DrawEntryList(SpriteBatch spriteBatch, Rectangle entryListRect)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Rectangle itemRect = GetEntryRectangle(entryListRect, i);
                bool isSelected = i == _selectedEntryIndex;

                Color fill = isSelected ? new Color(71, 104, 149, 255) : new Color(37, 49, 69, 210);
                Color border = isSelected ? new Color(235, 218, 170) : new Color(85, 95, 112);
                DrawPanel(spriteBatch, itemRect, fill, border);

                DrawText(spriteBatch, _entries[i].Title, new Vector2(itemRect.X + 10, itemRect.Y + 7), Color.White);
                if (!string.IsNullOrWhiteSpace(_entries[i].Subtitle))
                {
                    DrawText(spriteBatch, _entries[i].Subtitle, new Vector2(itemRect.X + 10, itemRect.Y + 23), new Color(219, 214, 193));
                }
            }
        }

        private void DrawEntryHeader(SpriteBatch spriteBatch, Rectangle textRect)
        {
            NpcInteractionEntry entry = SelectedEntry;
            if (entry == null)
            {
                return;
            }

            DrawText(spriteBatch, entry.Title, new Vector2(textRect.X, textRect.Y), Color.White);
            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                DrawText(spriteBatch, entry.Subtitle, new Vector2(textRect.X, textRect.Y + 18), new Color(224, 202, 145));
            }
        }

        private void DrawPageIndicator(SpriteBatch spriteBatch, Rectangle windowRect)
        {
            string pageText = $"{_currentPage + 1}/{Math.Max(1, GetCurrentPages().Count)}";
            Vector2 size = MeasureText(pageText);
            Vector2 position = new Vector2(windowRect.Right - Padding - size.X, windowRect.Bottom - 62);
            DrawText(spriteBatch, pageText, position, new Color(210, 210, 210));
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, string text, Rectangle bounds, Color color)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string normalizedText = NormalizePunctuation(text);
            string[] paragraphs = normalizedText.Replace("\r", string.Empty).Split('\n');
            float y = bounds.Y;

            for (int i = 0; i < paragraphs.Length; i++)
            {
                foreach (string line in WrapLine(paragraphs[i], bounds.Width))
                {
                    DrawText(spriteBatch, line, new Vector2(bounds.X, y), color);
                    y += GetLineHeight(line);

                    if (y > bounds.Bottom - GetLineHeight(line))
                    {
                        return;
                    }
                }

                y += 4f;
            }
        }

        private IEnumerable<string> WrapLine(string text, float maxWidth)
        {
            if (_font == null)
            {
                yield return text ?? string.Empty;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = NormalizePunctuation(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (MeasureText(candidate).X <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                    currentLine = words[i];
                }
                else
                {
                    yield return words[i];
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, Rectangle rect, Color color)
        {
            Vector2 size = MeasureText(text);
            Vector2 position = new Vector2(
                rect.X + ((rect.Width - size.X) / 2f),
                rect.Y + ((rect.Height - size.Y) / 2f));

            DrawText(spriteBatch, text, position, color);
        }

        private void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string normalizedText = NormalizePunctuation(text);
            if (ContainsUnsupportedFontCharacters(normalizedText))
            {
                DrawFallbackText(spriteBatch, normalizedText, position, color);
                return;
            }

            spriteBatch.DrawString(_font, normalizedText, position, color);
        }

        private Vector2 MeasureText(string text)
        {
            if (_font == null)
            {
                return Vector2.Zero;
            }

            string normalizedText = NormalizePunctuation(text);
            return ContainsUnsupportedFontCharacters(normalizedText)
                ? MeasureFallbackText(normalizedText)
                : _font.MeasureString(normalizedText);
        }

        private string NormalizePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                builder.Append(NormalizeCharacter(text[i]));
            }

            return builder.ToString();
        }

        private bool ContainsUnsupportedFontCharacters(string text)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!FontSupportsCharacter(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool FontSupportsCharacter(char character)
        {
            IReadOnlyList<char> supportedCharacters = _font.Characters;
            if (supportedCharacters == null)
            {
                return true;
            }

            for (int i = 0; i < supportedCharacters.Count; i++)
            {
                if (supportedCharacters[i] == character)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetLineHeight(string text)
        {
            return ContainsUnsupportedFontCharacters(NormalizePunctuation(text))
                ? _fallbackLineHeight
                : _font.LineSpacing;
        }

        private Vector2 MeasureFallbackText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            SD.SizeF size = _measureGraphics.MeasureString(text, _fallbackFont, SD.PointF.Empty, SD.StringFormat.GenericTypographic);
            if (size.Width <= 0f || size.Height <= 0f)
            {
                size = _measureGraphics.MeasureString(text, _fallbackFont);
            }

            return new Vector2((float)Math.Ceiling(size.Width), (float)Math.Ceiling(size.Height));
        }

        private void DrawFallbackText(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Texture2D texture = GetOrCreateFallbackTexture(text, color);
            if (texture == null)
            {
                return;
            }

            spriteBatch.Draw(texture, position, color: XnaColor.White);
        }

        private Texture2D GetOrCreateFallbackTexture(string text, XnaColor color)
        {
            var cacheKey = new TextRenderCacheKey(text, color);
            if (_textTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) && cachedTexture != null && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Vector2 size = MeasureFallbackText(text);
            int width = Math.Max(1, (int)size.X);
            int height = Math.Max(1, (int)size.Y);

            using var bitmap = new SD.Bitmap(width, height);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;
            using var brush = new SD.SolidBrush(SD.Color.FromArgb(color.A, color.R, color.G, color.B));
            graphics.DrawString(text, _fallbackFont, brush, 0f, 0f, SD.StringFormat.GenericTypographic);

            Texture2D texture = bitmap.ToTexture2D(_pixel.GraphicsDevice);
            _textTextureCache[cacheKey] = texture;
            return texture;
        }

        private void ClearTextTextureCache()
        {
            foreach (Texture2D texture in _textTextureCache.Values)
            {
                texture?.Dispose();
            }

            _textTextureCache.Clear();
        }

        private static char NormalizeCharacter(char character)
        {
            return character switch
            {
                '\u2018' => '\'',
                '\u2019' => '\'',
                '\u201A' => '\'',
                '\u201B' => '\'',
                '\u201C' => '"',
                '\u201D' => '"',
                '\u201E' => '"',
                '\u201F' => '"',
                '\u2032' => '\'',
                '\u2033' => '"',
                '\u00B4' => '\'',
                '\u0060' => '\'',
                '\u2013' => '-',
                '\u2014' => '-',
                '\u2212' => '-',
                '\u2026' => '.',
                '\u00A0' => ' ',
                _ => character
            };
        }

        private Rectangle GetWindowRectangle(int renderWidth, int renderHeight)
        {
            int windowWidth = WindowWidth;
            int windowHeight = WindowHeight;
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                windowWidth = _packetQuestResultTopTexture?.Width ?? PacketQuestResultUtilDialogLayout.DefaultWindowWidth;
                windowHeight = ShouldUsePacketQuestResultWindowArt()
                    ? (_packetQuestResultTopTexture.Height
                       + (_packetQuestResultCenterTexture.Height * PacketQuestResultUtilDialogLayout.DefaultCenterRepeatCount)
                       + _packetQuestResultBottomTexture.Height)
                    : PacketQuestResultUtilDialogLayout.DefaultWindowHeight;
            }

            int x = (renderWidth - windowWidth) / 2;
            int y = Math.Max(32, renderHeight - windowHeight - 140);
            return new Rectangle(x, y, windowWidth, windowHeight);
        }

        private static Rectangle GetEntryListRectangle(Rectangle windowRect)
        {
            return new Rectangle(
                windowRect.X + Padding,
                windowRect.Y + 54,
                EntryListWidth,
                windowRect.Height - 116);
        }

        private static Rectangle GetEntryRectangle(Rectangle listRect, int index)
        {
            int itemHeight = 46;
            int itemGap = 6;
            int y = listRect.Y + 8 + index * (itemHeight + itemGap);
            return new Rectangle(listRect.X + 8, y, listRect.Width - 16, itemHeight);
        }

        private static Rectangle GetCloseButtonRectangle(Rectangle windowRect)
        {
            return new Rectangle(windowRect.Right - CloseButtonSize - 10, windowRect.Y + 8, CloseButtonSize, CloseButtonSize);
        }

        private Rectangle GetPrevButtonRectangle(Rectangle windowRect)
        {
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                Texture2D nextTexture = ResolveNextButtonTextures().Normal;
                Texture2D prevTexture = ResolvePrevButtonTextures().Normal;
                int nextWidth = nextTexture?.Width ?? ButtonWidth;
                int nextHeight = nextTexture?.Height ?? ButtonHeight;
                int prevWidth = prevTexture?.Width ?? ButtonWidth;
                int prevHeight = prevTexture?.Height ?? ButtonHeight;
                int buttonY = windowRect.Bottom - Math.Max(nextHeight, prevHeight) - 18;
                int buttonX = windowRect.Right - nextWidth - prevWidth - ButtonGap - 24;
                return new Rectangle(buttonX, buttonY, prevWidth, prevHeight);
            }

            int defaultButtonY = windowRect.Bottom - ButtonHeight - 18;
            int defaultButtonX = windowRect.Right - (ButtonWidth * 2) - ButtonGap - Padding;
            return new Rectangle(defaultButtonX, defaultButtonY, ButtonWidth, ButtonHeight);
        }

        private Rectangle GetNextButtonRectangle(Rectangle windowRect)
        {
            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                Texture2D nextTexture = ResolveNextButtonTextures().Normal;
                int width = nextTexture?.Width ?? ButtonWidth;
                int height = nextTexture?.Height ?? ButtonHeight;
                return new Rectangle(prevRect.Right + ButtonGap, prevRect.Y, width, height);
            }

            return new Rectangle(prevRect.Right + ButtonGap, prevRect.Y, ButtonWidth, ButtonHeight);
        }

        private Rectangle GetPrimaryButtonRectangle(Rectangle windowRect)
        {
            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            return new Rectangle(windowRect.X + Padding, prevRect.Y, ButtonWidth + 12, ButtonHeight);
        }

        private string GetPrevButtonText()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog ? string.Empty : "Prev";
        }

        private string GetNextButtonText()
        {
            if (_presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                return PacketQuestResultUtilDialogLayout.ResolveNextButtonText(_currentPage < GetCurrentPages().Count - 1);
            }

            return _currentPage < GetCurrentPages().Count - 1 ? "Next" : "Close";
        }

        private UtilDialogButtonTextures ResolvePrevButtonTextures()
        {
            return _packetQuestResultPrevButtonTextures;
        }

        private UtilDialogButtonTextures ResolveNextButtonTextures()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                   && _currentPage >= GetCurrentPages().Count - 1
                ? _packetQuestResultOkButtonTextures
                : _packetQuestResultNextButtonTextures;
        }

        private void ResetPacketQuestResultButtonInteractionState()
        {
            _isPrevButtonHovered = false;
            _isNextButtonHovered = false;
            _isPrevButtonPressed = false;
            _isNextButtonPressed = false;
        }

        private void ResetPacketQuestResultKeyboardFocus()
        {
            _isPacketQuestResultPrevButtonFocused = false;
        }

        private void FocusPacketQuestResultPrevButton()
        {
            if (_currentPage > 0 || _pageContextStack.Count > 0)
            {
                _isPacketQuestResultPrevButtonFocused = true;
            }
        }

        private void FocusPacketQuestResultNextButton()
        {
            _isPacketQuestResultPrevButtonFocused = false;
        }

        private bool IsPacketQuestResultPrevButtonFocused()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                && _isPacketQuestResultPrevButtonFocused
                && (_currentPage > 0 || _pageContextStack.Count > 0);
        }

        private bool IsPacketQuestResultNextButtonFocused()
        {
            return _presentationStyle == NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                && !IsPacketQuestResultPrevButtonFocused();
        }

        private NpcInteractionOverlayResult HandlePacketQuestResultKeyboard(
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState)
        {
            if (IsKeyReleased(Keys.Left, keyboardState, previousKeyboardState))
            {
                FocusPacketQuestResultPrevButton();
                return new NpcInteractionOverlayResult(true, null);
            }

            if (IsKeyReleased(Keys.Right, keyboardState, previousKeyboardState) ||
                IsKeyReleased(Keys.Tab, keyboardState, previousKeyboardState))
            {
                FocusPacketQuestResultNextButton();
                return new NpcInteractionOverlayResult(true, null);
            }

            if (!IsKeyReleased(Keys.Enter, keyboardState, previousKeyboardState))
            {
                return default;
            }

            if (IsPacketQuestResultPrevButtonFocused())
            {
                if (_currentPage > 0)
                {
                    _currentPage--;
                }
                else if (_pageContextStack.Count > 0)
                {
                    PageContext context = _pageContextStack.Pop();
                    _currentPages = context.Pages;
                    _currentPage = context.PageIndex;
                }

                return new NpcInteractionOverlayResult(true, null);
            }

            if (_currentPage < GetCurrentPages().Count - 1)
            {
                _currentPage++;
                return new NpcInteractionOverlayResult(true, null);
            }

            Close();
            return new NpcInteractionOverlayResult(
                true,
                null,
                closeKind: NpcInteractionOverlayCloseKind.Completed);
        }

        private void UpdatePacketQuestResultButtonInteractionState(
            MouseState mouseState,
            MouseState previousMouseState,
            Rectangle windowRect,
            Point mousePoint)
        {
            if (_presentationStyle != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog)
            {
                ResetPacketQuestResultButtonInteractionState();
                return;
            }

            _isPrevButtonHovered = GetPrevButtonRectangle(windowRect).Contains(mousePoint);
            _isNextButtonHovered = GetNextButtonRectangle(windowRect).Contains(mousePoint);

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            _isPrevButtonPressed = leftPressed
                && (_isPrevButtonPressed || (_isPrevButtonHovered && leftJustPressed));
            _isNextButtonPressed = leftPressed
                && (_isNextButtonPressed || (_isNextButtonHovered && leftJustPressed));
        }

        private static void EnsureParsed(WzImage image)
        {
            if (image != null && !image.Parsed)
            {
                image.ParseImage();
            }
        }

        private static WzImageProperty ResolveProperty(WzObject root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return root as WzImageProperty;
            }

            WzObject current = root;
            foreach (string segment in propertyPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current switch
                {
                    WzImage image => image[segment],
                    WzImageProperty property => property[segment],
                    _ => null
                };
                if (current == null)
                {
                    break;
                }
            }

            return current as WzImageProperty;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            try
            {
                return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_pixel.GraphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private static Rectangle[] GetChoiceButtonRectangles(Rectangle windowRect, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<Rectangle>();
            }

            const int columns = 3;
            int rows = (int)Math.Ceiling(count / (double)columns);
            int totalHeight = (rows * ButtonHeight) + ((rows - 1) * ButtonGap);
            int y = windowRect.Bottom - ButtonHeight - 30 - totalHeight;

            var rects = new Rectangle[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int itemsInRow = Math.Min(columns, count - (row * columns));
                int totalWidth = (ChoiceButtonWidth * itemsInRow) + (ButtonGap * (itemsInRow - 1));
                int startX = windowRect.X + ((windowRect.Width - totalWidth) / 2);

                rects[i] = new Rectangle(
                    startX + column * (ChoiceButtonWidth + ButtonGap),
                    y + row * (ButtonHeight + ButtonGap),
                    ChoiceButtonWidth,
                    ButtonHeight);
            }

            return rects;
        }
    }
}
