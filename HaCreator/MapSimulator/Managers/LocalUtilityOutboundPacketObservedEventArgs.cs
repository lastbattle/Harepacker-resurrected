using System;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LocalUtilityOutboundPacketObservedEventArgs : EventArgs
    {
        public LocalUtilityOutboundPacketObservedEventArgs(int opcode, byte[] payload, byte[] rawPacket, string source)
        {
            Opcode = opcode;
            Payload = payload ?? Array.Empty<byte>();
            RawPacket = rawPacket ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "official-session:outbound" : source;
        }

        public int Opcode { get; }
        public byte[] Payload { get; }
        public byte[] RawPacket { get; }
        public string Source { get; }
    }
}
