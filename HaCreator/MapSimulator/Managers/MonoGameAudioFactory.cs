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
            if (sound == null)
            {
                throw new ArgumentNullException(nameof(sound));
            }

            switch (sound.WavFormat.Encoding)
            {
                case WaveFormatEncoding.MpegLayer3:
                    using (var sourceStream = new MemoryStream(sound.GetBytes(false), writable: false))
                    using (var mp3Reader = new Mp3FileReader(sourceStream))
                    using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader))
                    using (var wavStream = new MemoryStream())
                    {
                        WaveFileWriter.WriteWavFileToStream(wavStream, pcmStream);
                        wavStream.Position = 0;
                        return SoundEffect.FromStream(wavStream);
                    }

                case WaveFormatEncoding.Pcm:
                    using (var wavStream = new MemoryStream(sound.GetBytesForWAVPlayback(), writable: false))
                    {
                        return SoundEffect.FromStream(wavStream);
                    }

                default:
                    throw new NotSupportedException($"Unsupported audio format: {sound.WavFormat.Encoding}");
            }
        }
    }
}
