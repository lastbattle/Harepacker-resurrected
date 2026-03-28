using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Effects
{
    internal sealed class PacketFieldFadeOverlay
    {
        private int _fadeInMs;
        private int _holdMs;
        private int _fadeOutMs;
        private int _styleCode;
        private int _layerZ;
        private int _startedAt;
        private bool _active;

        public bool IsActive => _active;
        public int RequestedLayerZ => _layerZ;
        public int StyleCode => _styleCode;
        public int FadeInMs => _fadeInMs;
        public int HoldMs => _holdMs;
        public int FadeOutMs => _fadeOutMs;
        public int StartedAt => _startedAt;
        public int ExpiresAt => _active ? _startedAt + _fadeInMs + _holdMs + _fadeOutMs : int.MinValue;

        public void Start(int fadeInMs, int holdMs, int fadeOutMs, int styleCode, int layerZ, int currentTickCount)
        {
            _fadeInMs = Math.Max(0, fadeInMs);
            _holdMs = Math.Max(0, holdMs);
            _fadeOutMs = Math.Max(0, fadeOutMs);
            _styleCode = styleCode;
            _layerZ = layerZ;
            _startedAt = currentTickCount;
            _active = _fadeInMs > 0 || _holdMs > 0 || _fadeOutMs > 0;
        }

        public void Clear()
        {
            _active = false;
            _fadeInMs = 0;
            _holdMs = 0;
            _fadeOutMs = 0;
            _styleCode = 0;
            _layerZ = 0;
            _startedAt = 0;
        }

        public void Update(int currentTickCount)
        {
            if (!_active)
            {
                return;
            }

            int elapsed = Math.Max(0, unchecked(currentTickCount - _startedAt));
            int totalDuration = _fadeInMs + _holdMs + _fadeOutMs;
            if (elapsed > totalDuration)
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

            color = ResolveBaseColor() * alpha;
            return true;
        }

        private float GetAlpha(int currentTickCount)
        {
            int elapsed = Math.Max(0, unchecked(currentTickCount - _startedAt));
            if (_fadeInMs > 0 && elapsed < _fadeInMs)
            {
                return MathHelper.Clamp((float)elapsed / _fadeInMs, 0f, 1f);
            }

            elapsed -= _fadeInMs;
            if (elapsed < _holdMs)
            {
                return 1f;
            }

            elapsed -= _holdMs;
            if (_fadeOutMs > 0 && elapsed < _fadeOutMs)
            {
                return 1f - MathHelper.Clamp((float)elapsed / _fadeOutMs, 0f, 1f);
            }

            return 0f;
        }

        private Color ResolveBaseColor()
        {
            if (_styleCode == 1)
            {
                return Color.White;
            }

            if ((_styleCode & unchecked((int)0xFF000000)) != 0)
            {
                byte a = (byte)((_styleCode >> 24) & 0xFF);
                byte r = (byte)((_styleCode >> 16) & 0xFF);
                byte g = (byte)((_styleCode >> 8) & 0xFF);
                byte b = (byte)(_styleCode & 0xFF);
                return new Color(r, g, b, a == 0 ? byte.MaxValue : a);
            }

            if (_styleCode > 1 && _styleCode <= 0xFFFFFF)
            {
                byte r = (byte)((_styleCode >> 16) & 0xFF);
                byte g = (byte)((_styleCode >> 8) & 0xFF);
                byte b = (byte)(_styleCode & 0xFF);
                return new Color(r, g, b);
            }

            return Color.Black;
        }
    }
}
