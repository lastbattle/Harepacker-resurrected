using HaCreator.MapEditor.Instance;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace HaCreator.MapSimulator.Entities
{
    public class ReactorItem : BaseDXDrawableItem
    {
        private readonly struct AuthoredStateTransition
        {
            public AuthoredStateTransition(int eventType, int targetState)
            {
                EventType = eventType;
                TargetState = targetState;
            }

            public int EventType { get; }

            public int TargetState { get; }
        }

        private readonly ReactorInstance _reactorInstance;
        private readonly Dictionary<int, IDXObject[]> _stateFrames;
        private readonly Dictionary<int, AuthoredStateTransition[]> _stateTransitions;
        private readonly int[] _availableStates;
        private int _activeState;
        private int _activeFrameIndex;
        private int _lastStateTick;

        public ReactorInstance ReactorInstance
        {
            get { return _reactorInstance; }
            private set { }
        }

        public IReadOnlyList<int> AvailableStates => _availableStates;

        public ReactorItem(ReactorInstance reactorInstance, List<IDXObject> frames)
            : base(frames, reactorInstance.Flip)
        {
            _reactorInstance = reactorInstance;
            _stateFrames = new Dictionary<int, IDXObject[]>();
            _stateTransitions = new Dictionary<int, AuthoredStateTransition[]>();
            _availableStates = Array.Empty<int>();
        }

        public ReactorItem(ReactorInstance reactorInstance, Dictionary<int, List<IDXObject>> stateFrames)
            : base(GetDefaultFrames(stateFrames), reactorInstance.Flip)
        {
            _reactorInstance = reactorInstance;
            _stateFrames = new Dictionary<int, IDXObject[]>();
            _stateTransitions = LoadStateTransitions(reactorInstance);

            if (stateFrames != null)
            {
                foreach (KeyValuePair<int, List<IDXObject>> kvp in stateFrames)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        _stateFrames[kvp.Key] = kvp.Value.ToArray();
                    }
                }
            }

            _availableStates = _stateFrames.Keys.OrderBy(state => state).ToArray();
            _activeState = ResolveInitialState();
        }

        public ReactorItem(ReactorInstance reactorInstance, IDXObject frame0)
            : base(frame0, reactorInstance.Flip)
        {
            _reactorInstance = reactorInstance;
            _stateFrames = new Dictionary<int, IDXObject[]>();
            _stateTransitions = new Dictionary<int, AuthoredStateTransition[]>();
            _availableStates = Array.Empty<int>();
        }

        public void SetAnimationState(int state, int tickCount)
        {
            int resolvedState = ResolveState(state);
            if (resolvedState == _activeState)
                return;

            _activeState = resolvedState;
            _activeFrameIndex = 0;
            _lastStateTick = tickCount;
        }

        public int GetInitialState()
        {
            return ResolveInitialState();
        }

        public bool TryGetNextState(int currentState, out int nextState)
        {
            return TryGetNextState(currentState, ReactorActivationType.None, out nextState);
        }

        public bool TryGetNextState(int currentState, ReactorActivationType activationType, out int nextState)
        {
            nextState = currentState;

            if (TryGetAuthoredNextState(currentState, activationType, out nextState))
                return true;

            if (_availableStates.Length == 0)
                return false;

            int resolvedState = ResolveState(currentState);
            for (int i = 0; i < _availableStates.Length; i++)
            {
                if (_availableStates[i] <= resolvedState)
                    continue;

                nextState = _availableStates[i];
                return true;
            }

            return false;
        }

        public bool TryGetTimedStateTransition(int currentState, out int nextState)
        {
            return TryGetAuthoredNextState(currentState, ReactorActivationType.Time, out nextState);
        }

        public IReadOnlyCollection<int> GetAuthoredEventTypes()
        {
            if (_stateTransitions.Count == 0)
                return Array.Empty<int>();

            return _stateTransitions.Values
                .SelectMany(static transitions => transitions)
                .Select(static transition => transition.EventType)
                .Distinct()
                .ToArray();
        }

        public int GetStateDuration(int state)
        {
            int resolvedState = ResolveState(state);
            if (!_stateFrames.TryGetValue(resolvedState, out IDXObject[] frames) || frames.Length == 0)
                return 0;

            int totalDuration = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            return totalDuration;
        }

        public int GetCurrentFrameIndex(int tickCount)
        {
            if (_stateFrames.Count == 0)
                return 0;

            GetStateFrame(tickCount, out int frameIndex);
            return frameIndex;
        }

        public Rectangle GetCurrentBounds(int tickCount)
        {
            IDXObject frame = _stateFrames.Count > 0
                ? GetStateFrame(tickCount, out _)
                : LastFrameDrawn ?? Frame0;

            if (frame == null)
            {
                int fallbackX = ReactorInstance?.X ?? 0;
                int fallbackY = ReactorInstance?.Y ?? 0;
                int fallbackWidth = Math.Max(1, ReactorInstance?.Width ?? 1);
                int fallbackHeight = Math.Max(1, ReactorInstance?.Height ?? 1);
                return new Rectangle(fallbackX, fallbackY, fallbackWidth, fallbackHeight);
            }

            return new Rectangle(
                frame.X - Position.X,
                frame.Y - Position.Y,
                Math.Max(1, frame.Width),
                Math.Max(1, frame.Height));
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_stateFrames.Count > 0)
            {
                IDXObject drawFrame = GetStateFrame(TickCount);
                if (drawFrame == null)
                    return;

                int shiftCenteredX = mapShiftX - centerX;
                int shiftCenteredY = mapShiftY - centerY;
                if (!IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY, renderParameters.RenderWidth, renderParameters.RenderHeight))
                    return;

                drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX - Position.X, shiftCenteredY - Position.Y,
                    flip,
                    drawReflectionInfo);
                return;
            }

            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);
        }

        private IDXObject GetStateFrame(int tickCount)
        {
            return GetStateFrame(tickCount, out _);
        }

        private IDXObject GetStateFrame(int tickCount, out int frameIndex)
        {
            if (!_stateFrames.TryGetValue(_activeState, out IDXObject[] frames) || frames.Length == 0)
            {
                if (!_stateFrames.TryGetValue(0, out frames) || frames.Length == 0)
                {
                    frameIndex = 0;
                    return null;
                }
            }

            if (_activeFrameIndex >= frames.Length)
                _activeFrameIndex = 0;

            if (frames.Length > 1)
            {
                int elapsed = tickCount - _lastStateTick;
                while (elapsed > 0)
                {
                    int currentDelay = Math.Max(1, frames[_activeFrameIndex].Delay);
                    if (elapsed < currentDelay)
                        break;

                    elapsed -= currentDelay;
                    _activeFrameIndex = (_activeFrameIndex + 1) % frames.Length;
                    _lastStateTick += currentDelay;
                }
            }

            frameIndex = _activeFrameIndex;
            return frames[_activeFrameIndex];
        }

        private int ResolveState(int state)
        {
            if (_stateFrames.Count == 0 || _stateFrames.ContainsKey(state))
                return state;

            int lowerOrEqualState = _stateFrames.Keys
                .Where(key => key <= state)
                .DefaultIfEmpty(int.MinValue)
                .Max();
            if (lowerOrEqualState != int.MinValue)
                return lowerOrEqualState;

            return _stateFrames.Keys.OrderBy(key => key).First();
        }

        private int ResolveInitialState()
        {
            if (_availableStates.Length == 0)
                return 0;

            return _stateFrames.ContainsKey(0) ? 0 : _availableStates[0];
        }

        private bool TryGetAuthoredNextState(int currentState, ReactorActivationType activationType, out int nextState)
        {
            nextState = currentState;

            int resolvedState = ResolveState(currentState);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return false;
            }

            int[] candidateTargets = transitions
                .Where(transition => MatchesAuthoredEventType(transition.EventType, activationType))
                .Select(transition => transition.TargetState)
                .Where(targetState => _stateFrames.ContainsKey(targetState))
                .Distinct()
                .ToArray();

            if (candidateTargets.Length != 1)
                return false;

            int targetState = candidateTargets[0];
            if (targetState == resolvedState)
                return false;

            nextState = targetState;
            return true;
        }

        private static bool MatchesAuthoredEventType(int eventType, ReactorActivationType activationType)
        {
            return activationType switch
            {
                ReactorActivationType.Touch or ReactorActivationType.Quest => eventType is 0 or 6 or 100,
                ReactorActivationType.Hit => eventType is 1 or 2 or 8,
                ReactorActivationType.Skill => eventType == 5,
                ReactorActivationType.Item => eventType == 9,
                ReactorActivationType.Time => eventType is 7 or 101,
                _ => false
            };
        }

        private static Dictionary<int, AuthoredStateTransition[]> LoadStateTransitions(ReactorInstance reactorInstance)
        {
            var transitions = new Dictionary<int, AuthoredStateTransition[]>();

            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
                return transitions;

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out int stateId))
                    continue;

                if (WzInfoTools.GetRealProperty(property)?["event"] is not WzSubProperty eventProperty)
                    continue;

                AuthoredStateTransition[] stateTransitions = eventProperty.WzProperties
                    .OfType<WzSubProperty>()
                    .Select(static eventNode => TryCreateTransition(eventNode))
                    .Where(static transition => transition.HasValue)
                    .Select(static transition => transition.Value)
                    .ToArray();

                if (stateTransitions.Length > 0)
                {
                    transitions[stateId] = stateTransitions;
                }
            }

            return transitions;
        }

        private static AuthoredStateTransition? TryCreateTransition(WzSubProperty eventNode)
        {
            int? eventType = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["type"]));
            int? targetState = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["state"]));
            if (!eventType.HasValue || !targetState.HasValue)
                return null;

            return new AuthoredStateTransition(eventType.Value, targetState.Value);
        }

        private static int? TryReadOptionalInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => checked((int)longProperty.Value),
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int parsedValue) => parsedValue,
                _ => null
            };
        }

        private static List<IDXObject> GetDefaultFrames(Dictionary<int, List<IDXObject>> stateFrames)
        {
            if (stateFrames == null || stateFrames.Count == 0)
                throw new ArgumentException("Reactor state frames cannot be empty.", nameof(stateFrames));

            if (stateFrames.TryGetValue(0, out List<IDXObject> defaultFrames) && defaultFrames?.Count > 0)
                return defaultFrames;

            List<IDXObject> firstFrames = stateFrames.OrderBy(kvp => kvp.Key).First().Value;
            if (firstFrames == null || firstFrames.Count == 0)
                throw new ArgumentException("Reactor state frames cannot be empty.", nameof(stateFrames));

            return firstFrames;
        }
    }
}
