using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements Xenko specific touch state
    /// </summary>
    public class XenkoTouchState : TouchStateBase
    {
        private float normalizedX;
        private float normalizedY;
        private TouchGestures gesture;
        private bool isTouched;
        private TouchAction action;
        private float moveX;
        private float moveY;
        private bool hasGesture;
        private PointerEventType previousState;
        private Vector2 previousLocation;
        private int id;

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
            isTouched = false;
            hasGesture = false;

            Xenko.Input.InputManager manager = XenkoInputDevice.NativeInputManager;

            if (manager.PointerEvents.Count > 0)
            {
                var pointerEvent = manager.PointerEvents[0];
                if (pointerEvent.Pointer is IMouseDevice)
                {
                    isTouched = false;
                    return;
                }

                id = pointerEvent.PointerId;
                isTouched = true;
                normalizedX = pointerEvent.Position.X;
                normalizedY = pointerEvent.Position.Y;
                previousState = pointerEvent.EventType;
                previousLocation = pointerEvent.Position;
                switch (pointerEvent.EventType)
                {                    
                    case PointerEventType.Pressed:
                        action = TouchAction.Pressed;
                        break;
                    case PointerEventType.Moved:
                        action = TouchAction.Moved;
                        break;                    
                    case PointerEventType.Released:
                        action = TouchAction.Released;
                        break;
                    case PointerEventType.Canceled:
                        action = TouchAction.None;
                        break;
                    default:
                        break;
                }
            }
            else if (previousState == PointerEventType.Moved)
            {
                previousState = PointerEventType.Canceled;
                action = TouchAction.Released;
                normalizedX = previousLocation.X;
                normalizedY = previousLocation.Y;
                isTouched = true;
            }

            if (manager.GestureEvents.Count > 0)
            {                
                hasGesture = true;
                var gestureEvent = manager.GestureEvents[0];
                switch (gestureEvent.Type)
                {
                    case GestureType.Composite:
                        gesture = TouchGestures.MoveRotateAndScale;
                        var compositeEvent = gestureEvent as GestureEventComposite;
                        normalizedX = compositeEvent.CenterCurrentPosition.X;
                        normalizedY = compositeEvent.CenterCurrentPosition.Y;
                        moveX = compositeEvent.DeltaTranslation.X;
                        moveY = compositeEvent.DeltaTranslation.Y;
                        break;
                    case GestureType.Drag:
                        gesture = TouchGestures.FreeDrag;
                        var dragGestureEvent = gestureEvent as GestureEventDrag;
                        normalizedX = dragGestureEvent.CurrentPosition.X;
                        normalizedY = dragGestureEvent.CurrentPosition.Y;
                        moveX = dragGestureEvent.DeltaTranslation.X;
                        moveY = dragGestureEvent.DeltaTranslation.Y;
                        break;
                    case GestureType.Flick:                        
                        gesture = TouchGestures.Flick;
                        var flickEvent = gestureEvent as GestureEventFlick;
                        normalizedX = flickEvent.CurrentPosition.X;
                        normalizedY = flickEvent.CurrentPosition.Y;
                        moveX = flickEvent.DeltaTranslation.X;
                        moveY = flickEvent.DeltaTranslation.Y;                        
                        break;
                    case GestureType.LongPress:
                        gesture = TouchGestures.Hold;
                        var longPressEvent = gestureEvent as GestureEventLongPress;
                        normalizedX = longPressEvent.Position.X;
                        normalizedY = longPressEvent.Position.Y;
                        moveX = moveY = 0;
                        break;
                    case GestureType.Tap:                        
                        gesture = TouchGestures.Tap;
                        var tapEvent = gestureEvent as GestureEventTap;
                        normalizedX = tapEvent.TapPosition.X;
                        normalizedY = tapEvent.TapPosition.Y;
                        moveX = moveY = 0;
                        break;
                    default:
                        break;
                }                
            }
        }        
    }
}
