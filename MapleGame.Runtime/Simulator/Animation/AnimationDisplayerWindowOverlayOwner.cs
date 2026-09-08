using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Animation
{
    internal enum AnimationDisplayerWindowOverlayPass
    {
        Underlay = 0,
        Overlay = 1
    }

    internal sealed class AnimationDisplayerWindowOverlayOwner
    {
        private readonly List<Registration> _registrations = new();

        public int RegistrationCount => _registrations.Count;

        public void Clear()
        {
            _registrations.Clear();
        }

        public void ClearWindow(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            _registrations.RemoveAll(registration =>
                string.Equals(registration.WindowName, windowName, StringComparison.Ordinal));
        }

        public void RemoveTag(string windowName, string tag)
        {
            if (string.IsNullOrWhiteSpace(windowName) || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            _registrations.RemoveAll(registration =>
                string.Equals(registration.WindowName, windowName, StringComparison.Ordinal) &&
                string.Equals(registration.Tag, tag, StringComparison.Ordinal));
        }

        public void RegisterOneTime(
            string windowName,
            string tag,
            List<IDXObject> frames,
            Point anchorOffset,
            AnimationDisplayerWindowOverlayPass pass,
            int currentTimeMs,
            int startDelayMs = 0)
        {
            Register(windowName, tag, frames, anchorOffset, pass, currentTimeMs, startDelayMs, repeat: false, durationMs: 0);
        }

        public void RegisterRepeat(
            string windowName,
            string tag,
            List<IDXObject> frames,
            Point anchorOffset,
            AnimationDisplayerWindowOverlayPass pass,
            int currentTimeMs,
            int durationMs,
            int startDelayMs = 0)
        {
            Register(windowName, tag, frames, anchorOffset, pass, currentTimeMs, startDelayMs, repeat: true, durationMs);
        }

        public void DrawWindow(
            string windowName,
            AnimationDisplayerWindowOverlayPass pass,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            GameTime gameTime,
            Point windowPosition,
            int currentTimeMs)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            for (int i = _registrations.Count - 1; i >= 0; i--)
            {
                Registration registration = _registrations[i];
                if (!registration.Update(currentTimeMs))
                {
                    _registrations.RemoveAt(i);
                    continue;
                }

                if (!string.Equals(registration.WindowName, windowName, StringComparison.Ordinal) || registration.Pass != pass)
                {
                    continue;
                }

                registration.Draw(spriteBatch, skeletonRenderer, gameTime, windowPosition);
            }
        }

        private void Register(
            string windowName,
            string tag,
            List<IDXObject> frames,
            Point anchorOffset,
            AnimationDisplayerWindowOverlayPass pass,
            int currentTimeMs,
            int startDelayMs,
            bool repeat,
            int durationMs)
        {
            if (string.IsNullOrWhiteSpace(windowName) || frames == null || frames.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                RemoveTag(windowName, tag);
            }

            var registration = new Registration();
            registration.Initialize(windowName, tag, frames, anchorOffset, pass, currentTimeMs, Math.Max(0, startDelayMs), repeat, durationMs);
            _registrations.Add(registration);
        }

        private sealed class Registration
        {
            private List<IDXObject> _frames;
            private int _currentFrameIndex;
            private int _lastFrameTimeMs;
            private int _startTimeMs;
            private bool _repeat;
            private int _durationMs;

            public string WindowName { get; private set; }
            public string Tag { get; private set; }
            public Point AnchorOffset { get; private set; }
            public AnimationDisplayerWindowOverlayPass Pass { get; private set; }

            public void Initialize(
                string windowName,
                string tag,
                List<IDXObject> frames,
                Point anchorOffset,
                AnimationDisplayerWindowOverlayPass pass,
                int currentTimeMs,
                int startDelayMs,
                bool repeat,
                int durationMs)
            {
                WindowName = windowName;
                Tag = tag ?? string.Empty;
                _frames = frames;
                AnchorOffset = anchorOffset;
                Pass = pass;
                _currentFrameIndex = 0;
                _lastFrameTimeMs = currentTimeMs + startDelayMs;
                _startTimeMs = currentTimeMs + startDelayMs;
                _repeat = repeat;
                _durationMs = Math.Max(0, durationMs);
            }

            public bool Update(int currentTimeMs)
            {
                if (_frames == null || _frames.Count == 0)
                {
                    return false;
                }

                if (currentTimeMs < _startTimeMs)
                {
                    return true;
                }

                if (_repeat && _durationMs > 0 && currentTimeMs - _startTimeMs >= _durationMs)
                {
                    return false;
                }

                while (true)
                {
                    IDXObject currentFrame = _frames[Math.Clamp(_currentFrameIndex, 0, _frames.Count - 1)];
                    int delayMs = Math.Max(1, currentFrame.Delay);
                    if (currentTimeMs - _lastFrameTimeMs < delayMs)
                    {
                        break;
                    }

                    _lastFrameTimeMs += delayMs;
                    _currentFrameIndex++;
                    if (_currentFrameIndex < _frames.Count)
                    {
                        continue;
                    }

                    if (_repeat)
                    {
                        _currentFrameIndex = 0;
                        continue;
                    }

                    return false;
                }

                return true;
            }

            public void Draw(
                SpriteBatch spriteBatch,
                SkeletonMeshRenderer skeletonRenderer,
                GameTime gameTime,
                Point windowPosition)
            {
                if (_frames == null || _frames.Count == 0)
                {
                    return;
                }

                if (_currentFrameIndex < 0 || _currentFrameIndex >= _frames.Count)
                {
                    return;
                }

                IDXObject frame = _frames[_currentFrameIndex];
                frame?.DrawBackground(
                    spriteBatch,
                    skeletonRenderer,
                    gameTime,
                    windowPosition.X + AnchorOffset.X,
                    windowPosition.Y + AnchorOffset.Y,
                    Color.White,
                    false,
                    null);
            }
        }
    }
}
