using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
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
        private const int VkF1 = 0x70;
        private const int VkF12 = 0x7B;
        private const int VkA = 0x41;
        private const int VkY = 0x59;
        private const int VkZ = 0x5A;
        private const int VkControl = 0x11;
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

            SendMessage(_editHandle, EmSetSel, new IntPtr(selectionStart - 1), new IntPtr(selectionStart));
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

                selectionStart--;
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
            if (msg == WmGetDlgCode)
            {
                IntPtr originalResult = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                return new IntPtr(originalResult.ToInt32() | GetClientOwnedAntiMacroDialogCode());
            }

            if (msg == WmKeyDown && wParam.ToInt32() == VkReturn)
            {
                SubmitRequested?.Invoke();
                return IntPtr.Zero;
            }

            if (msg == WmChar && wParam.ToInt32() == VkReturn)
            {
                return IntPtr.Zero;
            }

            if (ShouldSuppressClientUnsupportedEditShortcut(msg, wParam.ToInt32(), IsControlKeyDown()))
            {
                return IntPtr.Zero;
            }

            if (msg == WmUndo || msg == WmContextMenu)
            {
                return IntPtr.Zero;
            }

            if ((msg == WmKeyDown || msg == WmKeyUp) && IsStagePassthroughVirtualKey(wParam.ToInt32()))
            {
                ForwardKeyToParent(msg, wParam, lParam);
                return IntPtr.Zero;
            }

            IntPtr result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
            if (msg == WmSetFocus)
            {
                UpdateImePlacement();
                FocusChanged?.Invoke(true);
            }
            else if (msg == WmKillFocus)
            {
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

        internal static bool ShouldSuppressClientUnsupportedEditShortcut(uint msg, int virtualKey, bool controlHeld)
        {
            if (!controlHeld || msg != WmKeyDown)
            {
                return false;
            }

            // `CCtrlEdit::OnKey` handles Ctrl+C/V/X, but not the Win32 EDIT affordances
            // for select-all or undo/redo. Keep those hosted-control extras out of the
            // anti-macro seam so it stays closer to the recovered client widget path.
            return virtualKey is VkA or VkY or VkZ;
        }

        private static bool IsControlKeyDown()
        {
            return (GetKeyState(VkControl) & 0x8000) != 0;
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
