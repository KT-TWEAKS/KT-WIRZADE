using System;
using System.Windows.Media;
using KTWirzade.Shared;

namespace KTWirzade.GUI.Views
{
    public partial class FinishErrorPageView : System.Windows.Controls.UserControl
    {

        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 217,
            G = 43,
            B = 54
        });

        public FinishErrorPageView()
        {
            InitializeComponent();
            errorBox.Height = 150.0;
            RebootText.Foreground = ErrorBrush;
            for (int i = 0; i < AmeliorationUtil.ErrorDisplayList.Count; i++)
            {
                if (i == AmeliorationUtil.ErrorDisplayList.Count - 1)
                {
                    errorBox.AppendText(AmeliorationUtil.ErrorDisplayList[i]);
                }
                else
                {
                    errorBox.AppendText(AmeliorationUtil.ErrorDisplayList[i] + Environment.NewLine + Environment.NewLine);
                }
            }
            errorBox.ScrollToEnd();
        }
    }
}