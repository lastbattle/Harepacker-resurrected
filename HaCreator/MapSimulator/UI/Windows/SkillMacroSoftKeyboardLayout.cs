using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal enum SkillMacroSoftKeyboardVisualState
    {
        Normal,
        Hovered,
        Pressed,
        Disabled
    }

    internal enum SkillMacroSoftKeyboardFunctionKey
    {
        None,
        CapsLock,
        LeftShift,
        RightShift,
        Enter,
        Backspace
    }

    internal enum SkillMacroSoftKeyboardWindowButton
    {
        None,
        Close,
        Minimize,
        Maximize
    }

    internal sealed class SkillMacroSoftKeyboardKeyTextures
    {
        public Texture2D Normal { get; init; }
        public Texture2D Hovered { get; init; }
        public Texture2D Pressed { get; init; }
        public Texture2D Disabled { get; init; }

        public Texture2D Resolve(SkillMacroSoftKeyboardVisualState state)
        {
            return state switch
            {
                SkillMacroSoftKeyboardVisualState.Hovered => Hovered ?? Normal,
                SkillMacroSoftKeyboardVisualState.Pressed => Pressed ?? Hovered ?? Normal,
                SkillMacroSoftKeyboardVisualState.Disabled => Disabled ?? Normal,
                _ => Normal
            };
        }
    }

    internal sealed class SkillMacroSoftKeyboardSkin
    {
        public Texture2D ExpandedBackground { get; init; }
        public Texture2D MinimizedBackground { get; init; }
        public Texture2D ExpandedTitle { get; init; }
        public Texture2D MinimizedTitle { get; init; }
        public Texture2D KeyboardBackground { get; init; }
        public Dictionary<int, SkillMacroSoftKeyboardKeyTextures> KeyTextures { get; } = new();
        public Dictionary<SkillMacroSoftKeyboardFunctionKey, SkillMacroSoftKeyboardKeyTextures> FunctionKeyTextures { get; } = new();
        public Dictionary<SkillMacroSoftKeyboardWindowButton, SkillMacroSoftKeyboardKeyTextures> WindowButtonTextures { get; } = new();
    }

    internal static class SkillMacroSoftKeyboardLayout
    {
        internal const int ExpandedWidth = 290;
        internal const int ExpandedHeight = 136;
        internal const int MinimizedHeight = 119;
        internal const int KeyboardTop = 20;
        internal const int KeyPitch = 23;
        internal const int KeyWidth = 21;
        internal const int KeyHeight = 22;
        internal const int HeaderButtonY = 4;
        internal const int HeaderButtonSize = 12;

        private static readonly string[] LowercaseKeyTexts =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
            "q", "w", "e", "r", "t", "y", "u", "i", "o", "p",
            "a", "s", "d", "f", "g", "h", "j", "k", "l",
            "z", "x", "c", "v", "b", "n", "m", "-"
        };

        private static readonly string[] UppercaseKeyTexts =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
            "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
            "A", "S", "D", "F", "G", "H", "J", "K", "L",
            "Z", "X", "C", "V", "B", "N", "M", "_"
        };

        private static readonly Rectangle CapsLockBounds = new(12, 43, 32, 22);
        private static readonly Rectangle LeftShiftBounds = new(10, 66, 48, 22);
        private static readonly Rectangle RightShiftBounds = new(238, 66, 40, 22);
        private static readonly Rectangle EnterBounds = new(244, 43, 33, 22);
        private static readonly Rectangle BackspaceBounds = new(255, 20, 21, 45);
        private static readonly Rectangle CloseButtonBounds = new(274, HeaderButtonY, HeaderButtonSize, HeaderButtonSize);
        private static readonly Rectangle MinimizeButtonBounds = new(260, HeaderButtonY, HeaderButtonSize, HeaderButtonSize);
        private static readonly Rectangle MaximizeButtonBounds = new(246, HeaderButtonY, HeaderButtonSize, HeaderButtonSize);

        internal static Rectangle GetBounds(Point topLeft, bool minimized)
        {
            return new Rectangle(topLeft.X, topLeft.Y, ExpandedWidth, minimized ? MinimizedHeight : ExpandedHeight);
        }

        internal static Rectangle GetKeyBounds(int keyIndex)
        {
            return keyIndex switch
            {
                >= 0 and <= 9 => new Rectangle(24 + (keyIndex * KeyPitch), KeyboardTop, KeyWidth, KeyHeight),
                >= 10 and <= 19 => new Rectangle(47 + ((keyIndex - 10) * KeyPitch), KeyboardTop + KeyPitch, KeyWidth, KeyHeight),
                >= 20 and <= 28 => new Rectangle(58 + ((keyIndex - 20) * KeyPitch), KeyboardTop + (KeyPitch * 2), KeyWidth, KeyHeight),
                >= 29 and <= 35 => new Rectangle(74 + ((keyIndex - 29) * KeyPitch), KeyboardTop + (KeyPitch * 3), KeyWidth, KeyHeight),
                36 => new Rectangle(239, KeyboardTop + (KeyPitch * 3), KeyWidth, KeyHeight),
                _ => Rectangle.Empty
            };
        }

        internal static int GetKeyIndexFromPoint(int localX, int localY)
        {
            if (localY - KeyboardTop < 0)
            {
                return -1;
            }

            switch ((localY - KeyboardTop) / KeyPitch)
            {
                case 0:
                    return ResolveLinearKeyIndex(localX, 24, 10, 0);
                case 1:
                    return ResolveLinearKeyIndex(localX, 47, 10, 10);
                case 2:
                    return ResolveLinearKeyIndex(localX, 58, 9, 20);
                case 3:
                    return ResolveLinearKeyIndex(localX, 74, 7, 29);
                default:
                    return -1;
            }
        }

        internal static SkillMacroSoftKeyboardFunctionKey GetFunctionKeyFromPoint(int localX, int localY, bool minimized)
        {
            if (BackspaceBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardFunctionKey.Backspace;
            }

            if (minimized)
            {
                return SkillMacroSoftKeyboardFunctionKey.None;
            }

            if (CapsLockBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardFunctionKey.CapsLock;
            }

            if (LeftShiftBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardFunctionKey.LeftShift;
            }

            if (RightShiftBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardFunctionKey.RightShift;
            }

            return EnterBounds.Contains(localX, localY)
                ? SkillMacroSoftKeyboardFunctionKey.Enter
                : SkillMacroSoftKeyboardFunctionKey.None;
        }

        internal static SkillMacroSoftKeyboardWindowButton GetWindowButtonFromPoint(int localX, int localY)
        {
            if (CloseButtonBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardWindowButton.Close;
            }

            if (MinimizeButtonBounds.Contains(localX, localY))
            {
                return SkillMacroSoftKeyboardWindowButton.Minimize;
            }

            return MaximizeButtonBounds.Contains(localX, localY)
                ? SkillMacroSoftKeyboardWindowButton.Maximize
                : SkillMacroSoftKeyboardWindowButton.None;
        }

        internal static Rectangle GetWindowButtonBounds(SkillMacroSoftKeyboardWindowButton button)
        {
            return button switch
            {
                SkillMacroSoftKeyboardWindowButton.Close => CloseButtonBounds,
                SkillMacroSoftKeyboardWindowButton.Minimize => MinimizeButtonBounds,
                SkillMacroSoftKeyboardWindowButton.Maximize => MaximizeButtonBounds,
                _ => Rectangle.Empty
            };
        }

        internal static Rectangle GetFunctionKeyBounds(SkillMacroSoftKeyboardFunctionKey key)
        {
            return key switch
            {
                SkillMacroSoftKeyboardFunctionKey.CapsLock => CapsLockBounds,
                SkillMacroSoftKeyboardFunctionKey.LeftShift => LeftShiftBounds,
                SkillMacroSoftKeyboardFunctionKey.RightShift => RightShiftBounds,
                SkillMacroSoftKeyboardFunctionKey.Enter => EnterBounds,
                SkillMacroSoftKeyboardFunctionKey.Backspace => BackspaceBounds,
                _ => Rectangle.Empty
            };
        }

        internal static string GetKeyText(int keyIndex, bool uppercase)
        {
            if (keyIndex < 0 || keyIndex >= LowercaseKeyTexts.Length)
            {
                return string.Empty;
            }

            return uppercase ? UppercaseKeyTexts[keyIndex] : LowercaseKeyTexts[keyIndex];
        }

        internal static IEnumerable<int> EnumerateVisibleKeyIndices(bool minimized)
        {
            if (minimized)
            {
                yield break;
            }

            for (int i = 0; i <= 35; i++)
            {
                yield return i;
            }
        }

        private static int ResolveLinearKeyIndex(int localX, int startX, int count, int baseIndex)
        {
            int adjustedX = localX - startX;
            if (adjustedX < 0)
            {
                return -1;
            }

            int offset = adjustedX / KeyPitch;
            if (offset < 0 || offset >= count)
            {
                return -1;
            }

            return baseIndex + offset;
        }
    }
}
