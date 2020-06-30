using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements MonoGame specific keyboard state
    /// </summary>
    public class MonoGameKeyboardState : KeyboardStateBase
    {
        private KeyboardState state;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameKeyboardState"/> class.
        /// </summary>
        public MonoGameKeyboardState()
            : base()
        {
        }

        /// <summary>
        /// Determines whether [is key pressed] [the specified key code].
        /// </summary>
        /// <param name="keyCode">The key code.</param>
        /// <returns></returns>
        public override bool IsKeyPressed(KeyCode keyCode)
        {
            Keys key = (Keys)(int)keyCode;
            return state.IsKeyDown(key);
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public override void Update()
        {
            state = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        }
    }
}
