using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Xenko.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements Xenko specific keyboard state
    /// </summary>
    public class XenkoKeyboardState : KeyboardStateBase
    {
        private Dictionary<int, int> translationTable = new Dictionary<int, int>();

        /// <summary>
        /// Initializes a new instance of the <see cref="XenkoKeyboardState"/> class.
        /// </summary>
        public XenkoKeyboardState()
            : base()
        {
            translationTable.Add(8, 2);
            translationTable.Add(9, 3);
            translationTable.Add(13, 6);
            translationTable.Add(19, 7);
            translationTable.Add(20, 8);
            translationTable.Add(21, 9);
            translationTable.Add(25, 12);
            translationTable.Add(27, 13);
            translationTable.Add(28, 14);
            translationTable.Add(29, 15);
            translationTable.Add(144, 114);
            translationTable.Add(145, 115);
            translationTable.Add(226, 154);
            translationTable.Add(254, 171);            
        }

        /// <summary>
        /// Determines whether [is key pressed] [the specified key code].
        /// </summary>
        /// <param name="keyCode">The key code.</param>
        /// <returns></returns>
        public override bool IsKeyPressed(KeyCode keyCode)
        {
            Keys key = Keys.None;
            int code = (int)keyCode;
            if (code >= 32 && code <= 57)
            {
                key = (Keys)(code - 14);
            }
            else
                if (code >= 65 && code <= 93)
                {
                    key = (Keys)(code - 21);
                }
                else
                    if (code >= 95 && code <= 135)
                    {
                        key = (Keys)(code - 22);
                    }
                    else
                        if (code >= 160 && code <= 183)
                        {
                            key = (Keys)(code - 44);
                        }
                        else
                            if (code >= 186 && code <= 192)
                            {
                                key = (Keys)(code - 46);
                            }
                            else
                                if (code >= 219 && code <= 223)
                                {
                                    key = (Keys)(code - 70);
                                }
                                else
                                    if (code >= 246 && code <= 251)
                                    {
                                        key = (Keys)(code - 83);
                                    }
                                    else
                                    {
                                        int newCode = 0;
                                        if (translationTable.TryGetValue(code, out newCode))
                                        {
                                            key = (Keys)newCode;
                                        }
                                        else
                                        {
                                            return false;
                                        }
                                    }

            return XenkoInputDevice.NativeInputManager.IsKeyDown(key);
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public override void Update()
        {            
        }
    }
}
