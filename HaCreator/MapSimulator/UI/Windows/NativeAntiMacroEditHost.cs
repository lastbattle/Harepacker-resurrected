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
        private const int WmSetFont = 0x0030;
        private const int WmGetFont = 0x0031;
        private const int WmChar = 0x0102;
        private const int WmKeyDown = 0x0100;
        private const int WmPaste = 0x0302;
        private const int EmGetSel = 0x00B0;
        private const int EmSetSel = 0x00B1;
        private const int EmReplaceSel = 0x00C2;
        private const int EmLimitText = 0x00C5;
        private const int VkReturn = 0x0D;
        private const uint WsChild = 0x40000000;
        private const uint WsVisible = 0x10000000;
        private const uint WsTabStop = 0x00010000;
        private const uint EsAutoHScroll = 0x0080;
        private const uint WsExClientEdge = 0x00000200;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private static readonly object HostMapLock = new();
        private static readonly Dictionary<IntPtr, NativeAntiMacroEditHost> HostByHandle = new();

        private readonly int _maxLength;
        private readonly WndProcDelegate _subclassWndProc;

        private IntPtr _parentHandle;
        private IntPtr _editHandle;
        private IntPtr _originalWndProc;
        private IntPtr _fontHandle;
        private string _lastKnownText = string.Empty;

        public NativeAntiMacroEditHost(int maxLength)
        {
            _maxLength = Math.Max(1, maxLength);
            _subclassWndProc = SubclassWndProc;
        }

        public bool IsAttached => _editHandle != IntPtr.Zero && IsWindow(_editHandle);
        public bool HasFocus => IsAttached && GetFocus() == _editHandle;
        public string Text => IsAttached ? GetControlText() : string.Empty;

        public event Action<string> TextChanged;
        public event Action SubmitRequested;

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
            _editHandle = CreateWindowEx(
                WsExClientEdge,
                "EDIT",
                string.Empty,
                WsChild | WsVisible | WsTabStop | EsAutoHScroll,
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
            ApplyClientFont();
            SendMessage(_editHandle, EmLimitText, new IntPtr(_maxLength), IntPtr.Zero);
            SendMessage(_editHandle, EmSetSel, IntPtr.Zero, IntPtr.Zero);
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

            SetWindowPos(_editHandle, HwndTop, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);
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
                SetWindowPos(_editHandle, HwndTop, 0, 0, 0, 0, SwpNoZOrder | SwpNoActivate);
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

        public void Reset()
        {
            if (!IsAttached)
            {
                return;
            }

            SetWindowText(_editHandle, string.Empty);
            SendMessage(_editHandle, EmSetSel, IntPtr.Zero, IntPtr.Zero);
            SynchronizeState();
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
        }

        public void Blur()
        {
            if (!IsAttached || _parentHandle == IntPtr.Zero)
            {
                return;
            }

            SetFocus(_parentHandle);
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
                preferredPrivateFontFamilyCandidates: ClientFontFamilyCandidates);

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

        private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmKeyDown && wParam.ToInt32() == VkReturn)
            {
                SubmitRequested?.Invoke();
                return IntPtr.Zero;
            }

            if (msg == WmChar && wParam.ToInt32() == VkReturn)
            {
                return IntPtr.Zero;
            }

            IntPtr result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
            if (msg == WmPaste)
            {
                SynchronizeState();
            }

            return result;
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

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, newLong)
                : SetWindowLong32(hWnd, nIndex, newLong);
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
