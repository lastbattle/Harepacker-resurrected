using System;
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
        private const int GCS_COMPSTR = 0x0008;
        private const int GCS_RESULTSTR = 0x0800;

        public event Action<string> CompositionTextChanged;

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
                    CompositionTextChanged?.Invoke(string.Empty);
                    break;
                case WM_IME_COMPOSITION:
                    HandleCompositionMessage(m);
                    break;
                case WM_IME_ENDCOMPOSITION:
                    CompositionTextChanged?.Invoke(string.Empty);
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
                    CompositionTextChanged?.Invoke(string.Empty);
                }

                return;
            }

            try
            {
                if ((flags & GCS_RESULTSTR) != 0)
                {
                    CompositionTextChanged?.Invoke(string.Empty);
                }

                if ((flags & GCS_COMPSTR) != 0)
                {
                    CompositionTextChanged?.Invoke(GetCompositionString(inputContext, GCS_COMPSTR));
                }
                else if ((flags & GCS_RESULTSTR) == 0)
                {
                    CompositionTextChanged?.Invoke(string.Empty);
                }
            }
            finally
            {
                ImmReleaseContext(message.HWnd, inputContext);
            }
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

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr windowHandle);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr windowHandle, IntPtr inputContext);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionStringW(IntPtr inputContext, int index, [Out] byte[] buffer, int bufferLength);
    }
}
