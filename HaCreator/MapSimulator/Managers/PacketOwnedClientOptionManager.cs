using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketOwnedClientOptionManager
    {
        internal readonly struct PendingSetOptRequest
        {
            public PendingSetOptRequest(uint type, int value)
            {
                Type = type;
                Value = value;
            }

            public uint Type { get; }
            public int Value { get; }
        }

        private readonly Dictionary<uint, int> _options = new();
        private readonly List<PendingSetOptRequest> _pendingSetOptRequests = new();

        public IReadOnlyDictionary<uint, int> Snapshot => _options;
        public IReadOnlyList<PendingSetOptRequest> PendingSetOptRequests => _pendingSetOptRequests;

        public void DecodeOpt(IReadOnlyDictionary<uint, int> options)
        {
            _options.Clear();
            _pendingSetOptRequests.Clear();
            if (options == null)
            {
                return;
            }

            foreach (KeyValuePair<uint, int> option in options)
            {
                _options[option.Key] = option.Value;
            }
        }

        public void SetOpt(uint type, int value, bool queuePendingSend = true)
        {
            _options[type] = value;
            if (queuePendingSend)
            {
                QueuePendingSetOptRequest(type, value);
            }
        }

        public void SetOpt(int type, int value, bool queuePendingSend = true)
        {
            if (type < 0)
            {
                return;
            }

            SetOpt((uint)type, value, queuePendingSend);
        }

        public IReadOnlyList<PendingSetOptRequest> DrainPendingSetOptRequests()
        {
            if (_pendingSetOptRequests.Count == 0)
            {
                return Array.Empty<PendingSetOptRequest>();
            }

            PendingSetOptRequest[] drained = _pendingSetOptRequests.ToArray();
            _pendingSetOptRequests.Clear();
            return drained;
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

        private void QueuePendingSetOptRequest(uint type, int value)
        {
            for (int i = 0; i < _pendingSetOptRequests.Count; i++)
            {
                if (_pendingSetOptRequests[i].Type == type)
                {
                    _pendingSetOptRequests[i] = new PendingSetOptRequest(type, value);
                    return;
                }
            }

            _pendingSetOptRequests.Add(new PendingSetOptRequest(type, value));
        }
    }
}
