using System.Windows.Forms;

namespace TPShipToolkit
{
    public partial class HelpWindow : Form
    {
        public HelpWindow(Form owner)
        {
            InitializeComponent();
            this.Owner = owner;
            CenterToParent();
        }
    }
}
