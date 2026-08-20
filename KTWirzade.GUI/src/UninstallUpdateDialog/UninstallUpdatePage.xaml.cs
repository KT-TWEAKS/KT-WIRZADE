using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.UninstallUpdateDialog
{
    public partial class UninstallUpdatePage : Grid
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

        public UninstallUpdatePage()
        {
            InitializeComponent();
        }

        public void SetLast()
        {
            SetValue(HeightProperty, 501);
        }
    }
}