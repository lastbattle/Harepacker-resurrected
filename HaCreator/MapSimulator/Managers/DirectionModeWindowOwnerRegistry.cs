using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Tracks scripted or NPC-owned simulator windows that should hold direction mode
    /// until they are dismissed and the delayed release timer expires.
    /// </summary>
    public sealed class DirectionModeWindowOwnerRegistry
    {
        private readonly HashSet<string> _ownedWindowNames = new(StringComparer.Ordinal);

        public void TrackWindow(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            _ownedWindowNames.Add(windowName);
        }

        public bool IsTracking(string windowName)
        {
            return !string.IsNullOrWhiteSpace(windowName) && _ownedWindowNames.Contains(windowName);
        }

        public bool HasVisibleOwnedWindow(Func<string, bool> isWindowVisible)
        {
            if (isWindowVisible == null)
            {
                throw new ArgumentNullException(nameof(isWindowVisible));
            }

            if (_ownedWindowNames.Count == 0)
            {
                return false;
            }

            string[] ownedWindows = new string[_ownedWindowNames.Count];
            _ownedWindowNames.CopyTo(ownedWindows);

            bool anyVisible = false;
            for (int i = 0; i < ownedWindows.Length; i++)
            {
                string windowName = ownedWindows[i];
                if (isWindowVisible(windowName))
                {
                    anyVisible = true;
                    continue;
                }

                _ownedWindowNames.Remove(windowName);
            }

            return anyVisible;
        }

        public void Reset()
        {
            _ownedWindowNames.Clear();
        }
    }
}
