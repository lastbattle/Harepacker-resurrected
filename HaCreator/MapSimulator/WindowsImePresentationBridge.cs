using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal static class WindowsImePresentationBridge
    {
        private const uint ImeDefaultStyle = 0x0000;
        private const int NotifyCloseCandidate = 0x0011;
        private const int NotifyCompositionString = 0x0015;
        private const uint CancelComposition = 0x0004;
        private const int CandidateListCount = ImeCandidateListInterop.CandidateListCount;

        internal static bool TryUpdatePlacement(IntPtr windowHandle, UI.SkillMacroImeWindowPlacement placement)
        {
            if (windowHandle == IntPtr.Zero)
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
                COMPOSITIONFORM compositionForm = new()
                {
                    dwStyle = placement.CompositionStyle,
                    ptCurrentPos = CreatePoint(placement.CompositionPoint),
                    rcArea = CreateRect(placement.CompositionExcludeArea)
                };

                bool updated = ImmSetCompositionWindow(inputContext, ref compositionForm);
                for (uint index = 0; index < CandidateListCount; index++)
                {
                    CANDIDATEFORM candidateForm = new()
                    {
                        dwIndex = index,
                        dwStyle = placement.CandidateStyle,
                        ptCurrentPos = CreatePoint(placement.CandidatePoint),
                        rcArea = CreateRect(placement.CandidateExcludeArea)
                    };

                    updated = ImmSetCandidateWindow(inputContext, ref candidateForm) || updated;
                }

                return updated;
            }
            finally
            {
                ImmReleaseContext(windowHandle, inputContext);
            }
        }

        internal static bool TryResetPlacement(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
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
                COMPOSITIONFORM compositionForm = new()
                {
                    dwStyle = ImeDefaultStyle,
                    ptCurrentPos = default,
                    rcArea = default
                };

                bool updated = ImmSetCompositionWindow(inputContext, ref compositionForm);
                updated = ImmNotifyIME(inputContext, NotifyCompositionString, CancelComposition, 0) || updated;
                for (uint index = 0; index < CandidateListCount; index++)
                {
                    CANDIDATEFORM candidateForm = new()
                    {
                        dwIndex = index,
                        dwStyle = ImeDefaultStyle,
                        ptCurrentPos = default,
                        rcArea = default
                    };

                    updated = ImmSetCandidateWindow(inputContext, ref candidateForm) || updated;
                    updated = ImmNotifyIME(inputContext, NotifyCloseCandidate, index, 0) || updated;
                }

                return updated;
            }
            finally
            {
                ImmReleaseContext(windowHandle, inputContext);
            }
        }

        private static POINT CreatePoint(Point point) => new() { x = point.X, y = point.Y };

        private static RECT CreateRect(Rectangle rectangle) => new()
        {
            left = rectangle.X,
            top = rectangle.Y,
            right = rectangle.Right,
            bottom = rectangle.Bottom
        };

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

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr windowHandle);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr windowHandle, IntPtr inputContext);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCompositionWindow(IntPtr inputContext, ref COMPOSITIONFORM compositionForm);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetCandidateWindow(IntPtr inputContext, ref CANDIDATEFORM candidateForm);

        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr inputContext, int action, uint index, uint value);
    }
}
