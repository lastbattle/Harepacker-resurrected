using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class WaitWindow : Form
    {
        private bool finished = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        public WaitWindow(string message)
        {
            InitializeComponent();
            this.label1.Text = message;
        }

        private void WaitWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !finished;
        }

        public void EndWait()
        {
            finished = true;
            Close();
        }
    }
}
