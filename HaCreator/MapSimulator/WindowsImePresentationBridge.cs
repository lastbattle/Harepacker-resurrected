using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal static class WindowsImePresentationBridge
    {
        private const uint ImeExcludeStyle = 0x0080;
        private const int CandidateListCount = 4;

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
                    dwStyle = ImeExcludeStyle,
                    ptCurrentPos = CreatePoint(placement.CompositionPoint),
                    rcArea = CreateRect(placement.CompositionExcludeArea)
                };

                bool updated = ImmSetCompositionWindow(inputContext, ref compositionForm);
                for (uint index = 0; index < CandidateListCount; index++)
                {
                    CANDIDATEFORM candidateForm = new()
                    {
                        dwIndex = index,
                        dwStyle = ImeExcludeStyle,
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
    }
}
