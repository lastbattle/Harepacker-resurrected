using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for SoundPlayerXAML.xaml
    /// </summary>
    public partial class SoundPlayer : UserControl
    {
        private WzMp3Streamer currAudio;
        private WzSoundProperty soundProp;

        private DispatcherTimer timer;

        private bool isPlaying = false;
        private bool isUpdatingTimeLabel = false;

        public SoundPlayer()
        {
            InitializeComponent();

            Unloaded += SoundPlayerXAML_Unloaded;

            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan((int) (TimeSpan.TicksPerSecond / 2)); // every 0.5 sec
            timer.Tick += Timer_Tick;
        }

        /// <summary>
        /// Unloaded event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SoundPlayerXAML_Unloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }


        #region Slider
        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingTimeLabel)
                return; 


            PrepareAudioForPlayback();

            if (currAudio != null)
            {
                currAudio.Position = (int)(currAudio.Length / 100f * (float)slider1.Value); // convert trackbar 0~100 percentage to length position
                UpdateTimerLabel();
            }
        }
        #endregion

        #region Play pause
        private void checkbox_Replay_Checked(object sender, RoutedEventArgs e)
        {
            PrepareAudioForPlayback();

            currAudio.Repeat = checkbox_Replay.IsChecked == true;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                timer.Stop();

                currAudio.Pause();

                PauseButton.Content = "Play";
            } else
            {
                PrepareAudioForPlayback();

                currAudio.Play();
                timer.Start();

                PauseButton.Content = "Pause";
            }
            isPlaying = !isPlaying;
        }
        #endregion

        #region Etc

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isPlaying)
            {
                return;
            }
            UpdateTimerLabel();
        }

        private void UpdateTimerLabel()
        {
            isUpdatingTimeLabel = true; // flag

            slider1.Value = (int)(currAudio.Position / (float)currAudio.Length * 100f);
            TimeSpan time = TimeSpan.FromSeconds(currAudio.Position);
            CurrentPositionLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0');

            isUpdatingTimeLabel = false; // flag
        }

        private void PrepareAudioForPlayback()
        {
            if (currAudio == null)
            {
                currAudio = new WzMp3Streamer(soundProp, checkbox_Replay.IsChecked == true);
            }
        }
        #endregion

        #region Export fields
        public void Stop()
        {
            timer.Stop();

            try
            {
                if (currAudio != null)
                {
                    currAudio.Pause();
                    currAudio.Dispose();
                }
            }
            catch { }
        }

        public WzSoundProperty SoundProperty
        {
            get { return soundProp; }
            set
            {
                soundProp = value;

                if (currAudio != null && !currAudio.Disposed)
                    currAudio.Dispose();
                currAudio = null;

                if (soundProp != null)
                {
                    TimeSpan time = TimeSpan.FromMilliseconds(soundProp.Length);
                    LengthLabel.Text = Convert.ToString(time.Minutes).PadLeft(2, '0') + ":" + Convert.ToString(time.Seconds).PadLeft(2, '0');
                }
                CurrentPositionLabel.Text = "00:00 ";
                slider1.Value = 0;
            }
        }
        #endregion
    }
}
