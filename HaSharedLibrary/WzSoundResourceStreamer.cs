/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using NAudio.Wave;
using System.IO;
using System.Diagnostics;
using MapleLib.PacketLib;

namespace HaSharedLibrary
{
    /// <summary>
    /// Streams wav, or mp3.
    /// </summary>
    public class WzSoundResourceStreamer
    {
        private readonly Stream byteStream;

        private Mp3FileReader mpegStream;
        private WaveFileReader waveFileStream;

        private readonly WaveOut wavePlayer;
        private readonly WzBinaryProperty sound;
        private bool repeat;

        private bool bPlaybackLoadedSuccess = true;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="repeat"></param>
        public WzSoundResourceStreamer(WzBinaryProperty sound, bool repeat)
        {
            this.repeat = repeat;
            this.sound = sound;

            wavePlayer = new WaveOut(WaveCallbackInfo.FunctionCallback());
            try
            {
                if (sound.WavFormat.Encoding == WaveFormatEncoding.MpegLayer3)
                {
                    this.byteStream = new MemoryStream(sound.GetBytes(false));
                    mpegStream = new Mp3FileReader(byteStream);
                    
                    wavePlayer.Init(mpegStream);
                } 
                else if (sound.WavFormat.Encoding == WaveFormatEncoding.Pcm)
                {
                    /*byte[] wavSoundBytes = sound.GetBytesForWAVPlayback();

                    this.byteStream = new MemoryStream(wavSoundBytes);
                    Debug.WriteLine(HexTool.ByteArrayToString(wavSoundBytes));

                    waveFileStream = new WaveFileReader(byteStream);
                    
                    wavePlayer.Init(waveFileStream);*/
                }
                else
                {
                    bPlaybackLoadedSuccess = false;
                }
            }
            catch (Exception exp)
            {
                bPlaybackLoadedSuccess = false;
                
                Debug.WriteLine(exp.ToString());
                //InvalidDataException
                // Message = "Not a WAVE file - no RIFF header"
            }
            Volume = 0.5f; // default volume
            wavePlayer.PlaybackStopped += new EventHandler<StoppedEventArgs>(wavePlayer_PlaybackStopped);
        }

        void wavePlayer_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (repeat)
            {
                if (disposed) {
                    return;
                }
                if (mpegStream != null)
                    mpegStream.Seek(0, SeekOrigin.Begin);
                else
                    waveFileStream.Seek(0, SeekOrigin.Begin);

                wavePlayer.Pause();
                wavePlayer.Play();
            } else {
                Position = 0;
            }
        }

        private bool disposed = false;
        public bool Disposed
        {
            get { return disposed; }
        }
        public void Dispose()
        {
            if (!bPlaybackLoadedSuccess)
                return;

            disposed = true;
            wavePlayer.Dispose();
            if (mpegStream != null)
            {
                mpegStream.Dispose();
                mpegStream = null;
            }
            if (waveFileStream != null)
            {
                waveFileStream.Dispose();
                waveFileStream = null;
            }
            byteStream.Dispose();
        }

        public void Play()
        {
            if (!bPlaybackLoadedSuccess)
                return;

            wavePlayer.Play();
        }

        public void Pause()
        {
            if (!bPlaybackLoadedSuccess)
                return;

            wavePlayer.Pause();
        }

        public void Stop()
        {
            if (!bPlaybackLoadedSuccess) return;
            wavePlayer.Stop();
        }

        public bool Repeat
        {
            get { return repeat; }
            set { repeat = value; }
        }

        public int Length
        {
            get { return sound.Length / 1000; }
        }

        public float Volume
        {
            get
            {
                return wavePlayer.Volume;
            }
            set {
                if (value >= 0 && value <= 1.0)
                {
                    this.wavePlayer.Volume = value;
                }
            }

        }

        public int Position
        {
            get
            {
                if (mpegStream != null)
                    return (int)(mpegStream.Position / mpegStream.WaveFormat.AverageBytesPerSecond);
                else if (waveFileStream != null)
                    return (int)(waveFileStream.Position / waveFileStream.WaveFormat.AverageBytesPerSecond);

                return 0;
            }
            set
            {
                if (mpegStream != null)
                    mpegStream.Seek(value * mpegStream.WaveFormat.AverageBytesPerSecond, SeekOrigin.Begin);
                else if (waveFileStream != null)
                    waveFileStream.Seek(value * waveFileStream.WaveFormat.AverageBytesPerSecond, SeekOrigin.Begin);
            }
        }
    }
}
