using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Effects
{
    internal sealed class PacketFieldFadeOverlay
    {
        private int _fadeInMs;
        private int _holdMs;
        private int _fadeOutMs;
        private int _startingAlpha;
        private int _layerZ;
        private int _startedAt;
        private bool _active;

        public bool IsActive => _active;
        public int RequestedLayerZ => _layerZ;
        public int StartingAlpha => _startingAlpha;
        public int FadeInMs => _fadeInMs;
        public int HoldMs => _holdMs;
        public int FadeOutMs => _fadeOutMs;
        public int StartedAt => _startedAt;
        public int FadeOutStartsAt => _active ? _startedAt + _fadeInMs + _holdMs : int.MinValue;
        public int ExpiresAt => _active ? _startedAt + _fadeInMs + _holdMs + _fadeOutMs : int.MinValue;

        public void Start(int fadeInMs, int holdMs, int fadeOutMs, int startingAlpha, int layerZ, int currentTickCount)
        {
            _fadeInMs = Math.Max(0, fadeInMs);
            _holdMs = Math.Max(0, holdMs);
            _fadeOutMs = Math.Max(0, fadeOutMs);
            _startingAlpha = Math.Clamp(startingAlpha, 0, byte.MaxValue);
            _layerZ = layerZ;
            _startedAt = currentTickCount;
            _active = _fadeInMs + _holdMs + _fadeOutMs > 0;
        }

        public void Clear()
        {
            _active = false;
            _fadeInMs = 0;
            _holdMs = 0;
            _fadeOutMs = 0;
            _startingAlpha = 0;
            _layerZ = 0;
            _startedAt = 0;
        }

        public void Update(int currentTickCount)
        {
            if (!_active)
            {
                return;
            }

            if (unchecked(currentTickCount - ExpiresAt) >= 0)
            {
                Clear();
            }
        }

        public bool TryGetOverlay(int currentTickCount, out Color color)
        {
            color = Color.Transparent;
            if (!_active)
            {
                return false;
            }

            float alpha = GetAlpha(currentTickCount);
            if (alpha <= 0f)
            {
                return false;
            }

            color = Color.Black * alpha;
            return true;
        }

        private float GetAlpha(int currentTickCount)
        {
            int elapsed = Math.Max(0, unchecked(currentTickCount - _startedAt));
            float startingAlpha = _startingAlpha / (float)byte.MaxValue;
            if (_fadeInMs > 0 && elapsed < _fadeInMs)
            {
                float fadeProgress = MathHelper.Clamp((float)elapsed / _fadeInMs, 0f, 1f);
                return MathHelper.Lerp(startingAlpha, 1f, fadeProgress);
            }

            int fadeOutStartsAt = FadeOutStartsAt;
            if (unchecked(currentTickCount - fadeOutStartsAt) < 0)
            {
                return 1f;
            }

            if (_fadeOutMs > 0)
            {
                elapsed = Math.Max(0, unchecked(currentTickCount - fadeOutStartsAt));
                if (elapsed < _fadeOutMs)
                {
                    return 1f - MathHelper.Clamp((float)elapsed / _fadeOutMs, 0f, 1f);
                }
            }

            return 0f;
        }
    }
}
