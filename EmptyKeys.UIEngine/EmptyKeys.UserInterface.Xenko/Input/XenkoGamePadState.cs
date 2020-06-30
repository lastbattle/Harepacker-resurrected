using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xenko.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements Xenko engine specific game pad state
    /// </summary>
    public class XenkoGamePadState : GamePadStateBase
    {
        private GamePadState state;

        /// <summary>
        /// Gets a value indicating whether this instance is a button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is a button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsAButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.A); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is b button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is b button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsBButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.B); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is c button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is c button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsCButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.Y); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is d button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is d button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsDButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.X); }
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
                PointF pad = new PointF();
                pad.X = state.Buttons.HasFlag(GamePadButton.PadRight) ? 1 : pad.X;
                pad.Y = state.Buttons.HasFlag(GamePadButton.PadUp) ? 1 : pad.Y;
                pad.X = state.Buttons.HasFlag(GamePadButton.PadLeft) ? -1 : pad.X;
                pad.Y = state.Buttons.HasFlag(GamePadButton.PadDown) ? -1 : pad.Y;
                return pad;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is left shoulder button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is left shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsLeftShoulderButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.LeftShoulder); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is left stick button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is left stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsLeftStickButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.LeftThumb); }
        }

        /// <summary>
        /// Gets the left thumb stick.
        /// </summary>
        /// <value>
        /// The left thumb stick.
        /// </value>
        public override PointF LeftThumbStick
        {
            get { return new PointF(state.LeftThumb.X, state.LeftThumb.Y); }
        }

        /// <summary>
        /// Gets the left trigger.
        /// </summary>
        /// <value>
        /// The left trigger.
        /// </value>
        public override float LeftTrigger
        {
            get { return state.LeftTrigger; }
        }

        /// <summary>
        /// Gets the player number.
        /// </summary>
        /// <value>
        /// The player number.
        /// </value>
        public override int PlayerNumber
        {
            get { return 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is right shoulder button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is right shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsRightShoulderButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.RightShoulder); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is right stick button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is right stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsRightStickButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.RightThumb); ; }
        }

        /// <summary>
        /// Gets the right thumb stick.
        /// </summary>
        /// <value>
        /// The right thumb stick.
        /// </value>
        public override PointF RightThumbStick
        {
            get { return new PointF(state.RightThumb.X, state.RightThumb.Y); }
        }

        /// <summary>
        /// Gets the right trigger.
        /// </summary>
        /// <value>
        /// The right trigger.
        /// </value>
        public override float RightTrigger
        {
            get { return state.RightTrigger; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is select button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is select button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsSelectButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.Back); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is start button pressed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is start button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsStartButtonPressed
        {
            get { return state.Buttons.HasFlag(GamePadButton.Start); }
        }

        /// <summary>
        /// Updates the specified game pad index.
        /// </summary>
        /// <param name="gamePadIndex">Index of the game pad.</param>
        public override void Update(int gamePadIndex)
        {
            var input = XenkoInputDevice.NativeInputManager;
            if (input.HasGamePad)
            {
                state = input.DefaultGamePad.State;                               
            }
        }
    }
}
