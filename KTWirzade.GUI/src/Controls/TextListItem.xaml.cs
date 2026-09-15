using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    public partial class TextListItem : System.Windows.Controls.UserControl
    {

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<Inline> Inlines { get; set; } = new List<Inline>();

        public string Text
        {
            get
            {
                return (string)GetValue(TextBlock.TextProperty);
            }
            set
            {
                SetValue(TextBlock.TextProperty, value);
            }
        }

        public new FontWeight FontWeight
        {
            get
            {
                return (FontWeight)GetValue(TextBlock.FontWeightProperty);
            }
            set
            {
                SetValue(TextBlock.FontWeightProperty, value);
            }
        }

        [TypeConverter(typeof(FontSizeConverter))]
        [Localizability(LocalizationCategory.None)]
        public new double FontSize
        {
            get
            {
                return (double)GetValue(TextBlock.FontSizeProperty);
            }
            set
            {
                SetValue(TextBlock.FontSizeProperty, value);
            }
        }

        public new System.Windows.Media.Brush Foreground
        {
            get
            {
                return (System.Windows.Media.Brush)GetValue(TextBlock.ForegroundProperty);
            }
            set
            {
                SetValue(TextBlock.ForegroundProperty, value);
            }
        }

        [TypeConverter(typeof(LengthConverter))]
        public double LineHeight
        {
            get
            {
                return (double)GetValue(TextBlock.LineHeightProperty);
            }
            set
            {
                SetValue(TextBlock.LineHeightProperty, value);
            }
        }

        public TextTrimming TextTrimming
        {
            get
            {
                return (TextTrimming)GetValue(TextBlock.TextTrimmingProperty);
            }
            set
            {
                SetValue(TextBlock.TextTrimmingProperty, value);
            }
        }

        public TextWrapping TextWrapping
        {
            get
            {
                return (TextWrapping)GetValue(TextBlock.TextWrappingProperty);
            }
            set
            {
                SetValue(TextBlock.TextWrappingProperty, value);
            }
        }

        public Visibility PrefixVisibility
        {
            get
            {
                return (Visibility)PrefixBlock.GetValue(VisibilityProperty);
            }
            set
            {
                PrefixBlock.SetValue(VisibilityProperty, value);
            }
        }

        public string Prefix { get; set; }

        public void RefreshInlines()
        {
            MainBlock.Inlines.Clear();
            foreach (Inline inline in Inlines)
            {
                MainBlock.Inlines.Add(inline);
            }
        }

        public TextListItem()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += delegate
            {
                foreach (Inline current in Inlines)
                {
                    MainBlock.Inlines.Add(current);
                }
            };
        }
    }
}
