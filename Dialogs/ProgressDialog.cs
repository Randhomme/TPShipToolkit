using System;
using System.Windows.Forms;

namespace TPShipToolkit.Dialogs
{
    /// <summary>
    /// A progress dialog window to display a progress on a bar, and logs.
    /// </summary>
    public partial class ProgressDialog : Form
    {
        private int filecount;

        //We use 2 different progress and not one because we update the logs alone a lot.
        public IProgress<int> Progress { get; }
        public IProgress<string> Logs { get; }

        public ProgressDialog(string title, int filecount)
        {
            InitializeComponent();
            this.Text = title;
            this.filecount = filecount;
            this.progressBar1.Maximum = this.filecount;
            this.label1.Text = "Opened 0 / " + this.filecount;
            Progress = new Progress<int>(ProgressReport);
            Logs = new Progress<string>(LogsReport);
        }

        /// <summary>
        /// Enable the close button.
        /// </summary>
        public void EnableClose()
        {
            button1.Enabled = true;
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Value = progressBar1.Maximum;
        }

        /// <summary>
        /// Update the progress bar.
        /// </summary>
        /// <param name="progress">The actual progress.</param>
        private void ProgressReport(int progress)
        {
            //this.progressBar1.Value = progress;
            label1.Text = "Opened " + progress + " / " + filecount;
        }

        /// <summary>
        /// Update the logs.
        /// </summary>
        /// <param name="logs">The logs to be displayed.</param>
        private void LogsReport(string logs)
        {
            richTextBox1.Text += logs;
        }
    }
}
