using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static KTWirzade.Shared.Playbook.FeaturePage;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Pages.SelectPage
{
    public partial class SelectISOPage : System.Windows.Controls.UserControl
    {
        public string DependsOn;

        private bool animating;

        private int enabledIndex = -1;

        private System.Windows.Controls.Button parentNextButton;


        public string Text
        {
            get
            {
                return (string)DescriptionText.GetValue(TextBlock.TextProperty);
            }
            set
            {
                DescriptionText.SetValue(TextBlock.TextProperty, value);
            }
        }

        public Thickness OptionsMargin
        {
            get
            {
                return (Thickness)OptionsContainer.GetValue(FrameworkElement.MarginProperty);
            }
            set
            {
                OptionsContainer.SetValue(FrameworkElement.MarginProperty, value);
            }
        }

        public Line TopLine
        {
            set
            {
                OptionsContainer.Margin = new Thickness(0.0, OptionsContainer.Margin.Top + 32.0, 0.0, 0.0);
                SetLineBlock(TopLineBlock, value);
                TopLineBlock.Visibility = Visibility.Visible;
            }
        }

        public Line BottomLine
        {
            set
            {
                SetLineBlock(BottomLineBlock, value);
                BottomLineBlock.Visibility = Visibility.Visible;
            }
        }

        public bool CheckDefaultBrowser { get; set; }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://store.steampowered.com/steamos/download?ver=steamdeck");
            }
            catch (Exception)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Error opening link.", "Warning", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Warning);
            }
        }

        private void Hyperlink_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
        }

        private void Hyperlink_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = null;
        }

        private void SetLineBlock(TextBlock block, Line value)
        {
            block.Opacity = 1.0;
            block.Inlines.Clear();
            if (value.Link != null)
            {
                Hyperlink link = new Hyperlink
                {
                    TextDecorations = null,
                    NavigateUri = new Uri(value.Link),
                    Inlines = { value.Text }
                };
                link.MouseEnter += delegate (object sender, System.Windows.Input.MouseEventArgs args)
                {
                    ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
                };
                link.MouseLeave += delegate (object sender, System.Windows.Input.MouseEventArgs args)
                {
                    ((Hyperlink)sender).TextDecorations = null;
                };
                link.Click += delegate
                {
                    try
                    {
                        Process.Start(value.Link);
                    }
                    catch (Exception)
                    {
                        KTWirzade.GUI.MessageBox.Show(typeof(MainWindow), "Link is invalid.", "Warning");
                    }
                };
                block.Inlines.Add(link);
            }
            else
            {
                block.Opacity = 0.7;
                block.Inlines.Add(value.Text);
            }
        }

        public async Task ShowSteamEULA(System.Windows.Controls.Button NextButton, RoutedEventArgs e)
        {
            parentNextButton = NextButton;
            parentNextButton.IsEnabled = false;
            foreach (System.Windows.Controls.RadioButton item in OptionsContainer.Children.Cast<System.Windows.Controls.RadioButton>().ToList())
            {
                item.IsHitTestVisible = false;
            }
            await Task.Delay(750);
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 380)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 380)))
            }
            };
            DoubleAnimation opacityAnimSteps = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            DoubleAnimation opacityAnimBlocks = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            DoubleAnimation opacityAnimOptions = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -93.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 340))
            };
            ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
            };
            ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            transitionAnim.KeyFrames.Add(transitionKey3);
            transitionAnim.KeyFrames.Add(transitionKey4);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimSteps, DescriptionText);
            Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimBlocks, LineBlocksContainer);
            Storyboard.SetTargetProperty(opacityAnimBlocks, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimOptions, OptionsContainer);
            Storyboard.SetTargetProperty(opacityAnimOptions, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(opacityAnimSteps);
            storyboard.Children.Add(opacityAnimBlocks);
            storyboard.Children.Add(opacityAnimOptions);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.8,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 250))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.8,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 250))
                }
            }
            };
            Storyboard.SetTargetName(scale_x, "browsertransform");
            Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
            storyboard.Children.Add(scale_x);
            Storyboard.SetTargetName(scale_y, "browsertransform");
            Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleXProperty));
            storyboard.Children.Add(scale_y);
            storyboard.Begin(this);
            BrowserModule.Visibility = Visibility.Visible;
        }

        public async Task ShowBinbowsEULA(System.Windows.Controls.Button NextButton, RoutedEventArgs e)
        {
            parentNextButton = NextButton;
            parentNextButton.IsEnabled = false;
            foreach (System.Windows.Controls.RadioButton item in OptionsContainer.Children.Cast<System.Windows.Controls.RadioButton>().ToList())
            {
                item.IsHitTestVisible = false;
            }
            await Task.Delay(750);
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 380)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 380)))
            }
            };
            DoubleAnimation opacityAnimSteps = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            DoubleAnimation opacityAnimBlocks = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            DoubleAnimation opacityAnimOptions = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)),
                To = 0.5
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -93.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 340))
            };
            ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
            };
            ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            transitionAnim.KeyFrames.Add(transitionKey3);
            transitionAnim.KeyFrames.Add(transitionKey4);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimSteps, DescriptionText);
            Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimBlocks, LineBlocksContainer);
            Storyboard.SetTargetProperty(opacityAnimBlocks, new PropertyPath("Opacity"));
            Storyboard.SetTarget(opacityAnimOptions, OptionsContainer);
            Storyboard.SetTargetProperty(opacityAnimOptions, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(opacityAnimSteps);
            storyboard.Children.Add(opacityAnimBlocks);
            storyboard.Children.Add(opacityAnimOptions);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.8,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 250))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.8,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 250))
                }
            }
            };
            Storyboard.SetTargetName(scale_x, "browsertransformbinbows");
            Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
            storyboard.Children.Add(scale_x);
            Storyboard.SetTargetName(scale_y, "browsertransformbinbows");
            Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleXProperty));
            storyboard.Children.Add(scale_y);
            storyboard.Begin(this);
            BrowserModuleBinbows.Visibility = Visibility.Visible;
        }

        public SelectISOPage()
        {
            InitializeComponent();
            OptionsMargin = new Thickness(0.0, 26.0, 0.0, 0.0);
            base.DataContext = this;
            Title.Text = "Image Options";
        }

        private async void SlideModule(bool wait = true, bool fadeBack = true)
        {
            if (!animating)
            {
                animating = true;
                Storyboard board = new Storyboard();
                DoubleAnimation opacityAnim = new DoubleAnimation();
                opacityAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 280));
                opacityAnim.To = 0.0;
                DoubleAnimationUsingKeyFrames opacityAnimSteps = new DoubleAnimationUsingKeyFrames();
                opacityAnimSteps.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 360));
                opacityAnimSteps.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))));
                opacityAnimSteps.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 360))));
                DoubleAnimationUsingKeyFrames opacityAnimBlocks = new DoubleAnimationUsingKeyFrames();
                opacityAnimBlocks.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 360));
                opacityAnimBlocks.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))));
                opacityAnimBlocks.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 360))));
                DoubleAnimationUsingKeyFrames opacityAnimOptions = new DoubleAnimationUsingKeyFrames();
                opacityAnimOptions.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 360));
                opacityAnimOptions.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))));
                opacityAnimOptions.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 360))));
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames();
                transitionAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540));
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = new Thickness(340.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 300))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(340.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 500))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -93.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 500))
                };
                transitionAnim.KeyFrames.Add(transitionKey1);
                transitionAnim.KeyFrames.Add(transitionKey2);
                transitionAnim.KeyFrames.Add(transitionKey3);
                transitionAnim.KeyFrames.Add(transitionKey4);
                ThicknessAnimationUsingKeyFrames transitionAnimStep = new ThicknessAnimationUsingKeyFrames();
                transitionAnimStep.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540));
                ThicknessKeyFrame transitionKeyStep1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 300))
                };
                ThicknessKeyFrame transitionKeyStep2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, -93.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 500))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -93.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 500))
                };
                ThicknessKeyFrame transitionKeyStep4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 500))
                };
                transitionAnimStep.KeyFrames.Add(transitionKeyStep1);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep2);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep3);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep4);
                Storyboard.SetTarget(transitionAnimStep, ContentGrid);
                Storyboard.SetTargetProperty(transitionAnimStep, new PropertyPath("Margin"));
                board.Children.Add(transitionAnimStep);
                Storyboard.SetTarget(opacityAnim, ModuleGrid);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimSteps, DescriptionText);
                Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimBlocks, LineBlocksContainer);
                Storyboard.SetTargetProperty(opacityAnimBlocks, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimOptions, OptionsContainer);
                Storyboard.SetTargetProperty(opacityAnimOptions, new PropertyPath("Opacity"));
                Storyboard.SetTarget(transitionAnim, ModuleGrid);
                Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                board.Children.Add(opacityAnim);
                board.Children.Add(opacityAnimSteps);
                board.Children.Add(opacityAnimBlocks);
                if (fadeBack)
                {
                    board.Children.Add(opacityAnimOptions);
                }
                board.Children.Add(transitionAnim);
                board.Begin();
                if (wait)
                {
                    await Task.Delay(500);
                    BrowserModule.Visibility = Visibility.Collapsed;
                    animating = false;
                }
            }
        }

        private async void BrowserKeepButton_OnClick(object sender, RoutedEventArgs e)
        {
            SlideModule(wait: true, fadeBack: false);
            await Task.Delay(1100);
            animating = false;
            BrowserModule.Visibility = Visibility.Collapsed;
            parentNextButton.IsEnabled = true;
            ((SelectISOPane)((FrameworkElement)((FrameworkElement)((FrameworkElement)base.Parent).Parent).Parent).Parent).NextButton_OnClick(this, e);
        }

        private async void BrowserInstallButton_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.RadioButton item in OptionsContainer.Children.Cast<System.Windows.Controls.RadioButton>())
            {
                item.IsHitTestVisible = true;
            }
            parentNextButton.IsEnabled = true;
            SlideModule();
        }

        private async void CheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            SelectISOPane.HasShownSteamEULA = true;
            await Task.Delay(250);
            foreach (System.Windows.Controls.RadioButton item in OptionsContainer.Children.Cast<System.Windows.Controls.RadioButton>())
            {
                item.IsHitTestVisible = true;
            }
            parentNextButton.IsEnabled = true;
            SlideModule();
        }

        private async void BinbowsCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            SelectISOPane.HasShownBinbowsEULA = true;
            await Task.Delay(250);
            foreach (System.Windows.Controls.RadioButton item in OptionsContainer.Children.Cast<System.Windows.Controls.RadioButton>())
            {
                item.IsHitTestVisible = true;
            }
            parentNextButton.IsEnabled = true;
            SlideModule();
        }
    }
}
