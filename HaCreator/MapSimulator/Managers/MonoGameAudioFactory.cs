using MapleLib.PacketLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Audio;
using NAudio.Wave;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MonoGameAudioFactory
    {
        public static SoundEffect CreateSoundEffect(WzBinaryProperty sound)
        {
            return CreateSoundEffect(sound, 0, out _);
        }

        public static SoundEffect CreateSoundEffect(WzBinaryProperty sound, int startOffsetMs, out TimeSpan availableDuration)
        {
            if (sound == null)
            {
                throw new ArgumentNullException(nameof(sound));
            }

            using MemoryStream wavStream = CreateWaveStream(sound);
            using WaveFileReader reader = new(wavStream);

            TimeSpan requestedOffset = TimeSpan.FromMilliseconds(Math.Max(0, startOffsetMs));
            if (requestedOffset > reader.TotalTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startOffsetMs),
                    $"Audio offset {startOffsetMs} ms exceeds track duration {reader.TotalTime.TotalMilliseconds:0} ms.");
            }

            if (requestedOffset == reader.TotalTime)
            {
                availableDuration = TimeSpan.Zero;
                return CreateSilentSoundEffect(reader.WaveFormat);
            }

            reader.Position = AlignToBlock(reader.WaveFormat, reader.WaveFormat.AverageBytesPerSecond * requestedOffset.TotalSeconds);
            TimeSpan actualOffset = TimeSpan.FromSeconds((double)reader.Position / reader.WaveFormat.AverageBytesPerSecond);
            availableDuration = reader.TotalTime - actualOffset;

            using MemoryStream trimmedWavStream = new();
            using (WaveFileWriter writer = new(trimmedWavStream, reader.WaveFormat))
            {
                byte[] buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                }
            }

            byte[] trimmedSoundBytes = trimmedWavStream.ToArray();
            using MemoryStream playbackStream = new(trimmedSoundBytes, writable: false);
            return SoundEffect.FromStream(playbackStream);
        }

        private static SoundEffect CreateSilentSoundEffect(WaveFormat waveFormat)
        {
            using MemoryStream silentWavStream = new();
            using (WaveFileWriter writer = new(silentWavStream, waveFormat))
            {
                writer.Write(new byte[Math.Max(1, waveFormat?.BlockAlign ?? 1)], 0, Math.Max(1, waveFormat?.BlockAlign ?? 1));
            }

            silentWavStream.Position = 0;
            return SoundEffect.FromStream(silentWavStream);
        }

        private static MemoryStream CreateWaveStream(WzBinaryProperty sound)
        {
            switch (sound.WavFormat.Encoding)
            {
                case WaveFormatEncoding.MpegLayer3:
                    {
                        MemoryStream wavStream = new();
                        using MemoryStream sourceStream = new(sound.GetBytes(false), writable: false);
                        using Mp3FileReader mp3Reader = new(sourceStream);
                        using WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
                        WaveFileWriter.WriteWavFileToStream(wavStream, pcmStream);
                        wavStream.Position = 0;
                        return wavStream;
                    }

                case WaveFormatEncoding.Pcm:
                    return new MemoryStream(sound.GetBytesForWAVPlayback(), writable: false);

                default:
                    throw new NotSupportedException($"Unsupported audio format: {sound.WavFormat.Encoding}");
            }
        }

        private static long AlignToBlock(WaveFormat waveFormat, double byteOffset)
        {
            int blockAlign = Math.Max(1, waveFormat?.BlockAlign ?? 1);
            long alignedOffset = (long)byteOffset;
            return alignedOffset - (alignedOffset % blockAlign);
        }
    }
}
