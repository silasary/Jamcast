using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jamcast5
{
    public partial class ProgressForm : Form
    {
        public ProgressForm()
        {
            InitializeComponent();
        }

        internal void SetProgress(string v1, int v2)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, int>(SetProgress), v1, v2);
                return;
            }
            if (!string.IsNullOrEmpty(v1))
                label1.Text = v1;
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Value = v2;
            if (!this.Visible)
                this.Show();
        }

        internal void UnsetProgress()
        {
            progressBar1.Style = ProgressBarStyle.Marquee;
        }
    }
}
