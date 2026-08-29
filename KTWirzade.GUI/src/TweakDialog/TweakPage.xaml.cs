using System.Windows.Controls;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.TweakDialog
{
    public partial class TweakPage : Grid
    {
        public double PageHeight
        {
            get
            {
                return (double)GetValue(HeightProperty);
            }
            set
            {
                SetValue(HeightProperty, value);
            }
        }

        public TweakPage()
        {
            InitializeComponent();
        }

        public void SetLast()
        {
            SetValue(HeightProperty, 501);
        }
    }
}