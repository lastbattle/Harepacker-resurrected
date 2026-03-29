using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HaCreator.MapSimulator
{
    internal sealed class WindowsImeCompositionMonitor : NativeWindow, IDisposable
    {
        private const int WM_IME_STARTCOMPOSITION = 0x010D;
        private const int WM_IME_ENDCOMPOSITION = 0x010E;
        private const int WM_IME_COMPOSITION = 0x010F;
        private const int WM_IME_NOTIFY = 0x0282;
        private const int GCS_COMPSTR = 0x0008;
        private const int GCS_COMPCLAUSE = 0x0020;
        private const int GCS_CURSORPOS = 0x0080;
        private const int GCS_RESULTSTR = 0x0800;
        private const int IMN_CHANGECANDIDATE = 0x0003;
        private const int IMN_CLOSECANDIDATE = 0x0004;
        private const int IMN_OPENCANDIDATE = 0x0005;

        public event Action<string> CompositionTextChanged;
        public event Action<ImeCompositionState> CompositionStateChanged;
        public event Action<ImeCandidateListState> CandidateListChanged;

        private ImeCompositionState _lastCompositionState = ImeCompositionState.Empty;

        public void Attach(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (Handle != IntPtr.Zero)
            {
                ReleaseHandle();
            }

            AssignHandle(handle);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_IME_STARTCOMPOSITION:
                    PublishCompositionState(ImeCompositionState.Empty);
                    break;
                case WM_IME_COMPOSITION:
                    HandleCompositionMessage(m);
                    break;
                case WM_IME_ENDCOMPOSITION:
                    PublishCompositionState(ImeCompositionState.Empty);
                    PublishCandidateList(ImeCandidateListState.Empty);
                    break;
                case WM_IME_NOTIFY:
                    HandleNotifyMessage(m);
                    break;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                ReleaseHandle();
            }
        }

        private void HandleCompositionMessage(Message message)
        {
            long flags = message.LParam.ToInt64();
            IntPtr inputContext = ImmGetContext(message.HWnd);
            if (inputContext == IntPtr.Zero)
            {
                if ((flags & GCS_RESULTSTR) != 0)
                {
                    PublishCompositionState(ImeCompositionState.Empty);
                }

                return;
            }

            try
            {
                if ((flags & GCS_COMPSTR) != 0)
                {
                    string compositionText = GetCompositionString(inputContext, GCS_COMPSTR);
                    IReadOnlyList<int> clauseOffsets = GetClauseOffsets(inputContext);
                    int cursorPosition = GetCursorPosition(inputContext);
                    PublishCompositionState(new ImeCompositionState(compositionText, clauseOffsets, cursorPosition));
                    return;
                }

                if ((flags & GCS_RESULTSTR) != 0 || (flags & GCS_COMPSTR) == 0)
                {
                    PublishCompositionState(ImeCompositionState.Empty);
                }
            }
            finally
            {
                ImmReleaseContext(message.HWnd, inputContext);
            }
        }

        private void HandleNotifyMessage(Message message)
        {
            switch (message.WParam.ToInt32())
            {
                case IMN_OPENCANDIDATE:
                case IMN_CHANGECANDIDATE:
                    IntPtr inputContext = ImmGetContext(message.HWnd);
                    if (inputContext == IntPtr.Zero)
                    {
                        PublishCandidateList(ImeCandidateListState.Empty);
                        return;
                    }

                    try
                    {
                        PublishCandidateList(GetCandidateListState(inputContext));
                    }
                    finally
                    {
                        ImmReleaseContext(message.HWnd, inputContext);
                    }
                    break;

                case IMN_CLOSECANDIDATE:
                    PublishCandidateList(ImeCandidateListState.Empty);
                    break;
            }
        }

        private void PublishCompositionState(ImeCompositionState state)
        {
            _lastCompositionState = state ?? ImeCompositionState.Empty;
            CompositionStateChanged?.Invoke(_lastCompositionState);
        }

        private void PublishCandidateList(ImeCandidateListState state)
        {
            CandidateListChanged?.Invoke(state ?? ImeCandidateListState.Empty);
        }

        private static string GetCompositionString(IntPtr inputContext, int compositionType)
        {
            int byteLength = ImmGetCompositionStringW(inputContext, compositionType, null, 0);
            if (byteLength <= 0)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[byteLength];
            int bytesRead = ImmGetCompositionStringW(inputContext, compositionType, buffer, byteLength);
            if (bytesRead <= 0)
            {
                return string.Empty;
            }

            return Encoding.Unicode.GetString(buffer, 0, bytesRead).TrimEnd('\0');
        }

        private static IReadOnlyList<int> GetClauseOffsets(IntPtr inputContext)
        {
            int byteLength = ImmGetCompositionStringW(inputContext, GCS_COMPCLAUSE, null, 0);
            if (byteLength < sizeof(uint))
            {
                return Array.Empty<int>();
            }

            byte[] buffer = new byte[byteLength];
            int bytesRead = ImmGetCompositionStringW(inputContext, GCS_COMPCLAUSE, buffer, byteLength);
            if (bytesRead < sizeof(uint))
            {
                return Array.Empty<int>();
            }

            int count = bytesRead / sizeof(uint);
            List<int> offsets = new(count);
            for (int i = 0; i < count; i++)
            {
                offsets.Add(BitConverter.ToInt32(buffer, i * sizeof(uint)));
            }

            return offsets;
        }

        private static int GetCursorPosition(IntPtr inputContext)
        {
            int cursorPosition = ImmGetCompositionStringW(inputContext, GCS_CURSORPOS, null, 0);
            return cursorPosition >= 0 ? cursorPosition : -1;
        }

        private ImeCandidateListState GetCandidateListState(IntPtr inputContext)
        {
            int byteLength = (int)ImmGetCandidateListW(inputContext, 0, null, 0);
            if (byteLength <= 0)
            {
                return ImeCandidateListState.Empty;
            }

            byte[] buffer = new byte[byteLength];
            uint bytesRead = ImmGetCandidateListW(inputContext, 0, buffer, (uint)buffer.Length);
            if (bytesRead < 24)
            {
                return ImeCandidateListState.Empty;
            }

            uint count = BitConverter.ToUInt32(buffer, 8);
            int selection = (int)BitConverter.ToUInt32(buffer, 12);
            int pageStart = (int)BitConverter.ToUInt32(buffer, 16);
            int pageSize = (int)BitConverter.ToUInt32(buffer, 20);

            List<string> candidates = new((int)count);
            int offsetTableStart = 24;
            for (int i = 0; i < count; i++)
            {
                int offsetIndex = offsetTableStart + (i * sizeof(uint));
                if (offsetIndex + sizeof(uint) > buffer.Length)
                {
                    break;
                }

                int stringOffset = (int)BitConverter.ToUInt32(buffer, offsetIndex);
                if (stringOffset < 0 || stringOffset >= buffer.Length)
                {
                    continue;
                }

                int terminatorOffset = stringOffset;
                while (terminatorOffset + 1 < buffer.Length)
                {
                    if (buffer[terminatorOffset] == 0 && buffer[terminatorOffset + 1] == 0)
                    {
                        break;
                    }

                    terminatorOffset += 2;
                }

                int stringByteLength = Math.Max(0, terminatorOffset - stringOffset);
                string candidate = stringByteLength > 0
                    ? Encoding.Unicode.GetString(buffer, stringOffset, stringByteLength)
                    : string.Empty;
                candidates.Add(candidate);
            }

            bool vertical = _lastCompositionState.CursorPosition >= 0;
            return candidates.Count > 0
                ? new ImeCandidateListState(candidates, pageStart, pageSize, selection, vertical)
                : ImeCandidateListState.Empty;
        }

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr windowHandle);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr windowHandle, IntPtr inputContext);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionStringW(IntPtr inputContext, int index, [Out] byte[] buffer, int bufferLength);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ImmGetCandidateListW(IntPtr inputContext, uint deIndex, [Out] byte[] candidateList, uint bufferLength);
    }
}
