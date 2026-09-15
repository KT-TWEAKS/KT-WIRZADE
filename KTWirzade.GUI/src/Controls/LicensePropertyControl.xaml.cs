using System.Windows;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Controls
{
    public partial class LicensePropertyControl : System.Windows.Controls.UserControl
    {

        public static readonly DependencyProperty TitleContent = DependencyProperty.Register("Title", typeof(string), typeof(LicensePropertyControl), new PropertyMetadata(""));

        public static readonly DependencyProperty DescriptionContent = DependencyProperty.Register("Description", typeof(string), typeof(LicensePropertyControl), new PropertyMetadata(""));

        public object Description
        {
            get
            {
                return GetValue(DescriptionContent);
            }
            set
            {
                SetValue(DescriptionContent, value);
            }
        }

        public object Title
        {
            get
            {
                return GetValue(TitleContent);
            }
            set
            {
                SetValue(TitleContent, value);
            }
        }

        public LicensePropertyControl()
        {
            InitializeComponent();
        }
    }
}
