using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketOwnedClientOptionManager
    {
        private readonly Dictionary<uint, int> _options = new();

        public IReadOnlyDictionary<uint, int> Snapshot => _options;

        public void DecodeOpt(IReadOnlyDictionary<uint, int> options)
        {
            _options.Clear();
            if (options == null)
            {
                return;
            }

            foreach (KeyValuePair<uint, int> option in options)
            {
                _options[option.Key] = option.Value;
            }
        }

        public void SetOpt(uint type, int value)
        {
            _options[type] = value;
        }

        public void SetOpt(int type, int value)
        {
            if (type < 0)
            {
                return;
            }

            SetOpt((uint)type, value);
        }

        public int GetOpt(uint type)
        {
            return _options.TryGetValue(type, out int value)
                ? value
                : 0;
        }

        public int GetOpt(int type)
        {
            return type >= 0
                ? GetOpt((uint)type)
                : 0;
        }

        public bool TryGetOpt(uint type, out int value)
        {
            return _options.TryGetValue(type, out value);
        }

        public bool TryGetOpt(int type, out int value)
        {
            if (type < 0)
            {
                value = 0;
                return false;
            }

            return TryGetOpt((uint)type, out value);
        }
    }
}
