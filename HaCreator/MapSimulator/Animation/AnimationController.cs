using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Controls animation playback for entities (Mobs, NPCs, etc.).
    /// Encapsulates frame index tracking, timing, action management, and animation callbacks.
    /// </summary>
    public class AnimationController
    {
        private readonly AnimationSetBase _animationSet;
        private string _currentAction;
        private string _previousAction;
        private List<IDXObject> _currentFrames;
        private int _currentFrameIndex;
        private int _lastFrameSwitchTime;
        private bool _isPlayingOnce;      // Animation plays once then stops
        private bool _animationCompleted; // Animation has completed (for one-shot animations)

        #region Events

        /// <summary>
        /// Called when an animation completes (reaches last frame)
        /// </summary>
        public event Action<string> OnAnimationComplete;

        /// <summary>
        /// Called when the action changes
        /// </summary>
        public event Action<string, string> OnActionChanged; // (previousAction, newAction)

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new AnimationController for an entity
        /// </summary>
        /// <param name="animationSet">The animation set containing all available actions</param>
        /// <param name="initialAction">The initial action to play (default: "stand")</param>
        public AnimationController(AnimationSetBase animationSet, string initialAction = "stand")
        {
            _animationSet = animationSet ?? throw new ArgumentNullException(nameof(animationSet));
            _currentAction = initialAction;
            _previousAction = initialAction;
            _currentFrames = animationSet.GetFrames(initialAction);
            _currentFrameIndex = 0;
            _lastFrameSwitchTime = 0;
            _isPlayingOnce = false;
            _animationCompleted = false;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The current action being played
        /// </summary>
        public string CurrentAction => _currentAction;

        /// <summary>
        /// The previous action (before the current one)
        /// </summary>
        public string PreviousAction => _previousAction;

        /// <summary>
        /// The current frame index within the animation
        /// </summary>
        public int CurrentFrameIndex => _currentFrameIndex;

        /// <summary>
        /// The current animation frames
        /// </summary>
        public List<IDXObject> CurrentFrames => _currentFrames;

        /// <summary>
        /// Total number of frames in the current animation
        /// </summary>
        public int FrameCount => _currentFrames?.Count ?? 0;

        /// <summary>
        /// Whether the current animation has completed (for one-shot animations)
        /// </summary>
        public bool IsAnimationComplete => _animationCompleted;

        /// <summary>
        /// Whether this is a one-shot animation (plays once, doesn't loop)
        /// </summary>
        public bool IsPlayingOnce => _isPlayingOnce;

        /// <summary>
        /// The animation set this controller uses
        /// </summary>
        public AnimationSetBase AnimationSet => _animationSet;

        #endregion

        #region Action Management

        /// <summary>
        /// Set the current animation action (loops continuously)
        /// </summary>
        /// <param name="action">Action name</param>
        /// <returns>True if action was changed, false if already playing or not found</returns>
        public bool SetAction(string action)
        {
            return SetActionInternal(action, playOnce: false);
        }

        /// <summary>
        /// Play an animation once, then stop at the last frame
        /// </summary>
        /// <param name="action">Action name</param>
        /// <returns>True if action was changed, false if already playing or not found</returns>
        public bool PlayOnce(string action)
        {
            return SetActionInternal(action, playOnce: true);
        }

        /// <summary>
        /// Play an animation once, then transition to another action
        /// </summary>
        /// <param name="action">Action to play once</param>
        /// <param name="transitionTo">Action to transition to after completion</param>
        public void PlayOnceThenTransition(string action, string transitionTo)
        {
            if (SetActionInternal(action, playOnce: true))
            {
                // Store transition target (handled in UpdateFrame)
                _transitionAction = transitionTo;
            }
        }

        private string _transitionAction = null;

        private bool SetActionInternal(string action, bool playOnce)
        {
            if (action == _currentAction && !_animationCompleted)
                return false;

            var newFrames = _animationSet?.GetFrames(action);
            if (newFrames == null || newFrames.Count == 0)
                return false;

            _previousAction = _currentAction;
            _currentAction = action;
            _currentFrames = newFrames;
            _currentFrameIndex = 0;
            _lastFrameSwitchTime = 0; // Reset timing
            _isPlayingOnce = playOnce;
            _animationCompleted = false;
            _transitionAction = null;

            OnActionChanged?.Invoke(_previousAction, _currentAction);
            return true;
        }

        /// <summary>
        /// Force reset the animation to its first frame
        /// </summary>
        public void Reset()
        {
            _currentFrameIndex = 0;
            _lastFrameSwitchTime = 0;
            _animationCompleted = false;
        }

        /// <summary>
        /// Revert to the previous action
        /// </summary>
        public void RevertToPreviousAction()
        {
            if (!string.IsNullOrEmpty(_previousAction) && _previousAction != _currentAction)
            {
                SetAction(_previousAction);
            }
        }

        #endregion

        #region Frame Update

        /// <summary>
        /// Update the animation frame based on the current tick count.
        /// Call this every frame to advance the animation.
        /// </summary>
        /// <param name="tickCount">Current Environment.TickCount</param>
        /// <returns>True if the frame changed, false otherwise</returns>
        public bool UpdateFrame(int tickCount)
        {
            if (_currentFrames == null || _currentFrames.Count == 0)
                return false;

            // Initialize timing on first call
            if (_lastFrameSwitchTime == 0)
            {
                _lastFrameSwitchTime = tickCount;
                return false;
            }

            // If animation is complete and it's a one-shot, don't update
            if (_isPlayingOnce && _animationCompleted)
            {
                // Handle transition if one was set
                if (_transitionAction != null)
                {
                    string transition = _transitionAction;
                    _transitionAction = null;
                    SetAction(transition);
                    return true;
                }
                return false;
            }

            // Get frame delay from current frame
            int delay = GetCurrentFrameDelay();
            if (tickCount - _lastFrameSwitchTime < delay)
                return false;

            // Advance to next frame
            _lastFrameSwitchTime = tickCount;
            int previousIndex = _currentFrameIndex;
            _currentFrameIndex++;

            // Check if animation completed
            if (_currentFrameIndex >= _currentFrames.Count)
            {
                if (_isPlayingOnce)
                {
                    // Hold on last frame
                    _currentFrameIndex = _currentFrames.Count - 1;
                    _animationCompleted = true;
                    OnAnimationComplete?.Invoke(_currentAction);

                    // Handle transition if one was set
                    if (_transitionAction != null)
                    {
                        string transition = _transitionAction;
                        _transitionAction = null;
                        SetAction(transition);
                    }
                }
                else
                {
                    // Loop back to start
                    _currentFrameIndex = 0;
                    OnAnimationComplete?.Invoke(_currentAction);
                }
            }

            return _currentFrameIndex != previousIndex;
        }

        /// <summary>
        /// Get the delay for the current frame in milliseconds
        /// </summary>
        private int GetCurrentFrameDelay()
        {
            if (_currentFrames == null || _currentFrameIndex >= _currentFrames.Count)
                return 100; // Default delay

            var frame = _currentFrames[_currentFrameIndex];
            int delay = frame?.Delay ?? 100;
            return Math.Max(delay, 10); // Minimum 10ms delay
        }

        #endregion

        #region Frame Access

        /// <summary>
        /// Get the current animation frame
        /// </summary>
        public IDXObject GetCurrentFrame()
        {
            if (_currentFrames == null || _currentFrames.Count == 0)
                return null;

            int index = Math.Clamp(_currentFrameIndex, 0, _currentFrames.Count - 1);
            return _currentFrames[index];
        }

        /// <summary>
        /// Get a specific frame by index
        /// </summary>
        public IDXObject GetFrame(int index)
        {
            if (_currentFrames == null || _currentFrames.Count == 0)
                return null;

            index = Math.Clamp(index, 0, _currentFrames.Count - 1);
            return _currentFrames[index];
        }

        #endregion
    }
}
