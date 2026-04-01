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
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapSimulator.Entities
{
    internal readonly struct ReactorTransitionRequest
    {
        public ReactorTransitionRequest(
            ReactorActivationType activationType,
            ReactorType reactorType = ReactorType.UNKNOWN,
            int activationValue = 0)
        {
            ActivationType = activationType;
            ReactorType = reactorType;
            ActivationValue = activationValue;
        }

        public ReactorActivationType ActivationType { get; }

        public ReactorType ReactorType { get; }

        public int ActivationValue { get; }
    }

    public class ReactorItem : BaseDXDrawableItem
    {
        private readonly struct AuthoredStateTransition
        {
            public AuthoredStateTransition(int eventType, int targetState, int? selectorValue, int order)
            {
                EventType = eventType;
                TargetState = targetState;
                SelectorValue = selectorValue;
                Order = order;
            }
            public int EventType { get; }
            public int TargetState { get; }
            public int? SelectorValue { get; }
            public int Order { get; }
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
            return TryGetNextState(currentState, new ReactorTransitionRequest(activationType), out nextState);
        }

        internal int[] GetNextStateCandidates(int currentState, ReactorTransitionRequest request, bool includeNumericFallback = true)
        {
            int resolvedState = ResolveState(currentState);
            int[] authoredCandidates = GetAuthoredCandidates(resolvedState, request);
            if (authoredCandidates.Length > 0)
            {
                return authoredCandidates;
            }

            if (!includeNumericFallback || _availableStates.Length == 0)
            {
                return Array.Empty<int>();
            }

            for (int i = 0; i < _availableStates.Length; i++)
            {
                if (_availableStates[i] > resolvedState)
                {
                    return new[] { _availableStates[i] };
                }
            }

            return Array.Empty<int>();
        }

        internal bool CanActivateFromState(int currentState, ReactorTransitionRequest request)
        {
            int resolvedState = ResolveState(currentState);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return true;
            }

            AuthoredStateTransition[] matchingTransitions = transitions
                .Where(transition => MatchesAuthoredEventType(transition.EventType, request.ActivationType))
                .ToArray();
            if (matchingTransitions.Length == 0)
            {
                return true;
            }

            return FilterTransitionsBySelector(matchingTransitions, request).Length > 0;
        }

        internal bool TryGetNextState(int currentState, ReactorTransitionRequest request, out int nextState)
        {
            nextState = currentState;

            int[] candidates = GetNextStateCandidates(currentState, request);
            if (candidates.Length > 0)
            {
                nextState = candidates[0];
                return true;
            }

            return false;
        }

        public bool TryGetTimedStateTransition(int currentState, out int nextState)
        {
            nextState = currentState;
            int[] candidates = GetNextStateCandidates(
                currentState,
                new ReactorTransitionRequest(ReactorActivationType.Time),
                includeNumericFallback: false);
            if (candidates.Length == 0)
            {
                return false;
            }

            nextState = candidates[0];
            return true;
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

        private int[] GetAuthoredCandidates(int resolvedState, ReactorTransitionRequest request)
        {
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return Array.Empty<int>();
            }

            return FilterTransitionsBySelector(
                transitions.Where(transition => MatchesAuthoredEventType(transition.EventType, request.ActivationType)).ToArray(),
                request)
                .Where(transition => _stateFrames.ContainsKey(transition.TargetState))
                .Where(transition => transition.TargetState != resolvedState)
                .OrderBy(transition => GetEventTypePriority(transition.EventType, request))
                .ThenBy(transition => GetSelectorPriority(transition, request))
                .ThenBy(transition => transition.Order)
                .Select(transition => transition.TargetState)
                .Distinct()
                .ToArray();
        }

        private static AuthoredStateTransition[] FilterTransitionsBySelector(
            AuthoredStateTransition[] transitions,
            ReactorTransitionRequest request)
        {
            if (transitions == null || transitions.Length == 0)
            {
                return Array.Empty<AuthoredStateTransition>();
            }

            if (request.ActivationValue <= 0
                || (request.ActivationType != ReactorActivationType.Item
                    && request.ActivationType != ReactorActivationType.Skill))
            {
                return transitions;
            }

            AuthoredStateTransition[] filteredTransitions = transitions
                .Where(transition => !transition.SelectorValue.HasValue || transition.SelectorValue.Value == request.ActivationValue)
                .ToArray();

            return filteredTransitions.Length > 0
                ? filteredTransitions
                : Array.Empty<AuthoredStateTransition>();
        }

        private static int GetSelectorPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationValue <= 0)
            {
                return 0;
            }

            if (transition.SelectorValue == request.ActivationValue)
            {
                return 0;
            }

            return transition.SelectorValue.HasValue ? 2 : 1;
        }

        private static int GetEventTypePriority(int eventType, ReactorTransitionRequest request)
        {
            return request.ActivationType switch
            {
                ReactorActivationType.Touch => eventType switch
                {
                    0 => 0,
                    6 => 1,
                    100 => 2,
                    _ => 3
                },
                ReactorActivationType.Quest => eventType switch
                {
                    100 => 0,
                    6 => 1,
                    0 => 2,
                    _ => 3
                },
                ReactorActivationType.Hit => GetHitEventPriority(eventType, request.ReactorType),
                ReactorActivationType.Time => eventType switch
                {
                    101 => 0,
                    7 => 1,
                    _ => 2
                },
                _ => 0
            };
        }

        private static int GetHitEventPriority(int eventType, ReactorType reactorType)
        {
            return reactorType switch
            {
                ReactorType.ActivatedLeftHit => eventType switch
                {
                    1 => 0,
                    2 => 1,
                    8 => 2,
                    _ => 3
                },
                ReactorType.ActivatedRightHit => eventType switch
                {
                    2 => 0,
                    1 => 1,
                    8 => 2,
                    _ => 3
                },
                ReactorType.ActivatedByHarvesting => eventType switch
                {
                    8 => 0,
                    1 => 1,
                    2 => 2,
                    _ => 3
                },
                _ => eventType switch
                {
                    1 => 0,
                    2 => 1,
                    8 => 2,
                    _ => 3
                }
            };
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
                    .Select((eventNode, index) => TryCreateTransition(eventNode, index))
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

        private static AuthoredStateTransition? TryCreateTransition(WzSubProperty eventNode, int order)
        {
            int? eventType = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["type"]));
            int? targetState = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["state"]));
            if (!eventType.HasValue || !targetState.HasValue)
                return null;

            int? selectorValue = TryReadSelectorValue(eventNode);
            return new AuthoredStateTransition(eventType.Value, targetState.Value, selectorValue, order);
        }

        private static int? TryReadSelectorValue(WzSubProperty eventNode)
        {
            if (eventNode == null)
            {
                return null;
            }

            int? namedSelectorValue =
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["id"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["item"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["itemID"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["itemid"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["skill"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["skillID"])) ??
                TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode["skillid"]));

            if (namedSelectorValue.HasValue)
            {
                return namedSelectorValue;
            }

            return eventNode.WzProperties
                .Where(property => int.TryParse(property?.Name, out _))
                .OrderBy(property => int.Parse(property.Name))
                .Select(property => TryReadOptionalInt(WzInfoTools.GetRealProperty(property)))
                .FirstOrDefault(value => value.HasValue);
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
