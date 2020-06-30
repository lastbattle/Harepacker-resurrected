using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements MonoGame specific Game Pad State
    /// </summary>
    public class MonoGameGamePadState : GamePadStateBase
    {
        private GamePadState state;
        private PlayerIndex playerIndex;

        /// <summary>
        /// Gets a value indicating whether this instance is a button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is a button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsAButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.A == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is b button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is b button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsBButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.B == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is c button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is c button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsCButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.X == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is d button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is d button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsDButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.Y == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets the d pad.
        /// </summary>
        /// <value>
        /// The d pad.
        /// </value>
        public override PointF DPad
        {
            get
            {
                if (state == null)
                {
                    return new PointF();
                }
                
                float upDown = 0;
                float leftRight = 0;

                if (state.DPad.Down == ButtonState.Pressed)
                {
                    upDown = -1;
                }

                if (state.DPad.Up == ButtonState.Pressed)
                {
                    upDown = 1;
                }

                if (state.DPad.Left == ButtonState.Pressed)
                {
                    leftRight = -1;
                }

                if (state.DPad.Right == ButtonState.Pressed)
                {
                    leftRight = 1;
                }

                return new PointF(leftRight, upDown);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is left shoulder button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsLeftShoulderButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.LeftShoulder == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is left stick button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsLeftStickButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.LeftStick == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets the left thumb stick.
        /// </summary>
        /// <value>
        /// The left thumb stick.
        /// </value>
        public override PointF LeftThumbStick
        {
            get
            {
                if (state == null)
                {
                    return new PointF();
                }

                return new PointF(state.ThumbSticks.Left.X, state.ThumbSticks.Left.Y);
            }
        }

        /// <summary>
        /// Gets the left trigger.
        /// </summary>
        /// <value>
        /// The left trigger.
        /// </value>
        public override float LeftTrigger
        {
            get
            {
                if (state == null)
                {
                    return 0;
                }

                return state.Triggers.Left;
            }
        }

        /// <summary>
        /// Gets the player number.
        /// </summary>
        /// <value>
        /// The player number.
        /// </value>
        public override int PlayerNumber
        {
            get
            {
                if (state == null)
                {
                    return 0;
                }

                return (int)playerIndex;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is right shoulder button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsRightShoulderButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.RightShoulder == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is right stick button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsRightStickButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.RightStick == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets the right thumb stick.
        /// </summary>
        /// <value>
        /// The right thumb stick.
        /// </value>
        public override PointF RightThumbStick
        {
            get
            {
                if (state == null)
                {
                    return new PointF();
                }

                return new PointF(state.ThumbSticks.Right.X, state.ThumbSticks.Right.Y);
            }
        }

        /// <summary>
        /// Gets the right trigger.
        /// </summary>
        /// <value>
        /// The right trigger.
        /// </value>
        public override float RightTrigger
        {
            get
            {
                if (state == null)
                {
                    return 0;
                }

                return state.Triggers.Right;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is select button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is select button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsSelectButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.Back == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is start button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is start button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsStartButtonPressed
        {
            get
            {
                if (state == null)
                {
                    return false;
                }

                return state.Buttons.Start == ButtonState.Pressed;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameGamePadState"/> class.
        /// </summary>
        public MonoGameGamePadState()
            : base()
        {
            Update(0);
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <param name="gamePadIndex">Index of the game pad.</param>
        public override void Update(int gamePadIndex)
        {
            playerIndex = (PlayerIndex)gamePadIndex;
            state = Microsoft.Xna.Framework.Input.GamePad.GetState(playerIndex);
        }
    }
}
