using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class NativeAntiMacroEditHost : IDisposable
    {
        private static readonly string[] ClientFontFamilyCandidates =
        {
            "Arial",
            "DotumChe",
            "Dotum",
            "GulimChe",
            "Gulim",
            "Tahoma",
        };

        private const int ClientControlId = AntiMacroEditControl.ClientControlId;
        private const int FontHeightPixels = 12;
        private const int GwlWndProc = -4;
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int SwpNoZOrder = 0x0004;
        private const int SwpNoActivate = 0x0010;
        private const int WmSetText = 0x000C;
        private const int WmSetFont = 0x0030;
        private const int WmSetFocus = 0x0007;
        private const int WmKillFocus = 0x0008;
        private const int WmGetDlgCode = 0x0087;
        private const int WmChar = 0x0102;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmContextMenu = 0x007B;
        private const int WmCut = 0x0300;
        private const int WmClear = 0x0303;
        private const int WmUndo = 0x0304;
        private const int WmPaste = 0x0302;
        private const int GcsCompStr = 0x0008;
        private const int NiCompositionStr = 0x0015;
        private const int CpsCancel = 0x0004;
        private const int EmGetSel = 0x00B0;
        private const int EmSetSel = 0x00B1;
        private const int EmReplaceSel = 0x00C2;
        private const int EmLimitText = 0x00C5;
        private const int EmCharFromPos = 0x00D7;
        private const int EmSetMargins = 0x00D3;
        private const int EmPosFromChar = 0x00D6;
        private const int EcLeftMargin = 0x0001;
        private const int EcRightMargin = 0x0002;
        private const int VkReturn = 0x0D;
        private const int VkBack = 0x08;
        private const int VkEnd = 0x23;
        private const int VkHome = 0x24;
        private const int VkLeft = 0x25;
        private const int VkUp = 0x26;
        private const int VkRight = 0x27;
        private const int VkDown = 0x28;
        private const int VkInsert = 0x2D;
        private const int VkDelete = 0x2E;
        private const int VkF1 = 0x70;
        private const int VkF12 = 0x7B;
        private const int VkA = 0x41;
        private const int VkC = 0x43;
        private const int VkV = 0x56;
        private const int VkX = 0x58;
        private const int VkY = 0x59;
        private const int VkZ = 0x5A;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmLButtonDblClk = 0x0203;
        private const int WmMouseMove = 0x0200;
        private const uint WsChild = 0x40000000;
        private const uint WsVisible = 0x10000000;
        private const uint WsTabStop = 0x00010000;
        private const uint EsAutoHScroll = 0x0080;
        private const uint EsNoHideSel = 0x0100;
        private const uint ImeExcludeStyle = 0x0080;
        private const int DlgcWantArrows = 0x0001;
        private const int DlgcHasSetSel = 0x0008;
        private const int DlgcWantChars = 0x0080;
        private const int CandidateListCount = 4;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly object HostMapLock = new();
        private static readonly Dictionary<IntPtr, NativeAntiMacroEditHost> HostByHandle = new();

        private readonly int _maxLength;
        private readonly WndProcDelegate _subclassWndProc;
        private readonly HashSet<int> _clientOwnedKeyDowns = new();

        private IntPtr _parentHandle;
        private IntPtr _editHandle;
        private IntPtr _originalWndProc;
        private IntPtr _fontHandle;
        private Rectangle _currentBounds;
        private string _lastKnownText = string.Empty;
        private bool _mouseSelecting;
        private int _mouseSelectionAnchor = -1;

        public NativeAntiMacroEditHost(int maxLength)
        {
            _maxLength = Math.Max(1, maxLength);
            _subclassWndProc = SubclassWndProc;
        }

        public bool IsAttached => _editHandle != IntPtr.Zero && IsWindow(_editHandle);
        public bool HasFocus => IsAttached && GetFocus() == _editHandle;
        public bool IsSelectingWithMouse => _mouseSelecting;
        public string Text => IsAttached ? GetControlText() : string.Empty;

        public event Action<string> TextChanged;
        public event Action SubmitRequested;
        public event Action<bool> FocusChanged;

        public bool TryAttach(IntPtr parentHandle, Rectangle bounds)
        {
            if (parentHandle == IntPtr.Zero || !IsWindow(parentHandle))
            {
                return false;
            }

            if (parentHandle == _parentHandle && IsAttached)
            {
                UpdateBounds(bounds);
                return true;
            }

            Dispose();

            _parentHandle = parentHandle;
            _currentBounds = bounds;
            _editHandle = CreateWindowEx(
                0,
                "EDIT",
                string.Empty,
                WsChild | WsVisible | WsTabStop | EsAutoHScroll | EsNoHideSel,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                _parentHandle,
                new IntPtr(ClientControlId),
                IntPtr.Zero,
                IntPtr.Zero);

            if (_editHandle == IntPtr.Zero)
            {
                _parentHandle = IntPtr.Zero;
                return false;
            }

            lock (HostMapLock)
            {
                HostByHandle[_editHandle] = this;
            }

            _originalWndProc = SetWindowLongPtr(_editHandle, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_subclassWndProc));
            SetWindowTheme(_editHandle, string.Empty, string.Empty);
            ApplyClientFont();
            SetClientMargins();
            SendMessage(_editHandle, EmLimitText, new IntPtr(_maxLength), IntPtr.Zero);
            SendMessage(_editHandle, EmSetSel, IntPtr.Zero, IntPtr.Zero);
            UpdateImePlacement();
            SetVisible(false);
            SynchronizeState();
            return true;
        }

        public void UpdateBounds(Rectangle bounds)
        {
            if (!IsAttached)
            {
                return;
            }

            _currentBounds = bounds;
            SetWindowPos(_editHandle, HwndTop, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);
            UpdateImePlacement();
        }

        public void SetVisible(bool visible)
        {
            if (!IsAttached)
            {
                return;
            }

            ShowWindow(_editHandle, visible ? SwShow : SwHide);
            if (visible)
            {
                SetWindowPos(
                    _editHandle,
                    HwndTop,
                    _currentBounds.X,
                    _currentBounds.Y,
                    _currentBounds.Width,
                    _currentBounds.Height,
                    SwpNoZOrder | SwpNoActivate);
                UpdateImePlacement();
            }
        }

        public void SynchronizeState()
        {
            if (!IsAttached)
            {
                return;
            }

            string currentText = GetControlText();
            if (string.Equals(currentText, _lastKnownText, StringComparison.Ordinal))
            {
                return;
            }

            _lastKnownText = currentText;
            TextChanged?.Invoke(currentText);
        }

        internal static int GetClientOwnedAntiMacroDialogCode()
        {
            return DlgcWantArrows | DlgcHasSetSel | DlgcWantChars;
        }

        public void Reset()
        {
            if (!IsAttached)
            {
                return;
            }

            SetWindowText(_editHandle, string.Empty);
            SendMessage(_editHandle, EmSetSel, IntPtr.Zero, IntPtr.Zero);
            _mouseSelecting = false;
            _mouseSelectionAnchor = -1;
            _clientOwnedKeyDowns.Clear();
            SynchronizeState();
            UpdateImePlacement();
        }

        public void Focus()
        {
            if (!IsAttached)
            {
                return;
            }

            SetFocus(_editHandle);
            int textLength = GetWindowTextLength(_editHandle);
            SendMessage(_editHandle, EmSetSel, new IntPtr(textLength), new IntPtr(textLength));
            UpdateImePlacement();
        }

        public void Blur()
        {
            if (!IsAttached || _parentHandle == IntPtr.Zero)
            {
                return;
            }

            _mouseSelecting = false;
            _mouseSelectionAnchor = -1;
            _clientOwnedKeyDowns.Clear();
            SetFocus(_parentHandle);
        }

        public void BeginSelectionAtPoint(Point pointInParentClientCoordinates)
        {
            if (!IsAttached)
            {
                return;
            }

            int caretIndex = ResolveCaretIndexFromPoint(pointInParentClientCoordinates);
            _mouseSelectionAnchor = caretIndex;
            _mouseSelecting = true;
            SetFocus(_editHandle);
            SendMessage(_editHandle, EmSetSel, new IntPtr(caretIndex), new IntPtr(caretIndex));
            UpdateImePlacement();
        }

        public void UpdateSelectionAtPoint(Point pointInParentClientCoordinates)
        {
            if (!IsAttached || !_mouseSelecting)
            {
                return;
            }

            int caretIndex = ResolveCaretIndexFromPoint(pointInParentClientCoordinates);
            int anchorIndex = Math.Max(0, _mouseSelectionAnchor);
            SendMessage(_editHandle, EmSetSel, new IntPtr(anchorIndex), new IntPtr(caretIndex));
            UpdateImePlacement();
        }

        public void EndMouseSelection()
        {
            _mouseSelecting = false;
        }

        public bool TryInsertCharacter(char character)
        {
            if (!IsAttached || char.IsControl(character))
            {
                return false;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            string currentText = GetControlText();
            if (selectionStart == selectionEnd && currentText.Length >= _maxLength)
            {
                return false;
            }

            ReplaceSelection(character.ToString());
            return true;
        }

        public bool TryReplaceCharacterBeforeCaret(char character)
        {
            if (!IsAttached || char.IsControl(character))
            {
                return false;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionStart != selectionEnd)
            {
                return TryInsertCharacter(character);
            }

            if (selectionStart <= 0)
            {
                return false;
            }

            int replacementStart = ResolveClientBackspaceSelectionStart(GetControlText(), selectionStart);
            if (replacementStart >= selectionStart)
            {
                return false;
            }

            SendMessage(_editHandle, EmSetSel, new IntPtr(replacementStart), new IntPtr(selectionStart));
            ReplaceSelection(character.ToString());
            UpdateImePlacement();
            return true;
        }

        public bool TryBackspace()
        {
            if (!IsAttached)
            {
                return false;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionStart == selectionEnd)
            {
                if (selectionStart <= 0)
                {
                    return false;
                }

                selectionStart = ResolveClientBackspaceSelectionStart(GetControlText(), selectionStart);
            }

            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            ReplaceSelection(string.Empty);
            UpdateImePlacement();
            return true;
        }

        public void Dispose()
        {
            if (_editHandle != IntPtr.Zero)
            {
                lock (HostMapLock)
                {
                    HostByHandle.Remove(_editHandle);
                }

                if (_originalWndProc != IntPtr.Zero)
                {
                    SetWindowLongPtr(_editHandle, GwlWndProc, _originalWndProc);
                    _originalWndProc = IntPtr.Zero;
                }

                DestroyWindow(_editHandle);
                _editHandle = IntPtr.Zero;
            }

            if (_fontHandle != IntPtr.Zero)
            {
                DeleteObject(_fontHandle);
                _fontHandle = IntPtr.Zero;
            }

            _parentHandle = IntPtr.Zero;
            _lastKnownText = string.Empty;
            _clientOwnedKeyDowns.Clear();
        }

        private void ApplyClientFont()
        {
            if (!IsAttached)
            {
                return;
            }

            string requestedFontFamily = MapleStoryStringPool.GetOrFallback(AntiMacroEditControl.ClientFontStringPoolId, "Arial");
            string resolvedFontFamily = ClientTextRasterizer.ResolvePreferredFontFamily(
                requestedFontFamily,
                preferredPrivateFontFamilyCandidates: ClientFontFamilyCandidates,
                preferEmbeddedPrivateFontSources: true);

            if (_fontHandle != IntPtr.Zero)
            {
                DeleteObject(_fontHandle);
                _fontHandle = IntPtr.Zero;
            }

            IntPtr deviceContext = GetDC(_editHandle);
            int pixelsPerInch = 96;
            if (deviceContext != IntPtr.Zero)
            {
                pixelsPerInch = Math.Max(1, GetDeviceCaps(deviceContext, 90));
                ReleaseDC(_editHandle, deviceContext);
            }

            int logicalHeight = -MulDiv(FontHeightPixels, pixelsPerInch, 72);
            _fontHandle = CreateFont(
                logicalHeight,
                0,
                0,
                0,
                400,
                0,
                0,
                0,
                1,
                0,
                0,
                0,
                0,
                resolvedFontFamily);
            if (_fontHandle != IntPtr.Zero)
            {
                SendMessage(_editHandle, WmSetFont, _fontHandle, new IntPtr(1));
            }
        }

        private void SetClientMargins()
        {
            if (!IsAttached)
            {
                return;
            }

            int marginLParam = 0;
            SendMessage(_editHandle, EmSetMargins, new IntPtr(EcLeftMargin | EcRightMargin), new IntPtr(marginLParam));
        }

        private void GetSelection(out int selectionStart, out int selectionEnd)
        {
            int packedSelection = SendMessageInt(_editHandle, EmGetSel, IntPtr.Zero, IntPtr.Zero);
            selectionStart = packedSelection & 0xFFFF;
            selectionEnd = (packedSelection >> 16) & 0xFFFF;
        }

        private void ReplaceSelection(string replacementText)
        {
            SendMessageString(_editHandle, EmReplaceSel, new IntPtr(1), replacementText ?? string.Empty);
            SynchronizeState();
        }

        private string GetControlText()
        {
            if (!IsAttached)
            {
                return string.Empty;
            }

            int textLength = GetWindowTextLength(_editHandle);
            StringBuilder builder = new(textLength + 1);
            GetWindowText(_editHandle, builder, builder.Capacity);
            return builder.ToString();
        }

        private void UpdateImePlacement()
        {
            if (!IsAttached || !HasFocus)
            {
                return;
            }

            IntPtr inputContext = ImmGetContext(_editHandle);
            if (inputContext == IntPtr.Zero)
            {
                return;
            }

            try
            {
                ResolveImePlacement(out POINT compositionPoint, out RECT compositionArea, out POINT candidatePoint, out RECT candidateArea);
                COMPOSITIONFORM compositionForm = new()
                {
                    dwStyle = ImeExcludeStyle,
                    ptCurrentPos = compositionPoint,
                    rcArea = compositionArea
                };

                ImmSetCompositionWindow(inputContext, ref compositionForm);
                for (uint index = 0; index < CandidateListCount; index++)
                {
                    CANDIDATEFORM candidateForm = new()
                    {
                        dwIndex = index,
                        dwStyle = ImeExcludeStyle,
                        ptCurrentPos = candidatePoint,
                        rcArea = candidateArea
                    };

                    ImmSetCandidateWindow(inputContext, ref candidateForm);
                }
            }
            finally
            {
                ImmReleaseContext(_editHandle, inputContext);
            }
        }

        private void ResolveImePlacement(out POINT compositionPoint, out RECT compositionArea, out POINT candidatePoint, out RECT candidateArea)
        {
            int width = Math.Max(1, _currentBounds.Width);
            int height = Math.Max(1, _currentBounds.Height);
            Point caretPoint = ResolveCaretClientPoint(width, height);
            int compositionX = Math.Clamp(caretPoint.X, 0, width - 1);
            int compositionY = Math.Clamp(caretPoint.Y, 0, height - 1);
            int candidateY = Math.Max(compositionY + 1, height);

            compositionPoint = new POINT
            {
                x = compositionX,
                y = compositionY
            };
            compositionArea = new RECT
            {
                left = compositionX,
                top = 0,
                right = width,
                bottom = height
            };
            candidatePoint = new POINT
            {
                x = compositionX,
                y = candidateY
            };
            candidateArea = new RECT
            {
                left = compositionX,
                top = 0,
                right = width,
                bottom = height
            };
        }

        private Point ResolveCaretClientPoint(int width, int height)
        {
            int caretIndex = GetCaretIndex();
            IntPtr packedPosition = SendMessage(_editHandle, EmPosFromChar, IntPtr.Zero, new IntPtr(caretIndex));
            int packed = packedPosition.ToInt32();
            int x = (short)(packed & 0xFFFF);
            int y = (short)((packed >> 16) & 0xFFFF);
            return ResolveClientOwnedCaretPoint(x, y, width, height);
        }

        internal static Point ResolveClientOwnedCaretPoint(int rawX, int rawY, int width, int height)
        {
            int resolvedWidth = Math.Max(1, width);
            int resolvedHeight = Math.Max(1, height);
            int fallbackBaselineY = Math.Clamp(
                AntiMacroEditControl.ClientTextOrigin.Y + AntiMacroEditControl.ClientFontHeightPixels - 1,
                0,
                resolvedHeight - 1);
            if (rawX < 0 || rawY < 0)
            {
                return new Point(0, fallbackBaselineY);
            }

            int baselineY = rawY + AntiMacroEditControl.ClientTextOrigin.Y + AntiMacroEditControl.ClientFontHeightPixels - 1;
            return new Point(
                Math.Clamp(rawX + AntiMacroEditControl.ClientCaretOrigin.X, 0, resolvedWidth - 1),
                Math.Clamp(baselineY + AntiMacroEditControl.ClientCaretOrigin.Y, 0, resolvedHeight - 1));
        }

        private int GetCaretIndex()
        {
            GetSelection(out _, out int selectionEnd);
            return Math.Max(0, selectionEnd);
        }

        private int ResolveCaretIndexFromPoint(Point pointInParentClientCoordinates)
        {
            if (!IsAttached)
            {
                return 0;
            }

            int localX = Math.Clamp(pointInParentClientCoordinates.X - _currentBounds.X, 0, Math.Max(0, _currentBounds.Width - 1));
            int localY = Math.Clamp(pointInParentClientCoordinates.Y - _currentBounds.Y, 0, Math.Max(0, _currentBounds.Height - 1));
            int packedPoint = (localY << 16) | (localX & 0xFFFF);
            int caretIndex = SendMessageInt(_editHandle, EmCharFromPos, IntPtr.Zero, new IntPtr(packedPoint));
            if (caretIndex < 0)
            {
                return GetWindowTextLength(_editHandle);
            }

            return Math.Clamp(caretIndex, 0, GetWindowTextLength(_editHandle));
        }

        private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            int virtualKey = wParam.ToInt32();
            if (msg == WmGetDlgCode)
            {
                IntPtr originalResult = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                return new IntPtr(originalResult.ToInt32() | GetClientOwnedAntiMacroDialogCode());
            }

            int clientLParam = lParam.ToInt32();
            (bool controlHeld, bool shiftHeld) = ResolveClientOwnedModifierState(
                clientLParam,
                IsControlKeyDown(),
                IsShiftKeyDown());
            bool allowImeOwnedDownHandling = ShouldDeferDownKeyToIme(
                virtualKey,
                controlHeld,
                shiftHeld,
                HasImeOwnedInputStateForDownKey());
            if (msg == WmKeyDown && !allowImeOwnedDownHandling && HandleClientOwnedKeyDown(virtualKey, controlHeld, shiftHeld, wParam, lParam))
            {
                _clientOwnedKeyDowns.Add(virtualKey);
                return IntPtr.Zero;
            }

            if (msg == WmChar && virtualKey == VkReturn)
            {
                return IntPtr.Zero;
            }

            if (msg == WmLButtonDblClk)
            {
                ApplyClientWordSelectionFromDoubleClick(lParam);
                return IntPtr.Zero;
            }

            if (ShouldHandleClientOwnedMouseMessage(msg))
            {
                HandleClientOwnedMouseMessage(msg, lParam);
                return IntPtr.Zero;
            }

            if (ShouldSuppressClientUnsupportedEditKey(msg, virtualKey, controlHeld, shiftHeld))
            {
                if (msg == WmKeyDown)
                {
                    ForwardKeyToParent(WmKeyDown, wParam, lParam);
                }

                return IntPtr.Zero;
            }

            if (msg == WmUndo || msg == WmContextMenu)
            {
                return IntPtr.Zero;
            }

            if (ShouldHandleClientOwnedEditCommand(msg))
            {
                HandleClientOwnedEditCommand(msg);
                return IntPtr.Zero;
            }

            if ((msg == WmKeyDown || msg == WmKeyUp) && IsStagePassthroughVirtualKey(virtualKey))
            {
                ForwardKeyToParent(msg, wParam, lParam);
                UpdateImePlacement();
                return IntPtr.Zero;
            }

            if (msg == WmKeyUp)
            {
                bool wasClientOwnedKeyDown = _clientOwnedKeyDowns.Remove(virtualKey);
                if (ShouldForwardClientOwnedKeyUpToParent(virtualKey, wasClientOwnedKeyDown))
                {
                    ForwardKeyToParent(WmKeyUp, wParam, lParam);
                }

                UpdateImePlacement();
                return IntPtr.Zero;
            }

            if (msg == WmKeyDown)
            {
                if (allowImeOwnedDownHandling)
                {
                    bool imeOwnedDispatchApplied = TryDispatchDeferredDownKeyToImeOwner(hWnd, wParam, lParam);
                    if (!imeOwnedDispatchApplied)
                    {
                        _ = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                    }

                    // Let IME consume the Down key first; only fall through to the parent
                    // path when IME is no longer holding an active composition/candidate state.
                    bool imeStillOwnsInputAfterKeyDown = HasImeOwnedInputState();
                    if (ShouldForwardDeferredDownKeyToParentAfterIme(imeStillOwnsInputAfterKeyDown))
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    }
                }
                else
                {
                    // `CCtrlEdit::OnKey` falls through to the parent owner for every
                    // unhandled key-down after the edit consumes its own branch.
                    ForwardKeyToParent(WmKeyDown, wParam, lParam);
                }

                UpdateImePlacement();
                return IntPtr.Zero;
            }

            IntPtr result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);

            if (msg == WmSetFocus)
            {
                if (ShouldCancelImeCompositionOnFocusChange(hasFocus: true))
                {
                    CancelImeComposition();
                }

                if (ShouldDisableImeOpenStatusOnFocusChange(hasFocus: true))
                {
                    SetImeOpenStatus(open: false);
                }

                UpdateImePlacement();
                FocusChanged?.Invoke(true);
            }
            else if (msg == WmKillFocus)
            {
                _clientOwnedKeyDowns.Clear();
                if (ShouldCancelImeCompositionOnFocusChange(hasFocus: false))
                {
                    CancelImeComposition();
                }

                if (ShouldDisableImeOpenStatusOnFocusChange(hasFocus: false))
                {
                    SetImeOpenStatus(open: false);
                }

                FocusChanged?.Invoke(false);
            }

            if (msg == WmKeyDown
                || msg == WmKeyUp
                || msg == WmChar
                || msg == WmLButtonDown
                || msg == WmLButtonDblClk
                || msg == WmLButtonUp
                || msg == WmMouseMove)
            {
                UpdateImePlacement();
            }

            if (msg == WmSetText || msg == WmPaste || msg == WmCut || msg == WmClear || msg == WmUndo || msg == WmChar)
            {
                SynchronizeState();
            }

            return result;
        }

        private bool HandleClientOwnedKeyDown(int virtualKey, bool controlHeld, bool shiftHeld, IntPtr wParam, IntPtr lParam)
        {
            switch (virtualKey)
            {
                case VkReturn:
                    SubmitRequested?.Invoke();
                    if (ShouldForwardClientOwnedKeyDownToParent(virtualKey))
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    }

                    return true;
                case VkBack:
                    TryBackspace();
                    return true;
                case VkDelete:
                    if (shiftHeld)
                    {
                        CutSelectionToClipboard();
                    }
                    else
                    {
                        TryDeleteForward();
                    }

                    return true;
                case VkInsert:
                    if (shiftHeld)
                    {
                        PasteClipboardText();
                        return true;
                    }

                    ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    return true;
                case VkC:
                    if (!controlHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    CopySelectionToClipboard();
                    return true;
                case VkV:
                    if (!controlHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    PasteClipboardText();
                    return true;
                case VkX:
                    if (!controlHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    CutSelectionToClipboard();
                    return true;
                case VkLeft:
                    MoveCaretHorizontally(moveRight: false);
                    if (ShouldForwardClientOwnedKeyDownToParent(virtualKey))
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    }

                    return true;
                case VkRight:
                    MoveCaretHorizontally(moveRight: true);
                    if (ShouldForwardClientOwnedKeyDownToParent(virtualKey))
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    }

                    return true;
                case VkHome:
                    if (controlHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    MoveCaretToBoundary(moveToEnd: false);
                    return true;
                case VkEnd:
                    if (controlHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    MoveCaretToBoundary(moveToEnd: true);
                    return true;
                case VkUp:
                case VkDown:
                    if (controlHeld || shiftHeld)
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                        return true;
                    }

                    if (ShouldForwardClientOwnedKeyDownToParent(virtualKey))
                    {
                        ForwardKeyToParent(WmKeyDown, wParam, lParam);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private void HandleClientOwnedEditCommand(uint msg)
        {
            switch (msg)
            {
                case WmPaste:
                    PasteClipboardText();
                    break;
                case WmCut:
                    CutSelectionToClipboard();
                    break;
                case WmClear:
                    ClearSelectionText();
                    break;
            }
        }

        private void ForwardKeyToParent(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (_parentHandle != IntPtr.Zero && IsWindow(_parentHandle))
            {
                SendMessage(_parentHandle, (int)msg, wParam, lParam);
            }
        }

        private static bool IsStagePassthroughVirtualKey(int virtualKey)
        {
            return virtualKey >= VkF1 && virtualKey <= VkF12;
        }

        internal static bool ShouldForwardClientOwnedKeyUpToParent(int virtualKey)
        {
            return ShouldForwardClientOwnedKeyUpToParent(virtualKey, wasClientOwnedKeyDown: false);
        }

        internal static bool ShouldForwardClientOwnedKeyUpToParent(int virtualKey, bool wasClientOwnedKeyDown)
        {
            // `CCtrlEdit::OnKey` jumps straight to the parent path on every key-up.
            return true;
        }

        internal static bool ShouldForwardClientOwnedKeyDownToParent(int virtualKey)
        {
            // `CCtrlEdit::OnKey` falls through to the parent owner after handling
            // Enter and the arrow-navigation branch itself.
            return virtualKey is VkReturn or VkLeft or VkRight or VkUp or VkDown;
        }

        internal static bool ShouldDeferDownKeyToIme(int virtualKey, bool controlHeld, bool shiftHeld, bool imeCompositionActive)
        {
            return ShouldDeferDownKeyToIme(virtualKey, controlHeld, shiftHeld, imeCompositionActive, imeCandidateWindowActive: false);
        }

        internal static bool ShouldDeferDownKeyToIme(int virtualKey, bool controlHeld, bool shiftHeld, bool imeCompositionActive, bool imeCandidateWindowActive)
        {
            // `CCtrlEdit::OnKey` routes VK_DOWN to `m_pIMECandWnd` without a
            // Ctrl/Shift guard, so keep IME-owner deferral active even when
            // modifier bits are present in the encoded lParam path.
            return virtualKey == VkDown && (imeCompositionActive || imeCandidateWindowActive);
        }

        internal static bool ShouldDeferDownKeyToIme(bool controlHeld, bool shiftHeld, bool imeCompositionActive)
        {
            return ShouldDeferDownKeyToIme(VkDown, controlHeld, shiftHeld, imeCompositionActive);
        }

        internal static bool ShouldDeferDownKeyToIme(bool controlHeld, bool shiftHeld, bool imeCompositionActive, bool imeCandidateWindowActive)
        {
            return ShouldDeferDownKeyToIme(VkDown, controlHeld, shiftHeld, imeCompositionActive, imeCandidateWindowActive);
        }

        internal static bool ShouldCancelImeCompositionOnFocusChange(bool hasFocus)
        {
            // Keep composition cleanup on both focus transitions so the hosted
            // seam mirrors the recovered blur cleanup (`OnSetFocus(false)`) while
            // preserving the existing focus-gain reset behavior.
            return true;
        }

        internal static bool ShouldForwardDeferredDownKeyToParentAfterIme(bool imeOwnedInputStateAfterKeyDown)
        {
            // `CCtrlEdit::OnKey` still falls through to the parent callback after the
            // IME candidate-owner branch handles VK_DOWN.
            return true;
        }

        internal static bool ShouldDisableImeOpenStatusOnFocusChange(bool hasFocus)
        {
            // `CCtrlEdit::OnSetFocus(true)` also calls `CWndMan::EnableIME(..., 0)`,
            // so mirror that by explicitly disabling IME open status on focus gain.
            return hasFocus;
        }

        internal static bool IsClientEncodedKeyUp(int clientLParam)
        {
            return clientLParam < 0;
        }

        internal static bool IsClientEncodedOnKeyLParam(int clientLParam)
        {
            // `CCtrlEdit::OnKey` consumes only sign/ctrl/shift bits from the
            // encoded lParam path. Win32 WM_KEY lParam carries scan/repeat
            // fields, so keep those on the physical-keyboard fallback path.
            const uint clientOnKeyMask = 0x80000011U;
            return (((uint)clientLParam) & ~clientOnKeyMask) == 0;
        }

        internal static bool IsClientEncodedControlHeld(int clientLParam)
        {
            return (((uint)clientLParam >> 4) & 1U) != 0;
        }

        internal static bool IsClientEncodedShiftHeld(int clientLParam)
        {
            return (clientLParam & 1) != 0;
        }

        internal static (bool ControlHeld, bool ShiftHeld) ResolveClientOwnedModifierState(
            int clientLParam,
            bool controlKeyDown,
            bool shiftKeyDown)
        {
            if (IsClientEncodedOnKeyLParam(clientLParam))
            {
                return (IsClientEncodedControlHeld(clientLParam), IsClientEncodedShiftHeld(clientLParam));
            }

            return (controlKeyDown, shiftKeyDown);
        }

        internal static bool ShouldSuppressClientUnsupportedEditKey(uint msg, int virtualKey, bool controlHeld, bool shiftHeld)
        {
            if (msg != WmKeyDown)
            {
                return false;
            }

            if (controlHeld)
            {
                if (virtualKey is VkC or VkV or VkX)
                {
                    return false;
                }

                // `CCtrlEdit::OnKey` does not expose Win32 EDIT affordances such
                // as select-all, undo/redo, Ctrl+Insert, or Ctrl+Home/End.
                return virtualKey is VkA
                    or VkY
                    or VkZ
                    or VkInsert
                    or VkHome
                    or VkEnd;
            }

            if (shiftHeld)
            {
                return false;
            }

            return false;
        }

        internal static bool ShouldHandleClientOwnedEditCommand(uint msg)
        {
            return msg is WmPaste or WmCut or WmClear;
        }

        internal static bool ShouldHandleClientOwnedMouseMessage(uint msg)
        {
            // `CCtrlEdit::OnMouseButton` handles only left-down and double-click,
            // while `OnMouseMove` owns drag selection. Left-up just ends capture.
            return msg is WmLButtonDown or WmMouseMove or WmLButtonUp;
        }

        private static bool IsControlKeyDown()
        {
            return (GetKeyState(VkControl) & 0x8000) != 0;
        }

        private static bool IsShiftKeyDown()
        {
            return (GetKeyState(VkShift) & 0x8000) != 0;
        }

        private bool HasImeOwnedInputState()
        {
            if (!IsAttached || !HasFocus)
            {
                return false;
            }

            IntPtr inputContext = ImmGetContext(_editHandle);
            if (inputContext == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (!ImmGetOpenStatus(inputContext))
                {
                    return false;
                }

                return ImmGetCompositionString(inputContext, GcsCompStr, IntPtr.Zero, 0) > 0
                    || HasActiveImeCandidateWindow(inputContext);
            }
            finally
            {
                ImmReleaseContext(_editHandle, inputContext);
            }
        }

        private bool HasImeOwnedInputStateForDownKey()
        {
            if (!IsAttached || !HasFocus)
            {
                return false;
            }

            IntPtr inputContext = ImmGetContext(_editHandle);
            if (inputContext == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                bool imeOpen = ImmGetOpenStatus(inputContext);
                bool compositionActive = ImmGetCompositionString(inputContext, GcsCompStr, IntPtr.Zero, 0) > 0;
                bool candidateWindowActive = HasActiveImeCandidateWindow(inputContext);
                bool defaultImeWindowAvailable = HasImeDefaultWindow(_editHandle);
                return IsImeOwnedDownKeyPath(
                    imeOpen,
                    compositionActive,
                    candidateWindowActive,
                    defaultImeWindowAvailable);
            }
            finally
            {
                ImmReleaseContext(_editHandle, inputContext);
            }
        }

        private bool TryDispatchDeferredDownKeyToImeOwner(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
        {
            IntPtr targetWindow = hWnd != IntPtr.Zero ? hWnd : _editHandle;
            IntPtr defaultImeWindow = targetWindow == IntPtr.Zero ? IntPtr.Zero : ImmGetDefaultIMEWnd(targetWindow);
            if (!ShouldDispatchDeferredDownKeyToImeWindow(defaultImeWindow, targetWindow))
            {
                return false;
            }

            SendMessage(defaultImeWindow, WmKeyDown, wParam, lParam);
            return true;
        }

        internal static bool IsImeOwnedDownKeyPath(
            bool imeOpenStatus,
            bool imeCompositionActive,
            bool imeCandidateWindowActive,
            bool imeDefaultWindowAvailable)
        {
            // `CCtrlEdit::OnKey` checks owner presence (`m_pIMECandWnd`) for VK_DOWN.
            // In the hosted seam, treat an open IME with a resolvable default IME owner
            // window as equivalent ownership even when composition bytes are currently empty.
            return imeOpenStatus && (imeCompositionActive || imeCandidateWindowActive || imeDefaultWindowAvailable);
        }

        internal static bool ShouldDispatchDeferredDownKeyToImeWindow(IntPtr imeWindowHandle, IntPtr editHandle)
        {
            // Keep dispatch constrained to a valid, distinct IME owner window.
            return imeWindowHandle != IntPtr.Zero && imeWindowHandle != editHandle;
        }

        private static bool HasImeDefaultWindow(IntPtr editHandle)
        {
            return editHandle != IntPtr.Zero && ImmGetDefaultIMEWnd(editHandle) != IntPtr.Zero;
        }

        private static bool HasActiveImeCandidateWindow(IntPtr inputContext)
        {
            if (inputContext == IntPtr.Zero)
            {
                return false;
            }

            return ImmGetCandidateListCount(inputContext, out int candidateListCount) > 0
                || candidateListCount > 0;
        }

        private void CancelImeComposition()
        {
            if (!IsAttached)
            {
                return;
            }

            IntPtr inputContext = ImmGetContext(_editHandle);
            if (inputContext == IntPtr.Zero)
            {
                return;
            }

            try
            {
                ImmNotifyIME(inputContext, NiCompositionStr, CpsCancel, 0);
            }
            finally
            {
                ImmReleaseContext(_editHandle, inputContext);
            }
        }

        private void SetImeOpenStatus(bool open)
        {
            if (!IsAttached)
            {
                return;
            }

            IntPtr inputContext = ImmGetContext(_editHandle);
            if (inputContext == IntPtr.Zero)
            {
                return;
            }

            try
            {
                ImmSetOpenStatus(inputContext, open);
            }
            finally
            {
                ImmReleaseContext(_editHandle, inputContext);
            }
        }

        private void ApplyClientWordSelectionFromDoubleClick(IntPtr lParam)
        {
            if (!IsAttached)
            {
                return;
            }

            int packed = lParam.ToInt32();
            int localX = (short)(packed & 0xFFFF);
            int localY = (short)((packed >> 16) & 0xFFFF);
            int caretIndex = ResolveCaretIndexFromClientPoint(localX, localY);
            AntiMacroEditControl.ResolveClientWordSelectionRange(GetControlText(), caretIndex, out int selectionStart, out int selectionEnd);

            _mouseSelecting = false;
            _mouseSelectionAnchor = selectionStart;
            SetFocus(_editHandle);
            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            UpdateImePlacement();
            SynchronizeState();
        }

        private void HandleClientOwnedMouseMessage(uint msg, IntPtr lParam)
        {
            if (!IsAttached)
            {
                return;
            }

            DecodeClientMousePoint(lParam, out int localX, out int localY);
            switch (msg)
            {
                case WmLButtonDown:
                    BeginSelectionAtClientPoint(localX, localY);
                    SetCapture(_editHandle);
                    break;
                case WmMouseMove:
                    UpdateSelectionAtClientPoint(localX, localY);
                    break;
                case WmLButtonUp:
                    _mouseSelecting = false;
                    ReleaseCapture();
                    break;
            }

            UpdateImePlacement();
            SynchronizeState();
        }

        private void BeginSelectionAtClientPoint(int localX, int localY)
        {
            int caretIndex = ResolveCaretIndexFromClientPoint(localX, localY);
            _mouseSelectionAnchor = caretIndex;
            _mouseSelecting = true;
            SetFocus(_editHandle);
            SendMessage(_editHandle, EmSetSel, new IntPtr(caretIndex), new IntPtr(caretIndex));
        }

        private void UpdateSelectionAtClientPoint(int localX, int localY)
        {
            if (!_mouseSelecting)
            {
                return;
            }

            int caretIndex = ResolveCaretIndexFromClientPoint(localX, localY);
            int anchorIndex = Math.Max(0, _mouseSelectionAnchor);
            SendMessage(_editHandle, EmSetSel, new IntPtr(anchorIndex), new IntPtr(caretIndex));
        }

        private static void DecodeClientMousePoint(IntPtr lParam, out int localX, out int localY)
        {
            int packed = lParam.ToInt32();
            localX = (short)(packed & 0xFFFF);
            localY = (short)((packed >> 16) & 0xFFFF);
        }

        private int ResolveCaretIndexFromClientPoint(int localX, int localY)
        {
            if (!IsAttached)
            {
                return 0;
            }

            int clampedLocalX = Math.Clamp(localX, 0, Math.Max(0, _currentBounds.Width - 1));
            int clampedLocalY = Math.Clamp(localY, 0, Math.Max(0, _currentBounds.Height - 1));
            int packedPoint = (clampedLocalY << 16) | (clampedLocalX & 0xFFFF);
            int caretIndex = SendMessageInt(_editHandle, EmCharFromPos, IntPtr.Zero, new IntPtr(packedPoint));
            if (caretIndex < 0)
            {
                return GetWindowTextLength(_editHandle);
            }

            return Math.Clamp(caretIndex, 0, GetWindowTextLength(_editHandle));
        }

        private void MoveCaretHorizontally(bool moveRight)
        {
            if (!IsAttached)
            {
                return;
            }

            string currentText = GetControlText();
            GetSelection(out int selectionStart, out int selectionEnd);
            int resolvedCaret = ResolveClientOwnedNavigationCaret(currentText, selectionStart, selectionEnd, moveRight);
            SendMessage(_editHandle, EmSetSel, new IntPtr(resolvedCaret), new IntPtr(resolvedCaret));
            UpdateImePlacement();
        }

        private void MoveCaretToBoundary(bool moveToEnd)
        {
            if (!IsAttached)
            {
                return;
            }

            int target = moveToEnd ? GetWindowTextLength(_editHandle) : 0;
            SendMessage(_editHandle, EmSetSel, new IntPtr(target), new IntPtr(target));
            UpdateImePlacement();
        }

        private bool TryDeleteForward()
        {
            if (!IsAttached)
            {
                return false;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionStart == selectionEnd)
            {
                string currentText = GetControlText();
                if (selectionStart >= currentText.Length)
                {
                    return false;
                }

                selectionEnd = ResolveClientDeleteSelectionEnd(currentText, selectionStart);
            }

            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            ReplaceSelection(string.Empty);
            UpdateImePlacement();
            return true;
        }

        private void CopySelectionToClipboard()
        {
            if (!IsAttached)
            {
                return;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionEnd <= selectionStart)
            {
                return;
            }

            string currentText = GetControlText();
            if (selectionStart < 0 || selectionEnd > currentText.Length)
            {
                return;
            }

            try
            {
                System.Windows.Forms.Clipboard.SetText(currentText.Substring(selectionStart, selectionEnd - selectionStart));
            }
            catch
            {
            }
        }

        private void CutSelectionToClipboard()
        {
            if (!IsAttached)
            {
                return;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionEnd <= selectionStart)
            {
                return;
            }

            CopySelectionToClipboard();
            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            ReplaceSelection(string.Empty);
            UpdateImePlacement();
        }

        private void ClearSelectionText()
        {
            if (!IsAttached)
            {
                return;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            if (selectionEnd <= selectionStart)
            {
                return;
            }

            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            ReplaceSelection(string.Empty);
            UpdateImePlacement();
        }

        private void PasteClipboardText()
        {
            if (!IsAttached)
            {
                return;
            }

            string clipboardText;
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                clipboardText = System.Windows.Forms.Clipboard.GetText();
            }
            catch
            {
                return;
            }

            if (string.IsNullOrEmpty(clipboardText))
            {
                return;
            }

            string sanitized = RemoveControlCharacters(clipboardText);
            if (sanitized.Length == 0)
            {
                return;
            }

            GetSelection(out int selectionStart, out int selectionEnd);
            int selectedLength = Math.Max(0, selectionEnd - selectionStart);
            int currentLength = GetWindowTextLength(_editHandle);
            int availableLength = Math.Max(0, _maxLength - (currentLength - selectedLength));
            if (availableLength <= 0)
            {
                return;
            }

            string limitedText = TrimToMaxTextElements(sanitized, availableLength);
            if (limitedText.Length == 0)
            {
                return;
            }

            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart), new IntPtr(selectionEnd));
            ReplaceSelection(limitedText);
            UpdateImePlacement();
        }

        internal static int ResolveClientOwnedNavigationCaret(string text, int selectionStart, int selectionEnd, bool moveRight)
        {
            string resolvedText = text ?? string.Empty;
            int resolvedSelectionStart = Math.Clamp(selectionStart, 0, resolvedText.Length);
            int resolvedSelectionEnd = Math.Clamp(selectionEnd, 0, resolvedText.Length);
            int currentCaret = Math.Max(resolvedSelectionStart, resolvedSelectionEnd);
            int anchor = Math.Min(resolvedSelectionStart, resolvedSelectionEnd);
            return AntiMacroEditControl.ResolveArrowCaretIndex(
                resolvedText,
                currentCaret,
                anchor,
                moveRight,
                shiftHeld: false);
        }

        internal static int ResolveClientBackspaceSelectionStart(string text, int caretIndex)
        {
            string resolvedText = text ?? string.Empty;
            int resolvedCaret = Math.Clamp(caretIndex, 0, resolvedText.Length);
            return ResolvePreviousTextElementBoundary(resolvedText, resolvedCaret);
        }

        internal static int ResolveClientDeleteSelectionEnd(string text, int caretIndex)
        {
            string resolvedText = text ?? string.Empty;
            int resolvedCaret = Math.Clamp(caretIndex, 0, resolvedText.Length);
            return ResolveNextTextElementBoundary(resolvedText, resolvedCaret);
        }

        internal static string RemoveControlCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);
            foreach (char character in text)
            {
                if (!char.IsControl(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        internal static string TrimToMaxTextElements(string text, int maxTextElements)
        {
            if (string.IsNullOrEmpty(text) || maxTextElements <= 0)
            {
                return string.Empty;
            }

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
            StringBuilder builder = new(text.Length);
            int count = 0;
            while (count < maxTextElements && enumerator.MoveNext())
            {
                builder.Append(enumerator.GetTextElement());
                count++;
            }

            return builder.ToString();
        }

        private static int ResolvePreviousTextElementBoundary(string text, int caretIndex)
        {
            int previousBoundary = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text ?? string.Empty);
            while (enumerator.MoveNext())
            {
                int elementEnd = enumerator.ElementIndex + enumerator.GetTextElement().Length;
                if (elementEnd >= caretIndex)
                {
                    break;
                }

                previousBoundary = elementEnd;
            }

            return previousBoundary;
        }

        private static int ResolveNextTextElementBoundary(string text, int caretIndex)
        {
            string resolvedText = text ?? string.Empty;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(resolvedText);
            while (enumerator.MoveNext())
            {
                int elementEnd = enumerator.ElementIndex + enumerator.GetTextElement().Length;
                if (elementEnd > caretIndex)
                {
                    return elementEnd;
                }
            }

            return resolvedText.Length;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFont(
            int nHeight,
            int nWidth,
            int nEscapement,
            int nOrientation,
            int fnWeight,
            uint fdwItalic,
            uint fdwUnderline,
            uint fdwStrikeOut,
            uint fdwCharSet,
            uint fdwOutputPrecision,
            uint fdwClipPrecision,
            uint fdwQuality,
            uint fdwPitchAndFamily,
            string lpszFace);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessageString(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern int SendMessageInt(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, IntPtr lpBuf, int dwBufLen);

        [DllImport("imm32.dll", EntryPoint = "ImmGetCandidateListCountW")]
        private static extern int ImmGetCandidateListCount(IntPtr hIMC, out int lpdwListCount);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern bool ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr hIMC, int dwAction, int dwIndex, int dwValue);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetOpenStatus(IntPtr hIMC, bool fOpen);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref COMPOSITIONFORM compositionForm);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCandidateWindow(IntPtr hIMC, ref CANDIDATEFORM candidateForm);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, newLong)
                : SetWindowLong32(hWnd, nIndex, newLong);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMPOSITIONFORM
        {
            public uint dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CANDIDATEFORM
        {
            public uint dwIndex;
            public uint dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
