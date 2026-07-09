using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TPShipToolkit.Dialogs
{
    public partial class ModelFileFormatDialog : Form
    {
        public ModelFileFormatDialog()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
        }

        public string SelectedFormat { get; private set; } = "glb";

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedFormat = comboBox1.SelectedItem.ToString();
        }
    }
}
