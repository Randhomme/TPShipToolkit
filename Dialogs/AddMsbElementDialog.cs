using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace TPShipToolkit.Dialogs
{
    public partial class AddMsbElementDialog : Form
    {
        private readonly List<StringBuilder> _elementsName;
        public int ElementType { get => comboBox1.SelectedIndex; }
        public string ElementName { get => textBox1.Text; }
        public AddMsbElementDialog(List<StringBuilder> elementsName)
        {
            InitializeComponent();
            this.comboBox1.SelectedIndex = 0;
            this.button2.Enabled = false;
            _elementsName = elementsName;
        }

        /// <summary>
        /// Check if we can add the element to the list because we can't have 2 elements with the same name.
        /// </summary>
        /// <param name="name">The name of the element to add.</param>
        /// <returns>True if we can add the element, false if not.</returns>
        private bool CanAdd(string name)
        {
            foreach (StringBuilder s in _elementsName)
            {
                if (s.ToString().Equals(name))
                {
                    return false;
                }
            }
            return true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (CanAdd(textBox1.Text))
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("An element with the same name already exists.");
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                button2.Enabled = false;
            }
            else
            {
                button2.Enabled = true;
            }
        }
    }
}
