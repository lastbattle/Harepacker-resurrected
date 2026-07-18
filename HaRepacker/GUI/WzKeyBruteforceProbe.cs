using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib.Util;

namespace HaRepacker.GUI
{
    /// <summary>
    /// Preloads a short encrypted image name and uses its ".img" suffix as a fast IV filter.
    /// Full WZ parsing remains the final authority for the rare matching candidate.
    /// </summary>
    internal sealed class WzKeyBruteforceProbe
    {
        internal const int CandidateBatchSize = 16_384;

        private const int AesBlockSize = 16;
        private static readonly byte[] ExpectedImageSuffix = ".img"u8.ToArray();

        private readonly string _wzPath;
        private readonly byte[] _aesKey;
        private readonly KeyByteConstraint[] _constraints;

        internal WzKeyBruteforceProbe(string wzPath)
        {
            _wzPath = wzPath ?? throw new ArgumentNullException(nameof(wzPath));
            _constraints = ReadImageSuffixConstraints(wzPath);

            byte[] userKey = MapleCryptoConstants.UserKey_WzLib;
            _aesKey = MapleCryptoConstants.GetTrimmedUserKey(ref userKey);
        }

        internal Worker CreateWorker() => new Worker(this);

        internal bool TryCandidate(uint candidate)
        {
            using Worker worker = CreateWorker();
            return worker.MatchesSignature(candidate) && VerifyCandidate(candidate);
        }

        private bool VerifyCandidate(uint candidate)
        {
            byte[] key = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(key, candidate);
            return WzTool.TryBruteforcingWzIVKey(_wzPath, key);
        }

        private bool MatchesSignature(ReadOnlySpan<byte> keyStream)
        {
            foreach (KeyByteConstraint constraint in _constraints)
            {
                if (keyStream[constraint.Position] != constraint.ExpectedValue)
                    return false;
            }

            return true;
        }

        private static KeyByteConstraint[] ReadImageSuffixConstraints(string wzPath)
        {
            using FileStream stream = File.Open(wzPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new BinaryReader(stream);

            if (stream.Length < 20 || new string(reader.ReadChars(4)) != "PKG1")
                throw new InvalidDataException("The selected file does not have a valid WZ header.");

            reader.ReadUInt64();
            uint fileStart = reader.ReadUInt32();
            if (fileStart >= stream.Length - 1)
                throw new InvalidDataException("The selected WZ file has an invalid data offset.");

            bool hasEncryptedVersionHeader = HasEncryptedVersionHeader(reader, fileStart);
            stream.Position = fileStart + (hasEncryptedVersionHeader ? sizeof(ushort) : 0);

            int entryCount = ReadCompressedInt(reader);
            if (entryCount <= 0 || entryCount > 100_000)
                throw new InvalidDataException("The selected WZ file has no usable root directory entries.");

            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                byte type = reader.ReadByte();
                if (type == 1)
                {
                    stream.Seek(sizeof(int) + sizeof(short) + sizeof(int), SeekOrigin.Current);
                    continue;
                }

                long resumePosition;
                RawWzString rawName;
                switch (type)
                {
                    case 2:
                        int stringOffset = reader.ReadInt32();
                        resumePosition = stream.Position;
                        stream.Position = checked(fileStart + stringOffset + (hasEncryptedVersionHeader ? 0 : 1));
                        type = reader.ReadByte();
                        rawName = ReadRawWzString(reader);
                        break;

                    case 3:
                    case 4:
                        rawName = ReadRawWzString(reader);
                        resumePosition = stream.Position;
                        break;

                    default:
                        throw new InvalidDataException($"Unsupported WZ root entry type {type}.");
                }

                stream.Position = resumePosition;
                ReadCompressedInt(reader);
                ReadCompressedInt(reader);
                stream.Seek(sizeof(int), SeekOrigin.Current);

                if (type == 4 && TryCreateImageSuffixConstraints(rawName, out KeyByteConstraint[] constraints))
                    return constraints;
            }

            throw new InvalidDataException(
                "The selected WZ file does not contain a short root image name usable for fast key testing. " +
                "Use TamingMob.wz from a classic client.");
        }

        private static bool HasEncryptedVersionHeader(BinaryReader reader, uint fileStart)
        {
            reader.BaseStream.Position = fileStart;
            ushort value = reader.ReadUInt16();
            if (value > byte.MaxValue)
                return false;

            if (value == 0x80 && reader.BaseStream.Length - fileStart >= sizeof(int))
            {
                reader.BaseStream.Position = fileStart;
                int possibleEntryCount = reader.ReadInt32();
                if (possibleEntryCount > 0 && (possibleEntryCount & 0xFF) == 0 && possibleEntryCount <= ushort.MaxValue)
                    return false;
            }

            return true;
        }

