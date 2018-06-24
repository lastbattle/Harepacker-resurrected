/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace HaRepackerLib.Controls
{
    public partial class SoundPlayer : UserControl
    {
        private WzMp3Streamer currAudio;
        //private string currSoundFile = "";
        private WzSoundProperty soundProp;

        public SoundPlayer()
        {
            InitializeComponent();
            Disposed += new EventHandler(SoundPlayer_Disposed);
        }

        void SoundPlayer_Disposed(object sender, EventArgs e)
        {
            if (currAudio != null)
                currAudio.Dispose();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            Audio("pause");
        }

        private void TimeBar_Scroll(object sender, EventArgs e)
        {
            if (currAudio != null)
                currAudio.PositionPerByte = ((TrackBar)sender).Value;
        }
        private void AudioTimer_Tick(object sender, EventArgs e)
        {            
            if (currAudio == null) return;

            if(currAudio.PositionPerByte >= currAudio.LengthPerByte && !LoopBox.Checked)
            {
                Audio("stop");
                return;
            }else if (currAudio.PositionPerByte >= currAudio.LengthPerByte && LoopBox.Checked)
            {                
                TimeBar.Value = 0;
                CurrentPositionLabel.Text = "00:00 /";
                return;
            }
            TimeBar.Value = currAudio.PositionPerByte;
            TimeSpan time = TimeSpan.FromSeconds(currAudio.Position);
            CurrentPositionLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0') + " /";                        
        }
        private void PrepareAudio()
        {
            if(currAudio == null)
            {
                currAudio = new WzMp3Streamer(soundProp, LoopBox.Checked);
                TimeBar.Maximum = currAudio.LengthPerByte;
                TimeBar.Minimum = 0;
            }
        }
        private void Audio(string status)
        {            
            switch (status)
            {
                case "play":
                    AudioTimer.Start();
                    currAudio.Play();                    
                    PlayButton.Visible = false;
                    PauseButton.Visible = true;
                    break;
                case "pause":
                    AudioTimer.Start();
                    currAudio.Pause();
                    PauseButton.Visible = false;
                    PlayButton.Visible = true;
                    break;
                case "stop":
                    AudioTimer.Stop();
                    PauseButton.Visible = false;
                    PlayButton.Visible = true;
                    TimeBar.Value = 0;
                    CurrentPositionLabel.Text = "00:00 /";                    
                    currAudio.Stop();
                    currAudio.Position = 0;
                    break;
            } 
        }
        private void PlayButton_Click(object sender, EventArgs e)
        {            
                //currSoundFile = Path.GetTempFileName();
                //soundProp.SaveToFile(currSoundFile);
            PrepareAudio();
            Audio("play");
        }

        public WzSoundProperty SoundProperty
        {
            get { return soundProp; }
            set 
            {
                if (PauseButton.Visible == true) PauseButton_Click(null, null);
                soundProp = value;
                //currSoundFile = "";
                if (currAudio != null && !currAudio.Disposed)
                    currAudio.Dispose();
                currAudio = null;
                if (soundProp != null)
                {
                    TimeSpan time = TimeSpan.FromMilliseconds(soundProp.Length);
                    LengthLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0');                    
                }
                CurrentPositionLabel.Text = "00:00 /";
                TimeBar.Value = 0;
            }
        }

        private void LoopBox_CheckedChanged(object sender, EventArgs e)
        {
            PrepareAudio();
            currAudio.Repeat = LoopBox.Checked;
        }
    }
}
