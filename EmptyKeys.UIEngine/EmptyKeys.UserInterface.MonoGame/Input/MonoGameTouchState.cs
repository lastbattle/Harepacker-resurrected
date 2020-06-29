using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input.Touch;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements MonoGame specific touch state
    /// </summary>
    public class MonoGameTouchState : TouchStateBase
    {
        private TouchLocation previousLocation;
        private TouchLocation[] empty = new TouchLocation[0];

        internal TouchCollection ActualState
        {
            get;
            set;
        }

        private int id;
        private float normalizedX;
        private float normalizedY;
        private TouchGestures gesture;
        private bool isTouched;
        private TouchAction action;
        private float moveX;
        private float moveY;
        private bool hasGesture;

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public override int Id
        {
            get { return id; }
        }

        /// <summary>
        /// Gets the normalized x.
        /// </summary>
        /// <value>
        /// The normalized x.
        /// </value>
        public override float NormalizedX
        {
            get { return normalizedX; }
        }

        /// <summary>
        /// Gets the normalized y.
        /// </summary>
        /// <value>
        /// The normalized y.
        /// </value>
        public override float NormalizedY
        {
            get { return normalizedY; }
        }

        /// <summary>
        /// Gets the gesture.
        /// </summary>
        /// <value>
        /// The gesture.
        /// </value>
        public override TouchGestures Gesture
        {
            get { return gesture; }
        }

        /// <summary>
        /// Gets a value indicating whether is touched.
        /// </summary>
        /// <value>
        ///   <c>true</c> if is touched; otherwise, <c>false</c>.
        /// </value>
        public override bool IsTouched
        {
            get { return isTouched; }
        }

        /// <summary>
        /// Gets the action.
        /// </summary>
        /// <value>
        /// The action.
        /// </value>
        public override TouchAction Action
        {
            get { return action; }
        }

        /// <summary>
        /// Gets the move x.
        /// </summary>
        /// <value>
        /// The move x.
        /// </value>
        public override float MoveX
        {
            get { return moveX; }
        }

        /// <summary>
        /// Gets the move y.
        /// </summary>
        /// <value>
        /// The move y.
        /// </value>
        public override float MoveY
        {
            get { return moveY; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has gesture.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has gesture; otherwise, <c>false</c>.
        /// </value>
        public override bool HasGesture
        {
            get { return hasGesture; }
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public override void Update()
        {
            hasGesture = false;
            isTouched = false;
            Rect viewport = Engine.Instance.Renderer.GetViewport();

            ActualState = GetState();
            if (ActualState.Count > 0)
            {
                isTouched = true;
                TouchLocation location = ActualState[0];

                id = location.Id;
                normalizedX = location.Position.X / viewport.Width;
                normalizedY = location.Position.Y / viewport.Height;

                switch (location.State)
                {
                    case TouchLocationState.Invalid:
                        action = TouchAction.None;
                        break;
                    case TouchLocationState.Moved:
                        action = TouchAction.Moved;
                        break;
                    case TouchLocationState.Pressed:
                        action = TouchAction.Pressed;
                        break;
                    case TouchLocationState.Released:
                        action = TouchAction.Released;
                        break;
                    default:
                        break;
                }
            }

            if (TouchPanel.IsGestureAvailable)
            {
                GestureSample gs = TouchPanel.ReadGesture();

                normalizedX = gs.Position.X / viewport.Width;
                normalizedY = gs.Position.Y / viewport.Height;
                moveX = gs.Delta.X;
                moveY = gs.Delta.Y;
                gesture = (TouchGestures)(int)gs.GestureType;
                hasGesture = true;
            }
        }

        private TouchCollection GetState()
        {                       
            TouchCollection actualState = Microsoft.Xna.Framework.Input.Touch.TouchPanel.GetState();
            TouchLocation[] locations = new TouchLocation[1];
            if (actualState.Count > 0)
            {
                TouchLocation location = actualState[0];
                TouchLocation previousState;
                location.TryGetPreviousLocation(out previousState);

                if (previousState.State == TouchLocationState.Pressed)
                {
                    locations[0] = new TouchLocation(previousState.Id, TouchLocationState.Pressed, location.Position);
                }
                else
                {
                    locations[0] = location;
                }

                previousLocation = location;
                return new TouchCollection(locations);
            }
            else if (previousLocation.State == TouchLocationState.Moved)
            {
                locations[0] = new TouchLocation(previousLocation.Id, TouchLocationState.Released, previousLocation.Position);
                previousLocation = new TouchLocation();
                return new TouchCollection(locations);
            }

            return new TouchCollection(empty);
        }        
    }
}
