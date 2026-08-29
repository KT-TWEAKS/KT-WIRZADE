using System.Windows.Media;

namespace KTWirzade.GUI.Views
{
    public partial class FinishPageView : System.Windows.Controls.UserControl
    {
        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 22,
            G = 124,
            B = 50
        });
        public FinishPageView()
        {
            InitializeComponent();
            RebootText.Foreground = SuccessBrush;
        }
    }
}