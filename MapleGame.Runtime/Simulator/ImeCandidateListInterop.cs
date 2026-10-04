using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator
{
    internal static class ImeCandidateListInterop
    {
        internal const int CandidateListCount = 32;

        internal static IEnumerable<int> EnumerateCandidateListIndices(uint mask)
        {
            if (mask == 0)
            {
                yield return 0;
                yield break;
            }

            for (int index = 0; index < CandidateListCount; index++)
            {
                if ((mask & (1u << index)) != 0)
                {
                    yield return index;
                }
            }
        }

        internal static ImeCandidateListState DecodeCandidateList(byte[] buffer, bool vertical, int listIndex, ImeCandidateWindowForm windowForm = null)
        {
            if (buffer == null || buffer.Length < 24)
            {
                return ImeCandidateListState.Empty;
            }

            uint count = BitConverter.ToUInt32(buffer, 8);
            int selection = (int)BitConverter.ToUInt32(buffer, 12);
            int pageStart = (int)BitConverter.ToUInt32(buffer, 16);
            int pageSize = (int)BitConverter.ToUInt32(buffer, 20);

            List<string> candidates = new((int)count);
            int offsetTableStart = 24;
            for (int i = 0; i < count; i++)
            {
                int offsetIndex = offsetTableStart + (i * sizeof(uint));
                if (offsetIndex + sizeof(uint) > buffer.Length)
                {
                    break;
                }

                int stringOffset = (int)BitConverter.ToUInt32(buffer, offsetIndex);
                if (stringOffset < 0 || stringOffset >= buffer.Length)
                {
                    continue;
                }

                int terminatorOffset = stringOffset;
                while (terminatorOffset + 1 < buffer.Length)
                {
                    if (buffer[terminatorOffset] == 0 && buffer[terminatorOffset + 1] == 0)
                    {
                        break;
                    }

                    terminatorOffset += 2;
                }

                int stringByteLength = Math.Max(0, terminatorOffset - stringOffset);
                string candidate = stringByteLength > 0
                    ? Encoding.Unicode.GetString(buffer, stringOffset, stringByteLength)
                    : string.Empty;
                candidates.Add(candidate);
            }

            return candidates.Count > 0
                ? new ImeCandidateListState(candidates, pageStart, pageSize, selection, vertical, listIndex, windowForm)
                : ImeCandidateListState.Empty;
        }
    }
}
