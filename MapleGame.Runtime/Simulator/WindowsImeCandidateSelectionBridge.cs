using System;
using System.Runtime.InteropServices;

namespace HaCreator.MapSimulator
{
    internal static class WindowsImeCandidateSelectionBridge
    {
        private const int NI_SELECTCANDIDATESTR = 0x0012;

        internal static bool TrySelectCandidate(IntPtr windowHandle, int listIndex, int candidateIndex)
        {
            if (windowHandle == IntPtr.Zero || listIndex < 0 || candidateIndex < 0)
            {
                return false;
            }

            IntPtr inputContext = ImmGetContext(windowHandle);
            if (inputContext == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return ImmNotifyIME(inputContext, NI_SELECTCANDIDATESTR, (uint)listIndex, (uint)candidateIndex);
            }
            finally
            {
                ImmReleaseContext(windowHandle, inputContext);
            }
        }

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr windowHandle);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr windowHandle, IntPtr inputContext);

        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr inputContext, int action, uint index, uint value);
    }
}
