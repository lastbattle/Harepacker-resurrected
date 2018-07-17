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
using System.Windows.Forms;
namespace HaRepackerLib
{
    public class WzMp3Streamer
    {
        private Stream byteStream;

        private Mp3FileReader mpegStream;
        private WaveFileReader waveFileStream;        

        private WaveOut wavePlayer;
        private WzSoundProperty sound;
        private bool repeat;
        private bool status = true;

        public WzMp3Streamer(WzSoundProperty sound, bool repeat)
        {
            this.repeat = repeat;
            this.sound = sound;
            byteStream = new MemoryStream(sound.GetBytes(false));

            wavePlayer = new WaveOut(WaveCallbackInfo.FunctionCallback());
            
            
            try
            {
                mpegStream = new Mp3FileReader(byteStream);
                wavePlayer.Init(mpegStream);
            }
            catch (System.InvalidOperationException)
            {
                try
                {
                    waveFileStream = new WaveFileReader(byteStream);
                    wavePlayer.Init(waveFileStream);
                }
                catch (FormatException)
                {
                    status = false;
                }
                //InvalidDataException
            }            
            finally
            {
                if(!status) MessageBox.Show("it's not possible to read this format", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            wavePlayer.PlaybackStopped += new EventHandler<StoppedEventArgs>(wavePlayer_PlaybackStopped);
        }

        void wavePlayer_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (repeat && !disposed)
            {
                if (mpegStream != null)
                    mpegStream.Seek(0, SeekOrigin.Begin);
                else
                    waveFileStream.Seek(0, SeekOrigin.Begin);

                wavePlayer.Pause();
                wavePlayer.Play();
            }
        }

        private bool disposed = false;
        public bool Disposed
        {
            get { return disposed; }
        }
        public void Dispose()
        {
            if (!status) return;
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void Play()
        {
            if (!status) return;
            wavePlayer.Play();
        }

        public void Pause()
        {
            if (!status) return;
            wavePlayer.Pause();
        }
        
        public void Stop()
        {
            if (!status) return;
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
        public int LengthPerByte
        {
            get {
                if (mpegStream != null)
                    return (int)mpegStream.Length;
                else if (waveFileStream != null)
                    return (int)waveFileStream.Length;
                return 0;
            }
        }
        public int PositionPerByte
        {
            get
            {
                if (mpegStream != null)
                    return (int)mpegStream.Position;
                else if (waveFileStream != null)
                    return (int)waveFileStream.Position;
                return 0;
            }
            set
            {
                if (mpegStream != null)
                    mpegStream.Seek(value, SeekOrigin.Begin);
                else if (waveFileStream != null)
                    waveFileStream.Seek(value, SeekOrigin.Begin);
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
