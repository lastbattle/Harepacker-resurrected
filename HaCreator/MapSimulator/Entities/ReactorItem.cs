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
            int activationValue = 0,
            int hitOption = -1)
        {
            ActivationType = activationType;
            ReactorType = reactorType;
            ActivationValue = activationValue;
            HitOption = hitOption;
        }

        public ReactorActivationType ActivationType { get; }

        public ReactorType ReactorType { get; }

        public int ActivationValue { get; }

        public int HitOption { get; }
    }

    public class ReactorItem : BaseDXDrawableItem
    {
        internal readonly struct TransitionSelection
        {
            public TransitionSelection(int targetState, int authoredOrder)
            {
                TargetState = targetState;
                AuthoredOrder = authoredOrder;
            }

            public int TargetState { get; }

            public int AuthoredOrder { get; }

            public bool IsAuthored => AuthoredOrder >= 0;
        }

        private readonly struct AuthoredStateTransition
        {
            public AuthoredStateTransition(int eventType, int targetState, int[] selectorValues, int order)
            {
                EventType = eventType;
                TargetState = targetState;
                SelectorValues = selectorValues ?? Array.Empty<int>();
                Order = order;
            }
            public int EventType { get; }
            public int TargetState { get; }
            public int[] SelectorValues { get; }
            public int Order { get; }
        }

        private readonly ReactorInstance _reactorInstance;
        private readonly Dictionary<int, IDXObject[]> _stateFrames;
        private readonly Dictionary<int, int> _stateHitDurations;
        private readonly Dictionary<(int State, int ProperEventIndex), int> _stateIndexedHitDurations;
        private readonly Dictionary<int, IDXObject[]> _stateHitFrames;
        private readonly Dictionary<(int State, int ProperEventIndex), IDXObject[]> _stateIndexedHitFrames;
        private readonly Dictionary<int, WzImageProperty> _stateLayerProperties;
        private readonly Dictionary<int, WzImageProperty> _stateHitProperties;
        private readonly Dictionary<(int State, int ProperEventIndex), WzImageProperty> _stateIndexedHitProperties;
        private readonly Dictionary<int, bool> _stateRepeatModes;
        private readonly Dictionary<int, int[]> _stateEventTypes;
        private readonly Dictionary<int, AuthoredStateTransition[]> _stateTransitions;
        private readonly Dictionary<WzImageProperty, IDXObject[]> _lazySourceFrameCache;
        private readonly Func<WzImageProperty, List<IDXObject>> _lazySourceFrameLoader;
        private readonly HashSet<int> _authoredStates;
        private readonly int[] _availableStates;
        private readonly int _rootHitDuration;
        private readonly IDXObject[] _rootHitFrames;
        private readonly WzImageProperty _rootHitProperty;
        private readonly int _templateLayerMode;
        private readonly bool _templateMoveEnabled;
        private readonly int _originWorldX;
        private readonly int _originWorldY;
        private int _currentWorldX;
        private int _currentWorldY;
        private int _activeState;
        private int _activeFrameIndex;
        private int _lastStateTick;
        private IDXObject[] _transientFrames;
        private WzImageProperty _transientHitLayerSourceProperty;
        private int _transientHitLayerSourceState = -1;
        private int _transientHitLayerProperEventIndex = -1;
        private int _transientFrameIndex;
        private int _lastTransientTick;
        private int _lastVisibilityCheckFrame = -1;

        private enum HitAnimationSourceKind
        {
            None,
            StateLayer,
            IndexedHit,
            StateHit,
            RootHit
        }

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
            _stateHitDurations = new Dictionary<int, int>();
            _stateIndexedHitDurations = new Dictionary<(int State, int ProperEventIndex), int>();
            _stateHitFrames = new Dictionary<int, IDXObject[]>();
            _stateIndexedHitFrames = new Dictionary<(int State, int ProperEventIndex), IDXObject[]>();
            _stateLayerProperties = new Dictionary<int, WzImageProperty>();
            _stateHitProperties = new Dictionary<int, WzImageProperty>();
            _stateIndexedHitProperties = new Dictionary<(int State, int ProperEventIndex), WzImageProperty>();
            _stateRepeatModes = new Dictionary<int, bool>();
            _stateEventTypes = new Dictionary<int, int[]>();
            _stateTransitions = new Dictionary<int, AuthoredStateTransition[]>();
            _lazySourceFrameCache = new Dictionary<WzImageProperty, IDXObject[]>();
            _lazySourceFrameLoader = null;
            _authoredStates = new HashSet<int>();
            _availableStates = Array.Empty<int>();
            _rootHitDuration = 0;
            _rootHitFrames = Array.Empty<IDXObject>();
            _rootHitProperty = null;
            _templateLayerMode = 0;
            _templateMoveEnabled = false;
            _originWorldX = reactorInstance?.X ?? 0;
            _originWorldY = reactorInstance?.Y ?? 0;
            _currentWorldX = _originWorldX;
            _currentWorldY = _originWorldY;
        }

        public ReactorItem(
            ReactorInstance reactorInstance,
            Dictionary<int, List<IDXObject>> stateFrames,
            Dictionary<int, List<IDXObject>> stateHitFrames = null,
            Dictionary<(int State, int ProperEventIndex), List<IDXObject>> stateIndexedHitFrames = null,
            List<IDXObject> rootHitFrames = null,
            Dictionary<int, WzImageProperty> stateLayerProperties = null,
            Dictionary<int, WzImageProperty> stateHitProperties = null,
            Dictionary<(int State, int ProperEventIndex), WzImageProperty> stateIndexedHitProperties = null,
            WzImageProperty rootHitProperty = null,
            Func<WzImageProperty, List<IDXObject>> lazySourceFrameLoader = null)
            : base(GetDefaultFrames(stateFrames), reactorInstance.Flip)
        {
            _reactorInstance = reactorInstance;
            _stateFrames = new Dictionary<int, IDXObject[]>();
            _stateHitDurations = LoadStateHitDurations(reactorInstance);
            _stateIndexedHitDurations = LoadStateIndexedHitDurations(reactorInstance);
            _stateHitFrames = ToFrameArrayDictionary(stateHitFrames);
            _stateIndexedHitFrames = ToFrameArrayDictionary(stateIndexedHitFrames);
            _stateLayerProperties = stateLayerProperties != null
                ? new Dictionary<int, WzImageProperty>(stateLayerProperties)
                : new Dictionary<int, WzImageProperty>();
            _stateHitProperties = stateHitProperties != null
                ? new Dictionary<int, WzImageProperty>(stateHitProperties)
                : new Dictionary<int, WzImageProperty>();
            _stateIndexedHitProperties = stateIndexedHitProperties != null
                ? new Dictionary<(int State, int ProperEventIndex), WzImageProperty>(stateIndexedHitProperties)
                : new Dictionary<(int State, int ProperEventIndex), WzImageProperty>();
            _stateRepeatModes = LoadStateRepeatModes(reactorInstance);
            _stateEventTypes = LoadStateEventTypes(reactorInstance);
            _stateTransitions = LoadStateTransitions(reactorInstance);
            _lazySourceFrameCache = new Dictionary<WzImageProperty, IDXObject[]>();
            _lazySourceFrameLoader = lazySourceFrameLoader;
            _authoredStates = LoadAuthoredStates(reactorInstance);
            _rootHitDuration = LoadRootHitDuration(reactorInstance);
            _rootHitFrames = rootHitFrames?.Count > 0 ? rootHitFrames.ToArray() : Array.Empty<IDXObject>();
            _rootHitProperty = rootHitProperty;
            _templateLayerMode = LoadTemplateLayerMode(reactorInstance);
            _templateMoveEnabled = LoadTemplateMoveEnabled(reactorInstance);
            _originWorldX = reactorInstance?.X ?? 0;
            _originWorldY = reactorInstance?.Y ?? 0;
            _currentWorldX = _originWorldX;
            _currentWorldY = _originWorldY;

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

            _availableStates = BuildAvailableStates(_stateFrames.Keys, _stateLayerProperties.Keys);
            _activeState = ResolveInitialState();
        }

        public ReactorItem(ReactorInstance reactorInstance, IDXObject frame0)
            : base(frame0, reactorInstance.Flip)
        {
            _reactorInstance = reactorInstance;
            _stateFrames = new Dictionary<int, IDXObject[]>();
            _stateHitDurations = new Dictionary<int, int>();
            _stateIndexedHitDurations = new Dictionary<(int State, int ProperEventIndex), int>();
            _stateHitFrames = new Dictionary<int, IDXObject[]>();
            _stateIndexedHitFrames = new Dictionary<(int State, int ProperEventIndex), IDXObject[]>();
            _stateLayerProperties = new Dictionary<int, WzImageProperty>();
            _stateHitProperties = new Dictionary<int, WzImageProperty>();
            _stateIndexedHitProperties = new Dictionary<(int State, int ProperEventIndex), WzImageProperty>();
            _stateRepeatModes = new Dictionary<int, bool>();
            _stateEventTypes = new Dictionary<int, int[]>();
            _stateTransitions = new Dictionary<int, AuthoredStateTransition[]>();
            _lazySourceFrameCache = new Dictionary<WzImageProperty, IDXObject[]>();
            _lazySourceFrameLoader = null;
            _authoredStates = new HashSet<int>();
            _availableStates = Array.Empty<int>();
            _rootHitDuration = 0;
            _rootHitFrames = Array.Empty<IDXObject>();
            _rootHitProperty = null;
            _templateLayerMode = 0;
            _templateMoveEnabled = false;
            _originWorldX = reactorInstance?.X ?? 0;
            _originWorldY = reactorInstance?.Y ?? 0;
            _currentWorldX = _originWorldX;
            _currentWorldY = _originWorldY;
        }

        public int RenderSortKey { get; set; }

        public int TemplateLayerMode => _templateLayerMode;

        public bool TemplateMoveEnabled => _templateMoveEnabled;

        public int CurrentWorldX => _currentWorldX;

        public int CurrentWorldY => _currentWorldY;

        public float LayerAlpha { get; set; } = 1f;

        internal WzImageProperty TransientHitLayerSourceProperty => _transientHitLayerSourceProperty;

        internal int TransientHitLayerSourceState => _transientHitLayerSourceState;

        internal int TransientHitLayerProperEventIndex => _transientHitLayerProperEventIndex;

        public void SetAnimationState(int state, int tickCount, bool restartIfSameState = false)
        {
            int resolvedState = ResolveState(state);
            if (resolvedState == _activeState && !restartIfSameState)
                return;

            ClearTransientAnimation();
            _activeState = resolvedState;
            _activeFrameIndex = 0;
            _lastStateTick = tickCount;
        }

        public int GetInitialState()
        {
            return ResolveInitialState();
        }

        public void SetWorldPosition(int x, int y, bool persistInstance = true)
        {
            if (persistInstance && _reactorInstance != null)
            {
                _reactorInstance.X = x;
                _reactorInstance.Y = y;
            }

            _currentWorldX = x;
            _currentWorldY = y;
            Position = new Point(_originWorldX - x, _originWorldY - y);
        }

        public void SetFlipState(bool isFlipped)
        {
            flip = isFlipped;
            if (_reactorInstance != null)
            {
                _reactorInstance.Flip = isFlipped;
            }
        }

        public bool HasAuthoredEventInfo(int state)
        {
            int resolvedState = ResolveState(state);
            return _stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                && transitions.Length > 0;
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

        internal bool TryResolveNextState(
            int currentState,
            ReactorTransitionRequest request,
            out TransitionSelection selection,
            bool includeNumericFallback = true,
            int preferredAuthoredOrder = -1)
        {
            int resolvedState = ResolveState(currentState);
            AuthoredStateTransition[] authoredCandidates = GetAuthoredTransitions(
                resolvedState,
                request,
                preferredAuthoredOrder);
            if (authoredCandidates.Length > 0)
            {
                AuthoredStateTransition authoredTransition = authoredCandidates[0];
                selection = new TransitionSelection(authoredTransition.TargetState, authoredTransition.Order);
                return true;
            }

            if (!includeNumericFallback || _availableStates.Length == 0)
            {
                selection = default;
                return false;
            }

            for (int i = 0; i < _availableStates.Length; i++)
            {
                if (_availableStates[i] > resolvedState)
                {
                    selection = new TransitionSelection(_availableStates[i], authoredOrder: -1);
                    return true;
                }
            }

            selection = default;
            return false;
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

        public int GetRemainingAnimationDuration(int tickCount)
        {
            IDXObject[] frames = GetActiveFrameSet();
            if (frames == null || frames.Length == 0)
            {
                return 0;
            }

            GetStateFrame(tickCount, out int frameIndex);
            int elapsedWithinFrame = Math.Max(0, tickCount - (_transientFrames != null ? _lastTransientTick : _lastStateTick));
            int remainingDuration = Math.Max(0, Math.Max(1, frames[frameIndex].Delay) - elapsedWithinFrame);

            for (int i = frameIndex + 1; i < frames.Length; i++)
            {
                remainingDuration += Math.Max(1, frames[i].Delay);
            }

            return remainingDuration;
        }

        public int GetRemainingStoppedAnimationDuration(int tickCount)
        {
            return _transientFrames == null && IsStateRepeat(_activeState)
                ? 0
                : GetRemainingAnimationDuration(tickCount);
        }

        public int GetHitAnimationDuration(int state)
        {
            int resolvedState = ResolveState(state);
            return _stateHitDurations.TryGetValue(resolvedState, out int duration)
                ? duration
                : 0;
        }

        public bool TryGetHitAnimationDuration(int state, int properEventIndex, out int duration)
        {
            if (TryResolveHitAnimationDuration(state, properEventIndex, out duration))
            {
                return true;
            }

            duration = 0;
            return false;
        }

        public bool TryStartHitAnimation(int state, int properEventIndex, int tickCount, out int duration)
        {
            if (!TryResolveHitAnimation(
                state,
                properEventIndex,
                out IDXObject[] frames,
                out duration,
                out _,
                out WzImageProperty sourceProperty))
            {
                return false;
            }

            _transientHitLayerSourceProperty = sourceProperty;
            _transientHitLayerSourceState = ResolveState(state);
            _transientHitLayerProperEventIndex = properEventIndex;

            if (frames != null && frames.Length > 0)
            {
                _transientFrames = frames;
                _transientFrameIndex = 0;
                _lastTransientTick = tickCount;
            }

            return duration > 0;
        }

        public void ClearTransientAnimation()
        {
            _transientFrames = null;
            _transientHitLayerSourceProperty = null;
            _transientHitLayerSourceState = -1;
            _transientHitLayerProperEventIndex = -1;
            _transientFrameIndex = 0;
            _lastTransientTick = 0;
        }

        public bool IsStateRepeat(int state)
        {
            int resolvedState = ResolveState(state);
            return !_stateRepeatModes.TryGetValue(resolvedState, out bool repeat) || repeat;
        }

        internal bool TryResolveAutoHitEventIndex(int currentState, int hitOption, ReactorType reactorType, out int eventIndex)
        {
            int resolvedState = ResolveState(currentState);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                eventIndex = -1;
                return false;
            }

            if (TryResolveClientHitEventIndex(
                transitions.Select(static transition => transition.EventType),
                hitOption,
                reactorType,
                out eventIndex))
            {
                return true;
            }

            transitions = GetAuthoredTransitions(
                resolvedState,
                new ReactorTransitionRequest(ReactorActivationType.Hit, reactorType));
            if (transitions.Length == 0)
            {
                eventIndex = -1;
                return false;
            }

            eventIndex = transitions[0].Order;
            return true;
        }

        internal bool TryResolveAutoHitEventIndex(int currentState, ReactorType reactorType, out int eventIndex)
        {
            return TryResolveAutoHitEventIndex(currentState, hitOption: -1, reactorType, out eventIndex);
        }

        internal bool HasAuthoredEventIndex(int currentState, int properEventIndex)
        {
            int resolvedState = ResolveState(currentState);
            return properEventIndex >= 0
                && _stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                && properEventIndex < transitions.Length;
        }

        internal IReadOnlyList<int> GetAuthoredEventTypes(int currentState)
        {
            int resolvedState = ResolveState(currentState);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return Array.Empty<int>();
            }

            return transitions
                .Select(static transition => transition.EventType)
                .ToArray();
        }

        private bool TryResolveHitAnimationDuration(int state, int properEventIndex, out int duration)
        {
            return TryResolveHitAnimation(state, properEventIndex, out _, out duration);
        }

        internal bool TryResolveHitAnimationSourceProperty(int state, int properEventIndex, out WzImageProperty property)
        {
            return TryResolveHitAnimationSource(state, properEventIndex, out _, out property);
        }

        private bool TryResolveHitAnimation(int state, int properEventIndex, out IDXObject[] frames, out int duration)
        {
            return TryResolveHitAnimation(
                state,
                properEventIndex,
                out frames,
                out duration,
                out _,
                out _);
        }

        private bool TryResolveHitAnimation(
            int state,
            int properEventIndex,
            out IDXObject[] frames,
            out int duration,
            out HitAnimationSourceKind sourceKind,
            out WzImageProperty sourceProperty)
        {
            if (!TryResolveHitAnimationSource(state, properEventIndex, out sourceKind, out sourceProperty))
            {
                frames = Array.Empty<IDXObject>();
                duration = 0;
                sourceKind = HitAnimationSourceKind.None;
                sourceProperty = null;
                return false;
            }

            frames = ResolveHitAnimationFrames(sourceKind, state, properEventIndex, sourceProperty);
            duration = ResolveHitAnimationDuration(sourceKind, state, properEventIndex, sourceProperty);
            return duration > 0;
        }

        private bool TryResolveHitAnimationSource(
            int state,
            int properEventIndex,
            out HitAnimationSourceKind sourceKind,
            out WzImageProperty sourceProperty)
        {
            int resolvedState = ResolveState(state);
            sourceKind = HitAnimationSourceKind.None;
            sourceProperty = null;

            if (properEventIndex < 0)
            {
                if (_stateLayerProperties.TryGetValue(resolvedState, out sourceProperty) && sourceProperty != null)
                {
                    sourceKind = HitAnimationSourceKind.StateLayer;
                    return true;
                }

                return TryResolveRootHitSource(out sourceKind, out sourceProperty);
            }

            if (!HasAuthoredState(resolvedState)
                || !HasAuthoredEventIndex(resolvedState, properEventIndex))
            {
                return TryResolveRootHitSource(out sourceKind, out sourceProperty);
            }

            if (_stateIndexedHitProperties.TryGetValue((resolvedState, properEventIndex), out sourceProperty) && sourceProperty != null)
            {
                sourceKind = HitAnimationSourceKind.IndexedHit;
                return true;
            }

            if (TryResolveStateHitSource(resolvedState, out sourceKind, out sourceProperty))
            {
                return true;
            }

            return TryResolveRootHitSource(out sourceKind, out sourceProperty);
        }

        private bool TryResolveStateHitSource(int state, out HitAnimationSourceKind sourceKind, out WzImageProperty sourceProperty)
        {
            if (_stateHitProperties.TryGetValue(state, out sourceProperty) && sourceProperty != null)
            {
                sourceKind = HitAnimationSourceKind.StateHit;
                return true;
            }

            sourceKind = HitAnimationSourceKind.None;
            sourceProperty = null;
            return false;
        }

        private bool TryResolveRootHitSource(out HitAnimationSourceKind sourceKind, out WzImageProperty sourceProperty)
        {
            sourceProperty = _rootHitProperty;
            if (sourceProperty != null)
            {
                sourceKind = HitAnimationSourceKind.RootHit;
                return true;
            }

            sourceKind = HitAnimationSourceKind.None;
            return false;
        }

        private IDXObject[] ResolveHitAnimationFrames(
            HitAnimationSourceKind sourceKind,
            int state,
            int properEventIndex,
            WzImageProperty sourceProperty)
        {
            IDXObject[] frames = sourceKind switch
            {
                HitAnimationSourceKind.StateLayer => _stateFrames.TryGetValue(state, out IDXObject[] stateFrames)
                    ? stateFrames
                    : Array.Empty<IDXObject>(),
                HitAnimationSourceKind.IndexedHit => _stateIndexedHitFrames.TryGetValue((state, properEventIndex), out IDXObject[] indexedFrames)
                    ? indexedFrames
                    : Array.Empty<IDXObject>(),
                HitAnimationSourceKind.StateHit => _stateHitFrames.TryGetValue(state, out IDXObject[] stateHitFrames)
                    ? stateHitFrames
                    : Array.Empty<IDXObject>(),
                HitAnimationSourceKind.RootHit => _rootHitFrames,
                _ => Array.Empty<IDXObject>()
            };

            if (frames.Length > 0 || sourceProperty == null)
            {
                return frames;
            }

            return TryLoadFramesFromSourceProperty(sourceProperty);
        }

        private IDXObject[] TryLoadFramesFromSourceProperty(WzImageProperty sourceProperty)
        {
            if (sourceProperty == null)
            {
                return Array.Empty<IDXObject>();
            }

            if (_lazySourceFrameCache.TryGetValue(sourceProperty, out IDXObject[] cachedFrames))
            {
                return cachedFrames;
            }

            IDXObject[] loadedFrames = _lazySourceFrameLoader?.Invoke(sourceProperty)?.ToArray()
                ?? Array.Empty<IDXObject>();
            _lazySourceFrameCache[sourceProperty] = loadedFrames;
            return loadedFrames;
        }

        private IDXObject[] EnsureStateFramesLoaded(int state)
        {
            int resolvedState = ResolveState(state);
            if (_stateFrames.TryGetValue(resolvedState, out IDXObject[] cachedFrames) && cachedFrames.Length > 0)
            {
                return cachedFrames;
            }

            if (!_stateLayerProperties.TryGetValue(resolvedState, out WzImageProperty sourceProperty) || sourceProperty == null)
            {
                return Array.Empty<IDXObject>();
            }

            IDXObject[] loadedFrames = TryLoadFramesFromSourceProperty(sourceProperty);
            if (loadedFrames.Length > 0)
            {
                _stateFrames[resolvedState] = loadedFrames;
            }

            return loadedFrames;
        }

        private int ResolveHitAnimationDuration(
            HitAnimationSourceKind sourceKind,
            int state,
            int properEventIndex,
            WzImageProperty sourceProperty)
        {
            int duration = sourceKind switch
            {
                HitAnimationSourceKind.StateLayer => GetExactStateDuration(state),
                HitAnimationSourceKind.IndexedHit => _stateIndexedHitDurations.TryGetValue((state, properEventIndex), out int indexedDuration)
                    ? indexedDuration
                    : 0,
                HitAnimationSourceKind.StateHit => _stateHitDurations.TryGetValue(state, out int stateHitDuration)
                    ? stateHitDuration
                    : 0,
                HitAnimationSourceKind.RootHit => _rootHitDuration,
                _ => 0
            };

            return duration > 0
                ? duration
                : ResolveApproximateLoadLayerDuration(sourceKind, sourceProperty);
        }

        internal static int ResolveApproximateLoadLayerDurationForTesting(
            WzImageProperty sourceProperty,
            bool treatAsStateOrEventBranch)
        {
            return ResolveApproximateLoadLayerDuration(
                treatAsStateOrEventBranch ? HitAnimationSourceKind.StateLayer : HitAnimationSourceKind.StateHit,
                sourceProperty);
        }

        private static int ResolveApproximateLoadLayerDuration(
            HitAnimationSourceKind sourceKind,
            WzImageProperty sourceProperty)
        {
            int duration = TryReadHitDuration(sourceProperty);
            if (duration > 0 || sourceProperty == null)
            {
                return duration;
            }

            return TryReadHitDuration(WzInfoTools.GetRealProperty(sourceProperty)?["hit"]);
        }

        private bool HasAuthoredState(int state)
        {
            return _authoredStates.Contains(state);
        }

        private bool HasRenderableState(int state)
        {
            int resolvedState = ResolveState(state);
            return _stateFrames.ContainsKey(resolvedState)
                || _stateLayerProperties.ContainsKey(resolvedState);
        }

        private int GetExactStateDuration(int state)
        {
            IDXObject[] frames = EnsureStateFramesLoaded(state);
            if (frames.Length == 0)
            {
                return 0;
            }

            int totalDuration = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            return totalDuration;
        }

        private bool TryGetRootHitAnimationDuration(out int duration)
        {
            duration = _rootHitDuration;
            return duration > 0;
        }

        internal static bool TryResolveClientHitEventIndex(
            IEnumerable<int> eventTypes,
            int hitOption,
            ReactorType reactorType,
            out int eventIndex)
        {
            eventIndex = -1;
            if (eventTypes == null)
            {
                return false;
            }

            bool found = false;
            int bestPriority = int.MaxValue;
            int index = 0;
            foreach (int eventType in eventTypes)
            {
                int? priority = ResolveClientHitTypePriority(hitOption, eventType)
                    ?? TryResolveClientHitEventPriority(eventType, reactorType);
                if (priority.HasValue && priority.Value >= 0 && priority.Value < bestPriority)
                {
                    bestPriority = priority.Value;
                    eventIndex = index;
                    found = true;
                    if (bestPriority == 0)
                    {
                        break;
                    }
                }

                index++;
            }

            return found;
        }

        internal static bool TryResolveClientHitEventIndex(IEnumerable<int> eventTypes, ReactorType reactorType, out int eventIndex)
        {
            return TryResolveClientHitEventIndex(eventTypes, hitOption: -1, reactorType, out eventIndex);
        }

        public Rectangle GetCurrentBounds(int tickCount)
        {
            IDXObject frame = _stateFrames.Count > 0
                ? GetStateFrame(tickCount, out _)
                : LastFrameDrawn ?? Frame0;

            if (frame == null)
            {
                int fallbackX = _currentWorldX;
                int fallbackY = _currentWorldY;
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

        public new void UpdateVisibility(
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int viewWidth,
            int viewHeight,
            int frameNumber)
        {
            if (_lastVisibilityCheckFrame == frameNumber)
            {
                return;
            }

            _lastVisibilityCheckFrame = frameNumber;

            IDXObject frame = GetVisibilityFrame();
            if (frame == null)
            {
                SetVisible(false);
                return;
            }

            int shiftCenteredX = mapShiftX - centerX;
            int shiftCenteredY = mapShiftY - centerY;
            SetVisible(IsFrameWithinView(
                frame,
                shiftCenteredX - Position.X,
                shiftCenteredY - Position.Y,
                viewWidth,
                viewHeight));
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            float layerAlpha = NormalizeLayerAlpha(LayerAlpha);
            if (layerAlpha <= 0f)
            {
                return;
            }

            if (_stateFrames.Count > 0)
            {
                IDXObject drawFrame = GetStateFrame(TickCount);
                if (drawFrame == null)
                    return;

                int shiftCenteredX = mapShiftX - centerX;
                int shiftCenteredY = mapShiftY - centerY;
                if (!IsFrameWithinView(
                    drawFrame,
                    shiftCenteredX - Position.X,
                    shiftCenteredY - Position.Y,
                    renderParameters.RenderWidth,
                    renderParameters.RenderHeight))
                    return;

                DrawLayerFrame(drawFrame, sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX - Position.X, shiftCenteredY - Position.Y,
                    layerAlpha,
                    flip,
                    drawReflectionInfo);
                return;
            }

            if (layerAlpha < 1f)
            {
                IDXObject drawFrame = LastFrameDrawn ?? Frame0;
                if (drawFrame == null)
                {
                    return;
                }

                int shiftCenteredX = mapShiftX - centerX;
                int shiftCenteredY = mapShiftY - centerY;
                if (!IsFrameWithinView(
                    drawFrame,
                    shiftCenteredX - Position.X,
                    shiftCenteredY - Position.Y,
                    renderParameters.RenderWidth,
                    renderParameters.RenderHeight))
                    return;

                DrawLayerFrame(drawFrame, sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX - Position.X, shiftCenteredY - Position.Y,
                    layerAlpha,
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

        internal static float NormalizeLayerAlphaForTesting(float layerAlpha)
        {
            return NormalizeLayerAlpha(layerAlpha);
        }

        private static float NormalizeLayerAlpha(float layerAlpha)
        {
            return MathHelper.Clamp(layerAlpha, 0f, 1f);
        }

        private static void DrawLayerFrame(
            IDXObject frame,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int shiftCenteredX,
            int shiftCenteredY,
            float layerAlpha,
            bool flip,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (layerAlpha >= 1f)
            {
                frame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX, shiftCenteredY,
                    flip,
                    drawReflectionInfo);
                return;
            }

            frame.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                frame.X - shiftCenteredX,
                frame.Y - shiftCenteredY,
                Color.White * layerAlpha,
                flip,
                drawReflectionInfo);
        }

        private IDXObject GetStateFrame(int tickCount)
        {
            return GetStateFrame(tickCount, out _);
        }

        private IDXObject GetVisibilityFrame()
        {
            if (_transientFrames != null && _transientFrames.Length > 0)
            {
                int frameIndex = Math.Clamp(_transientFrameIndex, 0, _transientFrames.Length - 1);
                return _transientFrames[frameIndex];
            }

            if (_stateFrames.TryGetValue(_activeState, out IDXObject[] frames) && frames.Length > 0)
            {
                int frameIndex = Math.Clamp(_activeFrameIndex, 0, frames.Length - 1);
                return frames[frameIndex];
            }

            return LastFrameDrawn ?? Frame0;
        }

        private IDXObject GetStateFrame(int tickCount, out int frameIndex)
        {
            if (_transientFrames != null && _transientFrames.Length > 0)
            {
                return GetAnimationFrame(
                    _transientFrames,
                    ref _transientFrameIndex,
                    ref _lastTransientTick,
                    repeat: false,
                    tickCount,
                    out frameIndex);
            }

            IDXObject[] frames = ResolveStateFrames(_activeState);
            if (frames == null || frames.Length == 0)
            {
                frameIndex = 0;
                return null;
            }

            return GetAnimationFrame(
                frames,
                ref _activeFrameIndex,
                ref _lastStateTick,
                IsStateRepeat(_activeState),
                tickCount,
                out frameIndex);
        }

        private IDXObject[] GetActiveFrameSet()
        {
            return _transientFrames != null && _transientFrames.Length > 0
                ? _transientFrames
                : ResolveStateFrames(_activeState);
        }

        private IDXObject[] ResolveStateFrames(int state)
        {
            int resolvedState = ResolveState(state);
            IDXObject[] frames = EnsureStateFramesLoaded(resolvedState);
            if (frames.Length > 0)
            {
                return frames;
            }

            if (_stateFrames.TryGetValue(0, out IDXObject[] fallbackFrames) && fallbackFrames.Length > 0)
            {
                return fallbackFrames;
            }

            if (_stateLayerProperties.TryGetValue(0, out WzImageProperty fallbackSourceProperty) && fallbackSourceProperty != null)
            {
                fallbackFrames = TryLoadFramesFromSourceProperty(fallbackSourceProperty);
                if (fallbackFrames.Length > 0)
                {
                    _stateFrames[0] = fallbackFrames;
                    return fallbackFrames;
                }
            }

            return null;
        }

        private static IDXObject GetAnimationFrame(
            IDXObject[] frames,
            ref int activeFrameIndex,
            ref int lastFrameTick,
            bool repeat,
            int tickCount,
            out int frameIndex)
        {
            if (frames == null || frames.Length == 0)
            {
                frameIndex = 0;
                return null;
            }

            if (activeFrameIndex >= frames.Length)
            {
                activeFrameIndex = 0;
            }

            if (frames.Length > 1)
            {
                int elapsed = tickCount - lastFrameTick;
                while (elapsed > 0)
                {
                    int currentDelay = Math.Max(1, frames[activeFrameIndex].Delay);
                    if (elapsed < currentDelay)
                    {
                        break;
                    }

                    elapsed -= currentDelay;
                    if (!repeat && activeFrameIndex >= frames.Length - 1)
                    {
                        lastFrameTick = tickCount - elapsed;
                        activeFrameIndex = frames.Length - 1;
                        break;
                    }

                    activeFrameIndex = (activeFrameIndex + 1) % frames.Length;
                    lastFrameTick += currentDelay;
                }
            }

            frameIndex = activeFrameIndex;
            return frames[activeFrameIndex];
        }

        private int ResolveState(int state)
        {
            if (_availableStates.Length == 0 || _availableStates.Contains(state))
                return state;

            int lowerOrEqualState = _availableStates
                .Where(key => key <= state)
                .DefaultIfEmpty(int.MinValue)
                .Max();
            if (lowerOrEqualState != int.MinValue)
                return lowerOrEqualState;

            return _availableStates[0];
        }

        private int ResolveInitialState()
        {
            if (_availableStates.Length == 0)
                return 0;

            return _stateFrames.ContainsKey(0) ? 0 : _availableStates[0];
        }

        private static Dictionary<TKey, IDXObject[]> ToFrameArrayDictionary<TKey>(Dictionary<TKey, List<IDXObject>> framesByKey)
        {
            Dictionary<TKey, IDXObject[]> result = new Dictionary<TKey, IDXObject[]>();
            if (framesByKey == null)
            {
                return result;
            }

            foreach (KeyValuePair<TKey, List<IDXObject>> kvp in framesByKey)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    result[kvp.Key] = kvp.Value.ToArray();
                }
            }

            return result;
        }

        private static int[] BuildAvailableStates(IEnumerable<int> frameStates, IEnumerable<int> sourceStates)
        {
            return frameStates
                .Concat(sourceStates ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(state => state)
                .ToArray();
        }

        private int[] GetAuthoredCandidates(int resolvedState, ReactorTransitionRequest request)
        {
            return GetAuthoredTransitions(resolvedState, request)
                .Select(transition => transition.TargetState)
                .Distinct()
                .ToArray();
        }

        private AuthoredStateTransition[] GetAuthoredTransitions(
            int resolvedState,
            ReactorTransitionRequest request,
            int preferredAuthoredOrder = -1)
        {
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return Array.Empty<AuthoredStateTransition>();
            }

            AuthoredStateTransition[] filteredTransitions = FilterTransitionsBySelector(
                transitions.Where(transition => MatchesAuthoredEventType(transition.EventType, request.ActivationType)).ToArray(),
                request)
                .Where(transition => HasRenderableState(transition.TargetState))
                .Where(transition => transition.TargetState != resolvedState)
                .OrderBy(transition => GetEventTypePriority(transition.EventType, request))
                .ThenBy(transition => GetSelectorPriority(transition, request))
                .ThenBy(transition => GetSelectorLookaheadPriority(transition, request))
                .ThenBy(transition => GetHitPriorityLookaheadPriority(transition, request))
                .ThenBy(transition => GetSameTypeDescendantLookaheadPriority(transition, request))
                .ThenBy(transition => GetAuthoredGraphContinuationLookaheadPriority(transition, request))
                .ThenBy(transition => transition.Order)
                .ToArray();

            if (preferredAuthoredOrder < 0)
            {
                return filteredTransitions;
            }

            AuthoredStateTransition[] preferredOrderTransitions = filteredTransitions
                .Where(transition => transition.Order == preferredAuthoredOrder)
                .ToArray();
            return preferredOrderTransitions.Length > 0
                ? preferredOrderTransitions
                : filteredTransitions;
        }

        private static AuthoredStateTransition[] FilterTransitionsBySelector(
            AuthoredStateTransition[] transitions,
            ReactorTransitionRequest request)
        {
            if (transitions == null || transitions.Length == 0)
            {
                return Array.Empty<AuthoredStateTransition>();
            }

            bool isSelectorDrivenActivation = request.ActivationType == ReactorActivationType.Item
                || request.ActivationType == ReactorActivationType.Skill
                || request.ActivationType == ReactorActivationType.Quest;

            if (!isSelectorDrivenActivation)
            {
                return transitions;
            }

            if (request.ActivationValue <= 0)
            {
                AuthoredStateTransition[] genericTransitions = transitions
                    .Where(transition => transition.SelectorValues.Length == 0)
                    .ToArray();

                if (genericTransitions.Length > 0)
                {
                    return genericTransitions;
                }

                return ShouldRejectSelectorDrivenTransitionWithoutSelector(request.ActivationType)
                    ? Array.Empty<AuthoredStateTransition>()
                    : transitions;
            }

            AuthoredStateTransition[] filteredTransitions = transitions
                .Where(transition => transition.SelectorValues.Length == 0 || transition.SelectorValues.Contains(request.ActivationValue))
                .ToArray();

            return filteredTransitions.Length > 0
                ? filteredTransitions
                : Array.Empty<AuthoredStateTransition>();
        }

        private static int GetSelectorPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationValue <= 0)
            {
                return transition.SelectorValues.Length == 0 ? 0 : 1;
            }

            if (transition.SelectorValues.Contains(request.ActivationValue))
            {
                return 0;
            }

            return transition.SelectorValues.Length == 0 ? 1 : 2;
        }

        private int GetSelectorLookaheadPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationValue <= 0
                || !IsSelectorDrivenActivationType(request.ActivationType)
                || transition.SelectorValues.Contains(request.ActivationValue)
                || transition.SelectorValues.Length != 0)
            {
                return 0;
            }

            return HasDescendantSelectorMatch(
                transition.TargetState,
                request,
                new HashSet<int> { ResolveState(transition.TargetState) })
                ? 0
                : 1;
        }

        private bool HasDescendantSelectorMatch(
            int state,
            ReactorTransitionRequest request,
            HashSet<int> visitedStates)
        {
            if (request.ActivationValue <= 0
                || !IsSelectorDrivenActivationType(request.ActivationType))
            {
                return false;
            }

            int resolvedState = ResolveState(state);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return false;
            }

            AuthoredStateTransition[] matchingTransitions = transitions
                .Where(transition => MatchesAuthoredEventType(transition.EventType, request.ActivationType))
                .Where(transition => transition.TargetState != resolvedState)
                .Where(transition => HasRenderableState(transition.TargetState))
                .ToArray();
            if (matchingTransitions.Any(transition => transition.SelectorValues.Contains(request.ActivationValue)))
            {
                return true;
            }

            foreach (AuthoredStateTransition transition in matchingTransitions.Where(transition => transition.SelectorValues.Length == 0))
            {
                int nextState = ResolveState(transition.TargetState);
                if (!visitedStates.Add(nextState))
                {
                    continue;
                }

                if (HasDescendantSelectorMatch(nextState, request, visitedStates))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetHitPriorityLookaheadPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationType != ReactorActivationType.Hit)
            {
                return 0;
            }

            int resolvedTargetState = ResolveState(transition.TargetState);
            int? priority = TryResolveDescendantHitPriority(
                resolvedTargetState,
                request,
                new HashSet<int> { resolvedTargetState });
            return priority ?? int.MaxValue;
        }

        private int? TryResolveDescendantHitPriority(
            int state,
            ReactorTransitionRequest request,
            HashSet<int> visitedStates)
        {
            int resolvedState = ResolveState(state);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return null;
            }

            int? bestPriority = null;
            foreach (AuthoredStateTransition transition in transitions)
            {
                if (!MatchesAuthoredEventType(transition.EventType, ReactorActivationType.Hit)
                    || transition.TargetState == resolvedState
                    || !HasRenderableState(transition.TargetState))
                {
                    continue;
                }

                int? transitionPriority = TryResolveClientHitEventPriority(transition.EventType, request);
                if (transitionPriority.HasValue
                    && (!bestPriority.HasValue || transitionPriority.Value < bestPriority.Value))
                {
                    bestPriority = transitionPriority.Value;
                }

                int nextState = ResolveState(transition.TargetState);
                if (!visitedStates.Add(nextState))
                {
                    continue;
                }

                int? descendantPriority = TryResolveDescendantHitPriority(nextState, request, visitedStates);
                if (descendantPriority.HasValue
                    && (!bestPriority.HasValue || descendantPriority.Value < bestPriority.Value))
                {
                    bestPriority = descendantPriority.Value;
                }
            }

            return bestPriority;
        }

        private int GetSameTypeDescendantLookaheadPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationType == ReactorActivationType.None
                || request.ActivationType == ReactorActivationType.Quest
                || request.ActivationType == ReactorActivationType.Hit)
            {
                return 0;
            }

            int resolvedTargetState = ResolveState(transition.TargetState);
            return HasDescendantActivationTypeMatch(
                resolvedTargetState,
                request.ActivationType,
                new HashSet<int> { resolvedTargetState })
                ? 0
                : 1;
        }

        private bool HasDescendantActivationTypeMatch(
            int state,
            ReactorActivationType activationType,
            HashSet<int> visitedStates)
        {
            int resolvedState = ResolveState(state);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return false;
            }

            foreach (AuthoredStateTransition transition in transitions)
            {
                if (!MatchesAuthoredEventType(transition.EventType, activationType)
                    || transition.TargetState == resolvedState
                    || !HasRenderableState(transition.TargetState))
                {
                    continue;
                }

                return true;
            }

            foreach (AuthoredStateTransition transition in transitions.Where(static transition => transition.SelectorValues.Length == 0))
            {
                int nextState = ResolveState(transition.TargetState);
                if (!visitedStates.Add(nextState))
                {
                    continue;
                }

                if (HasDescendantActivationTypeMatch(nextState, activationType, visitedStates))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetAuthoredGraphContinuationLookaheadPriority(AuthoredStateTransition transition, ReactorTransitionRequest request)
        {
            if (request.ActivationType == ReactorActivationType.None
                || request.ActivationType == ReactorActivationType.Quest)
            {
                return 0;
            }

            int resolvedTargetState = ResolveState(transition.TargetState);
            int continuationDepth = ResolveAuthoredGraphContinuationDepth(
                resolvedTargetState,
                new HashSet<int> { resolvedTargetState });
            return continuationDepth > 0
                ? -Math.Min(continuationDepth, 32)
                : 0;
        }

        private int ResolveAuthoredGraphContinuationDepth(int state, HashSet<int> visitedStates)
        {
            int resolvedState = ResolveState(state);
            if (!_stateTransitions.TryGetValue(resolvedState, out AuthoredStateTransition[] transitions)
                || transitions.Length == 0)
            {
                return 0;
            }

            int bestDepth = 0;
            foreach (AuthoredStateTransition transition in transitions)
            {
                if (transition.SelectorValues.Length != 0
                    || transition.TargetState == resolvedState
                    || !HasRenderableState(transition.TargetState))
                {
                    continue;
                }

                int nextState = ResolveState(transition.TargetState);
                if (!visitedStates.Add(nextState))
                {
                    continue;
                }

                int depth = 1 + ResolveAuthoredGraphContinuationDepth(nextState, visitedStates);
                if (depth > bestDepth)
                {
                    bestDepth = depth;
                }

                visitedStates.Remove(nextState);
            }

            return bestDepth;
        }

        internal static bool ShouldRejectSelectorDrivenTransitionWithoutSelector(ReactorActivationType activationType)
        {
            return activationType == ReactorActivationType.Item
                || activationType == ReactorActivationType.Skill
                || activationType == ReactorActivationType.Quest;
        }

        private static bool IsSelectorDrivenActivationType(ReactorActivationType activationType)
        {
            return activationType == ReactorActivationType.Item
                || activationType == ReactorActivationType.Skill
                || activationType == ReactorActivationType.Quest;
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
                ReactorActivationType.Hit => GetHitEventPriority(eventType, request),
                ReactorActivationType.Time => eventType switch
                {
                    101 => 0,
                    7 => 1,
                    _ => 2
                },
                _ => 0
            };
        }

        private static int GetHitEventPriority(int eventType, ReactorTransitionRequest request)
        {
            int? clientPriority = TryResolveClientHitEventPriority(eventType, request);
            if (clientPriority.HasValue)
            {
                return clientPriority.Value;
            }

            return request.ReactorType switch
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

        private static int? TryResolveClientHitEventPriority(int eventType, ReactorTransitionRequest request)
        {
            if (request.HitOption >= 0)
            {
                int? hitOptionPriority = ResolveClientHitTypePriority(request.HitOption, eventType);
                if (hitOptionPriority.HasValue)
                {
                    return hitOptionPriority.Value;
                }
            }

            return TryResolveClientHitEventPriority(eventType, request.ReactorType);
        }

        private static int? TryResolveClientHitEventPriority(int eventType, ReactorType reactorType)
        {
            int? hitOption = reactorType switch
            {
                ReactorType.ActivatedRightHit => 1,
                ReactorType.ActivatedLeftHit or ReactorType.ActivatedByAnyHit => 0,
                _ => null
            };
            if (!hitOption.HasValue)
            {
                return null;
            }

            return ResolveClientHitTypePriority(hitOption.Value, eventType);
        }

        internal static int? ResolveClientHitTypePriority(int hitOption, int eventType)
        {
            int directionBit = hitOption & 1;
            if ((hitOption & 2) != 0)
            {
                return eventType switch
                {
                    0 => 1,
                    1 => directionBit != 0 ? -1 : 0,
                    2 => directionBit != 0 ? 0 : -1,
                    _ => null
                };
            }

            return eventType switch
            {
                0 => 2,
                1 => directionBit == 0 ? 1 : -1,
                2 => directionBit != 0 ? 1 : -1,
                3 => directionBit != 0 ? -1 : 0,
                4 => directionBit != 0 ? 0 : -1,
                _ => null
            };
        }

        private static bool MatchesAuthoredEventType(int eventType, ReactorActivationType activationType)
        {
            return activationType switch
            {
                ReactorActivationType.Touch or ReactorActivationType.Quest => eventType is 0 or 6 or 100,
                ReactorActivationType.Hit => eventType is 0 or 1 or 2 or 3 or 4 or 8,
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

        private static Dictionary<int, int[]> LoadStateEventTypes(ReactorInstance reactorInstance)
        {
            var eventTypes = new Dictionary<int, int[]>();

            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return eventTypes;
            }

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out int stateId))
                {
                    continue;
                }

                if (WzInfoTools.GetRealProperty(property)?["event"] is not WzSubProperty eventProperty)
                {
                    continue;
                }

                int[] stateEventTypes = eventProperty.WzProperties
                    .OfType<WzSubProperty>()
                    .Select(eventNode => TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["type"])))
                    .Where(static eventType => eventType.HasValue)
                    .Select(static eventType => eventType.Value)
                    .ToArray();
                if (stateEventTypes.Length > 0)
                {
                    eventTypes[stateId] = stateEventTypes;
                }
            }

            return eventTypes;
        }

        private static HashSet<int> LoadAuthoredStates(ReactorInstance reactorInstance)
        {
            var states = new HashSet<int>();

            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return states;
            }

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (int.TryParse(property?.Name, out int stateId))
                {
                    states.Add(stateId);
                }
            }

            return states;
        }

        private static Dictionary<int, int> LoadStateHitDurations(ReactorInstance reactorInstance)
        {
            var hitDurations = new Dictionary<int, int>();

            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return hitDurations;
            }

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out int stateId))
                {
                    continue;
                }

                int duration = TryReadHitDuration(WzInfoTools.GetRealProperty(property)?["hit"]);
                if (duration > 0)
                {
                    hitDurations[stateId] = duration;
                }
            }

            return hitDurations;
        }

        private static int LoadRootHitDuration(ReactorInstance reactorInstance)
        {
            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return 0;
            }

            return TryReadHitDuration(WzInfoTools.GetRealProperty(linkedImage["hit"]));
        }

        private static Dictionary<(int State, int ProperEventIndex), int> LoadStateIndexedHitDurations(ReactorInstance reactorInstance)
        {
            return LoadStateIndexedHitDurations(reactorInstance?.ReactorInfo?.LinkedWzImage);
        }

        internal static Dictionary<(int State, int ProperEventIndex), int> LoadStateIndexedHitDurationsForTesting(WzImage linkedImage)
        {
            return LoadStateIndexedHitDurations(linkedImage);
        }

        private static Dictionary<(int State, int ProperEventIndex), int> LoadStateIndexedHitDurations(WzImage linkedImage)
        {
            var hitDurations = new Dictionary<(int State, int ProperEventIndex), int>();
            if (linkedImage == null)
            {
                return hitDurations;
            }

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out int stateId))
                {
                    continue;
                }

                WzImageProperty realStateProperty = WzInfoTools.GetRealProperty(property);
                if (realStateProperty?.WzProperties == null)
                {
                    continue;
                }

                foreach ((int properEventIndex, WzImageProperty eventProperty) in EnumerateIndexedHitProperties(realStateProperty))
                {
                    int duration = TryReadIndexedHitDuration(eventProperty);
                    if (duration > 0)
                    {
                        hitDurations[(stateId, properEventIndex)] = duration;
                    }
                }
            }

            return hitDurations;
        }

        private static IEnumerable<(int ProperEventIndex, WzImageProperty EventProperty)> EnumerateIndexedHitProperties(WzImageProperty stateProperty)
        {
            WzImageProperty realStateProperty = WzInfoTools.GetRealProperty(stateProperty);
            if (realStateProperty?.WzProperties == null)
            {
                yield break;
            }

            HashSet<int> yieldedIndices = new HashSet<int>();
            if (WzInfoTools.GetRealProperty(realStateProperty["event"]) is WzSubProperty eventProperty)
            {
                foreach (WzImageProperty child in eventProperty.WzProperties)
                {
                    if (int.TryParse(child?.Name, out int properEventIndex)
                        && yieldedIndices.Add(properEventIndex))
                    {
                        yield return (properEventIndex, child);
                    }
                }
            }

            foreach (WzImageProperty child in realStateProperty.WzProperties)
            {
                if (int.TryParse(child?.Name, out int properEventIndex)
                    && WzInfoTools.GetRealProperty(child) is WzSubProperty
                    && yieldedIndices.Add(properEventIndex))
                {
                    yield return (properEventIndex, child);
                }
            }
        }

        private static int TryReadIndexedHitDuration(WzImageProperty property)
        {
            int duration = TryReadHitDuration(property);
            if (duration > 0 || property == null)
            {
                return duration;
            }

            return TryReadHitDuration(WzInfoTools.GetRealProperty(property)?["hit"]);
        }

        private static Dictionary<int, bool> LoadStateRepeatModes(ReactorInstance reactorInstance)
        {
            var repeatModes = new Dictionary<int, bool>();

            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return repeatModes;
            }

            foreach (WzImageProperty property in linkedImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out int stateId))
                {
                    continue;
                }

                WzImageProperty realStateProperty = WzInfoTools.GetRealProperty(property);
                int? repeatValue =
                    TryReadOptionalInt(WzInfoTools.GetRealProperty(realStateProperty?["repeat"])) ??
                    TryReadOptionalInt(WzInfoTools.GetRealProperty(realStateProperty?["info"]?["repeat"]));
                if (repeatValue.HasValue)
                {
                    repeatModes[stateId] = repeatValue.Value != 0;
                }
            }

            return repeatModes;
        }

        private static int TryReadHitDuration(WzImageProperty property)
        {
            WzImageProperty realProperty = WzInfoTools.GetRealProperty(property);
            if (realProperty == null)
            {
                return 0;
            }

            if (realProperty is WzCanvasProperty canvasProperty)
            {
                return Math.Max(1, TryReadOptionalInt(WzInfoTools.GetRealProperty(canvasProperty["delay"])) ?? 0);
            }

            if (realProperty is not WzSubProperty hitProperty)
            {
                return 0;
            }

            int totalDuration = 0;
            foreach (WzImageProperty frameProperty in hitProperty.WzProperties.Where(child => int.TryParse(child?.Name, out _)))
            {
                WzImageProperty resolvedFrameProperty = WzInfoTools.GetRealProperty(frameProperty);
                if (resolvedFrameProperty is WzCanvasProperty frameCanvas)
                {
                    totalDuration += Math.Max(1, TryReadOptionalInt(WzInfoTools.GetRealProperty(frameCanvas["delay"])) ?? 0);
                }
            }

            if (totalDuration > 0)
            {
                return totalDuration;
            }

            WzImageProperty nestedDefaultFrames = WzInfoTools.GetRealProperty(hitProperty["0"]);
            return !ReferenceEquals(nestedDefaultFrames, hitProperty)
                ? TryReadHitDuration(nestedDefaultFrames)
                : 0;
        }

        private static int LoadTemplateLayerMode(ReactorInstance reactorInstance)
        {
            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return 0;
            }

            return TryReadOptionalInt(WzInfoTools.GetRealProperty(linkedImage["info"])?["layer"]) ?? 0;
        }

        private static bool LoadTemplateMoveEnabled(ReactorInstance reactorInstance)
        {
            WzImage linkedImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedImage == null)
            {
                return false;
            }

            return (TryReadOptionalInt(WzInfoTools.GetRealProperty(linkedImage["info"])?["move"]) ?? 0) != 0;
        }

        private static AuthoredStateTransition? TryCreateTransition(WzSubProperty eventNode, int order)
        {
            int? eventType = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["type"]));
            int? targetState = TryReadOptionalInt(WzInfoTools.GetRealProperty(eventNode?["state"]));
            if (!eventType.HasValue || !targetState.HasValue)
                return null;

            int[] selectorValues = TryReadSelectorValues(eventNode);
            return new AuthoredStateTransition(eventType.Value, targetState.Value, selectorValues, order);
        }

        private static int[] TryReadSelectorValues(WzSubProperty eventNode)
        {
            if (eventNode == null)
            {
                return Array.Empty<int>();
            }

            // Some reactor event nodes encode multiple item/skill selectors under the same branch.
            // Preserve the whole authored set so dispatch can match any of them.
            IEnumerable<int> namedSelectorValues = EnumerateNamedSelectorValues(eventNode);

            IEnumerable<int> indexedSelectorValues = eventNode.WzProperties
                .Where(property => int.TryParse(property?.Name, out _))
                .OrderBy(property => int.Parse(property.Name))
                .SelectMany(property => ReadIndexedSelectorValues(WzInfoTools.GetRealProperty(property)));

            return namedSelectorValues
                .Concat(indexedSelectorValues)
                .Distinct()
                .ToArray();
        }

        private static IEnumerable<int> ReadIndexedSelectorValues(WzImageProperty property)
        {
            foreach (int directValue in EnumerateSelectorValues(property))
            {
                yield return directValue;
            }

            if (property is not WzSubProperty selectorNode)
            {
                yield break;
            }

            foreach (int selectorValue in ReadNamedSelectorValues(selectorNode))
            {
                yield return selectorValue;
            }
        }

        private static IEnumerable<int> ReadNamedSelectorValues(WzSubProperty eventNode)
        {
            foreach (int selectorValue in EnumerateNamedSelectorValues(eventNode))
            {
                yield return selectorValue;
            }
        }

        private static IEnumerable<int> EnumerateNamedSelectorValues(WzSubProperty eventNode)
        {
            if (eventNode == null)
            {
                yield break;
            }

            string[] selectorNames =
            {
                "id",
                "quest",
                "questID",
                "questid",
                "reqQuest",
                "item",
                "itemID",
                "itemid",
                "skill",
                "skillID",
                "skillid"
            };

            for (int i = 0; i < selectorNames.Length; i++)
            {
                foreach (int selectorValue in EnumerateSelectorValues(WzInfoTools.GetRealProperty(eventNode[selectorNames[i]])))
                {
                    yield return selectorValue;
                }
            }
        }

        private static IEnumerable<int> EnumerateSelectorValues(WzImageProperty property)
        {
            int? directValue = TryReadOptionalInt(property);
            if (directValue.HasValue)
            {
                yield return directValue.Value;
                yield break;
            }

            if (property is not WzStringProperty stringProperty)
            {
                yield break;
            }

            foreach (int parsedValue in ParseDelimitedSelectorValues(stringProperty.Value))
            {
                yield return parsedValue;
            }
        }

        private static IEnumerable<int> ParseDelimitedSelectorValues(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                yield break;
            }

            int tokenStart = -1;
            for (int i = 0; i <= rawValue.Length; i++)
            {
                char currentChar = i < rawValue.Length ? rawValue[i] : '\0';
                bool tokenChar = char.IsDigit(currentChar)
                    || (currentChar == '-' && tokenStart < 0 && i + 1 < rawValue.Length && char.IsDigit(rawValue[i + 1]));

                if (tokenChar)
                {
                    tokenStart = tokenStart >= 0 ? tokenStart : i;
                    continue;
                }

                if (tokenStart < 0)
                {
                    continue;
                }

                if (int.TryParse(rawValue.Substring(tokenStart, i - tokenStart), out int parsedValue))
                {
                    yield return parsedValue;
                }

                tokenStart = -1;
            }
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
