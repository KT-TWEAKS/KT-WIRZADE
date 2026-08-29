namespace KTWirzade.GUI.Pages.IsoPage
{
    public partial class RadioPlaybookButton : System.Windows.Controls.RadioButton
    {
        public bool Selected;

        public PlaybookGUI Playbook { get; set; }

        public RadioPlaybookButton()
        {
            InitializeComponent();
            Loaded += delegate
            {
                Text.Text = Playbook.Name;
                Image.Source = Playbook.Icon;
            };
        }
    }
}