        private static RawWzString ReadRawWzString(BinaryReader reader)
        {
            sbyte smallLength = reader.ReadSByte();
            bool unicode = smallLength > 0;
            int characterCount = unicode
                ? (smallLength == sbyte.MaxValue ? reader.ReadInt32() : smallLength)
                : (smallLength == sbyte.MinValue ? reader.ReadInt32() : -smallLength);

            if (characterCount <= 0 || characterCount > 1_000_000)
                throw new InvalidDataException("The selected WZ file contains an invalid root entry name length.");

            int byteCount = checked(characterCount * (unicode ? sizeof(char) : sizeof(byte)));
            if (reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
                throw new EndOfStreamException("The selected WZ file ends inside a root entry name.");

            byte[] encryptedBytes = reader.ReadBytes(byteCount);
            return new RawWzString(unicode, characterCount, encryptedBytes);
        }

        private static bool TryCreateImageSuffixConstraints(
            RawWzString rawName,
            out KeyByteConstraint[] constraints)
        {
            int suffixStart = rawName.CharacterCount - ExpectedImageSuffix.Length;
            int highestKeyIndex = rawName.IsUnicode
                ? ((rawName.CharacterCount - 1) * sizeof(char)) + 1
                : rawName.CharacterCount - 1;

            if (suffixStart < 0 || highestKeyIndex >= AesBlockSize)
            {
                constraints = Array.Empty<KeyByteConstraint>();
                return false;
            }

            List<KeyByteConstraint> result = new List<KeyByteConstraint>(rawName.IsUnicode ? 8 : 4);
            for (int suffixIndex = 0; suffixIndex < ExpectedImageSuffix.Length; suffixIndex++)
            {
                int characterIndex = suffixStart + suffixIndex;
                byte expectedCharacter = ExpectedImageSuffix[suffixIndex];

                if (rawName.IsUnicode)
                {
                    int byteIndex = characterIndex * sizeof(char);
                    ushort mask = (ushort)(0xAAAA + characterIndex);
                    result.Add(new KeyByteConstraint(
                        byteIndex,
                        (byte)(rawName.EncryptedBytes[byteIndex] ^ (byte)mask ^ expectedCharacter)));
                    result.Add(new KeyByteConstraint(
                        byteIndex + 1,
                        (byte)(rawName.EncryptedBytes[byteIndex + 1] ^ (byte)(mask >> 8))));
                }
                else
                {
                    result.Add(new KeyByteConstraint(
                        characterIndex,
                        (byte)(rawName.EncryptedBytes[characterIndex] ^ (byte)(0xAA + characterIndex) ^ expectedCharacter)));
                }
            }

            constraints = result.ToArray();
            return true;
        }

        private static int ReadCompressedInt(BinaryReader reader)
        {
            sbyte value = reader.ReadSByte();
            return value == sbyte.MinValue ? reader.ReadInt32() : value;
        }

        private readonly record struct RawWzString(bool IsUnicode, int CharacterCount, byte[] EncryptedBytes);
        private readonly record struct KeyByteConstraint(int Position, byte ExpectedValue);

        internal sealed class Worker : IDisposable
        {
            private readonly WzKeyBruteforceProbe _probe;
            private readonly Aes _aes;
            private readonly ICryptoTransform _encryptor;
            private readonly byte[] _input = new byte[CandidateBatchSize * AesBlockSize];
            private readonly byte[] _output = new byte[CandidateBatchSize * AesBlockSize];

            internal Worker(WzKeyBruteforceProbe probe)
            {
                _probe = probe;
                _aes = Aes.Create();
                _aes.KeySize = 256;
                _aes.BlockSize = AesBlockSize * 8;
                _aes.Key = probe._aesKey;
                _aes.Mode = CipherMode.ECB;
                _aes.Padding = PaddingMode.None;
                _encryptor = _aes.CreateEncryptor();
            }

            internal uint? FindFirst(
                ulong rangeStart,
                ulong rangeEnd,
                CancellationToken cancellationToken,
                Func<bool> shouldStop,
                Action<int> candidatesProcessed)
            {
                ulong current = rangeStart;
                while (current < rangeEnd && !shouldStop())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = (int)Math.Min((ulong)CandidateBatchSize, rangeEnd - current);
                    int matchingIndex = FindVerifiedMatch((uint)current, count);
                    candidatesProcessed(matchingIndex >= 0 ? matchingIndex + 1 : count);
                    if (matchingIndex >= 0)
                        return unchecked((uint)current + (uint)matchingIndex);

                    current += (uint)count;
                }

                return null;
            }

            internal int CountSignatureMatches(uint firstCandidate, int count)
            {
                EncryptCandidates(firstCandidate, count);

                int matches = 0;
                for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
                {
                    if (_probe.MatchesSignature(_output.AsSpan(candidateIndex * AesBlockSize, AesBlockSize)))
                        matches++;
                }

                return matches;
            }

            internal bool MatchesSignature(uint candidate)
            {
                EncryptCandidates(candidate, 1);
                return _probe.MatchesSignature(_output.AsSpan(0, AesBlockSize));
            }

            private int FindVerifiedMatch(uint firstCandidate, int count)
            {
                EncryptCandidates(firstCandidate, count);

                for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
                {
                    ReadOnlySpan<byte> keyStream = _output.AsSpan(candidateIndex * AesBlockSize, AesBlockSize);
                    if (!_probe.MatchesSignature(keyStream))
                        continue;

                    uint candidate = unchecked(firstCandidate + (uint)candidateIndex);
                    if (_probe.VerifyCandidate(candidate))
                        return candidateIndex;
                }

                return -1;
            }

            private void EncryptCandidates(uint firstCandidate, int count)
            {
                if ((uint)count > CandidateBatchSize)
                    throw new ArgumentOutOfRangeException(nameof(count));

                Span<uint> inputWords = MemoryMarshal.Cast<byte, uint>(_input.AsSpan(0, count * AesBlockSize));
                for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
                {
                    uint candidate = unchecked(firstCandidate + (uint)candidateIndex);
                    int wordIndex = candidateIndex * 4;
                    inputWords[wordIndex] = candidate;
                    inputWords[wordIndex + 1] = candidate;
                    inputWords[wordIndex + 2] = candidate;
                    inputWords[wordIndex + 3] = candidate;
                }

                int byteCount = count * AesBlockSize;
                int transformed = _encryptor.TransformBlock(_input, 0, byteCount, _output, 0);
                if (transformed != byteCount)
                    throw new CryptographicException("AES did not transform the complete IV candidate batch.");

                if (firstCandidate == 0)
                    _output.AsSpan(0, AesBlockSize).Clear();
            }

            public void Dispose()
            {
                _encryptor.Dispose();
                _aes.Dispose();
            }
        }
    }
}
