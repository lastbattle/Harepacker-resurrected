using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal static class PacketOwnedPayloadEnvelopeRuntime
    {
        private const int OpcodeEnvelopeSize = sizeof(ushort);
        private const int LengthOpcodeEnvelopeSize = sizeof(ushort) + sizeof(ushort);

        internal readonly record struct Candidate(byte[] Payload, string EnvelopeLabel);

        public static IReadOnlyList<Candidate> EnumerateDecodeCandidates(byte[] payload, ushort expectedOpcode)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<Candidate>();
            }

            List<Candidate> candidates = new()
            {
                new Candidate(payload, "raw payload")
            };

            if (payload.Length > OpcodeEnvelopeSize
                && BitConverter.ToUInt16(payload, 0) == expectedOpcode)
            {
                candidates.Add(new Candidate(payload[OpcodeEnvelopeSize..], "opcode+payload envelope"));
            }

            if (payload.Length > LengthOpcodeEnvelopeSize)
            {
                ushort prefixedLength = BitConverter.ToUInt16(payload, 0);
                ushort prefixedOpcode = BitConverter.ToUInt16(payload, sizeof(ushort));
                if (prefixedOpcode == expectedOpcode)
                {
                    int remainingPayloadLength = payload.Length - LengthOpcodeEnvelopeSize;
                    if (prefixedLength == remainingPayloadLength
                        || prefixedLength == remainingPayloadLength + sizeof(ushort)
                        || prefixedLength == remainingPayloadLength + sizeof(int))
                    {
                        candidates.Add(new Candidate(payload[LengthOpcodeEnvelopeSize..], "length+opcode+payload envelope"));
                    }
                }
            }

            return candidates;
        }
    }
}
