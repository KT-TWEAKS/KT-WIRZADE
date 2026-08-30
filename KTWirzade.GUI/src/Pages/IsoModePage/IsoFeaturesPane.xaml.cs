using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Pages.ModePage;
using KTWirzade.Shared;
using static KTWirzade.Shared.Playbook.FeaturePage;
using static KTWirzade.Shared.Playbook.RadioImagePage;
using static KTWirzade.Shared.Playbook.RadioPage;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Pages.IsoModePage
{
    public partial class IsoFeaturesPane : System.Windows.Controls.UserControl
    {
        public Action<List<string>> FinishAction;

        public Action CancelAction;

        private int Index;

        private int NoneCount;

        private List<string> Choices = [];

        private bool animating;


        public static string SystemOEMDriversSize { get; set; }

        public IsoFeaturesPane()
        {
            InitializeComponent();
        }

        public void LoadPages()
        {
            Index = 0;
            NoneCount = 0;
            Choices = new List<string>();
            MainContainerGrid.Children.Clear();
            MainContainerGrid.ColumnDefinitions.Clear();
            TextBlock nextText = NextText;
            Playbook.FeaturePage[] featurePages = GlobalsGUI.Current.ISO.SelectedPlaybook.FeaturePages;
            nextText.Text = ((featurePages != null && featurePages.Length != 0) ? "Next" : "OK");
            foreach (Playbook.FeaturePage page in GlobalsGUI.Current.ISO.SelectedPlaybook.FeaturePages ?? Array.Empty<Playbook.FeaturePage>())
            {
                if (page.GetType() == typeof(Playbook.CheckboxPage))
                {
                    LoadCheckboxPage((Playbook.CheckboxPage)page, null);
                }
                if (page.GetType() == typeof(Playbook.RadioPage))
                {
                    LoadRadioPage((Playbook.RadioPage)page);
                }
                if (page.GetType() == typeof(Playbook.RadioImagePage))
                {
                    LoadRadioImagePage((Playbook.RadioImagePage)page);
                }
            }
            Playbook.CheckboxPage checkboxPage = new Playbook.CheckboxPage();
            checkboxPage.Description = "Please select which drivers you would like to be included in the injected ISO.";
            checkboxPage.TopLine = new Playbook.FeaturePage.Line
            {
                Text = "Driver packs are large and download slowly"
            };
            Playbook.FeaturePage featurePage = checkboxPage;
            Playbook.FeaturePage.Option[] options = new Playbook.CheckboxPage.CheckboxOption[]
            {
                new Playbook.CheckboxPage.CheckboxOption
                {
                    Name = "ame-drivers-network",
                    Text = "Include additional network drivers (~100 MB)",
                    IsChecked = true
                },
                new Playbook.CheckboxPage.CheckboxOption
                {
                    Name = "ame-drivers-graphics",
                    Text = "Include all latest graphics drivers (~4 GB)",
                    IsChecked = false
                },
                new Playbook.CheckboxPage.CheckboxOption
                {
                    Name = "ame-drivers-system",
                    Text = "Include all drivers from this PC" + ((!string.IsNullOrWhiteSpace(IsoFeaturesPane.SystemOEMDriversSize) && !IsoFeaturesPane.SystemOEMDriversSize.StartsWith("0")) ? (" (~" + IsoFeaturesPane.SystemOEMDriversSize + ")") : null),
                    IsChecked = false
                }
            };
            featurePage.Options = options;
            LoadCheckboxPage(checkboxPage, "Include Drivers");
            MainContainerGrid.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(0.0), new Duration(TimeSpan.Zero)));
            foreach (object option in ((IsoFeaturePage)MainContainerGrid.Children[Index]).OptionsContainer.Children)
            {
                if (option.GetType() == typeof(System.Windows.Controls.CheckBox))
                {
                    if (!((System.Windows.Controls.CheckBox)option).IsChecked.Value)
                    {
                        OptionDeselected((System.Windows.Controls.CheckBox)option, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((System.Windows.Controls.CheckBox)option, new RoutedEventArgs());
                    }
                }
                else if (option.GetType() == typeof(System.Windows.Controls.RadioButton))
                {
                    if (!((System.Windows.Controls.RadioButton)option).IsChecked.Value)
                    {
                        OptionDeselected((System.Windows.Controls.RadioButton)option, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((System.Windows.Controls.RadioButton)option, new RoutedEventArgs());
                    }
                }
                else if (option.GetType() == typeof(RadioImageButton))
                {
                    if (!((RadioImageButton)option).IsChecked.Value)
                    {
                        OptionDeselected((RadioImageButton)option, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((RadioImageButton)option, new RoutedEventArgs());
                    }
                }
            }
        }

        private void LoadRadioImagePage(Playbook.RadioImagePage page)
        {
            IsoFeaturePage template = new IsoFeaturePage();
            template.DependsOn = page.DependsOn;
            template.WindowsVersion = page.WindowsVersion;
            template.OptionsMargin = new Thickness(-2.0, 16.0, 0.0, 0.0);
            template.OptionsContainer.Orientation = System.Windows.Controls.Orientation.Horizontal;
            template.CheckDefaultBrowser = false;
            if (page.TopLine != null)
            {
                template.TopLine = page.TopLine;
            }
            if (page.BottomLine != null)
            {
                template.BottomLine = page.BottomLine;
            }
            template.Text = page.Description;
            MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(363.0)
            });
            if (page.DefaultOption == null)
            {
                page.DefaultOption = page.Options.First<Playbook.FeaturePage.Option>().Name;
            }
            foreach (Playbook.RadioImagePage.RadioImageOption option in page.Options)
            {
                option.DependsOn = page.DependsOn;
                option.WindowsVersion = page.WindowsVersion;
                RadioImageButton panel = RadioImageOption(option, option.Name == page.DefaultOption, page.DependsOn, page.WindowsVersion);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
                if (panel.IsChecked.Value)
                {
                    IsoFeaturesPane.SwitchRadioImage(panel, new RoutedEventArgs());
                }
            }
            int imageOptionCount = template.OptionsContainer.Children.Count;
            double naturalWidth = 78.0 * imageOptionCount + 8.0 * (imageOptionCount - 1);
            if (naturalWidth > 350.0)
            {
                double scale = 350.0 / naturalWidth;
                foreach (System.Windows.Controls.Control child in template.OptionsContainer.Children)
                {
                    child.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
            }
            Grid.SetColumn(template, MainContainerGrid.ColumnDefinitions.Count - 1);
            MainContainerGrid.Children.Add(template);
        }

        private void LoadRadioPage(Playbook.RadioPage page)
        {
            IsoFeaturePage template = new IsoFeaturePage();
            template.DependsOn = page.DependsOn;
            template.WindowsVersion = page.WindowsVersion;
            if (page.TopLine != null)
            {
                template.TopLine = page.TopLine;
            }
            if (page.BottomLine != null)
            {
                template.BottomLine = page.BottomLine;
            }
            template.Text = page.Description;
            MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(363.0)
            });
            if (page.DefaultOption == null)
            {
                page.DefaultOption = page.Options.First<Playbook.FeaturePage.Option>().Name;
            }
            foreach (Playbook.RadioPage.RadioOption option in page.Options)
            {
                option.DependsOn = page.DependsOn;
                option.WindowsVersion = page.WindowsVersion;
                System.Windows.Controls.RadioButton panel = RadioOption(option, option.Name == page.DefaultOption, page.DependsOn, page.WindowsVersion);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(0.0, (page.Options.Length > 4) ? 4.0 : 9.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
            }
            Grid.SetColumn(template, MainContainerGrid.ColumnDefinitions.Count - 1);
            MainContainerGrid.Children.Add(template);
        }

        private void LoadCheckboxPage(Playbook.CheckboxPage page, string title = null)
        {
            IsoFeaturePage template = new IsoFeaturePage();
            template.Title.Text = (title ?? template.Title.Text);
            template.DependsOn = page.DependsOn;
            template.WindowsVersion = page.WindowsVersion;
            if (page.TopLine != null)
            {
                template.TopLine = page.TopLine;
            }
            if (page.BottomLine != null)
            {
                template.BottomLine = page.BottomLine;
            }
            template.Text = page.Description;
            MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(363.0)
            });
            foreach (Playbook.CheckboxPage.CheckboxOption option in page.Options)
            {
                option.DependsOn = page.DependsOn;
                option.WindowsVersion = page.WindowsVersion;
                System.Windows.Controls.CheckBox panel = CheckOption(option, page.DependsOn, page.WindowsVersion);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(0.0, (page.Options.Length > 4) ? 4.0 : 9.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
            }
            Grid.SetColumn(template, MainContainerGrid.ColumnDefinitions.Count - 1);
            MainContainerGrid.Children.Add(template);
        }

        private System.Windows.Controls.CheckBox CheckOption(Playbook.CheckboxPage.CheckboxOption option, string pageDependsOn, string pageWindowsVersion)
        {
            bool isChecked = (GlobalsGUI.Current.ISO.SelectedPlaybook.Options != null && 
                (GlobalsGUI.Current.ISO.SelectedPlaybook.Options.Contains(option.Name) ||
                (((option.DependsOn != null && !GlobalsGUI.Current.ISO.SelectedPlaybook.Options.Contains(option.DependsOn)) || 
                (pageDependsOn != null && !GlobalsGUI.Current.ISO.SelectedPlaybook.Options.Contains(pageDependsOn))) && option.IsChecked))) || 
                (GlobalsGUI.Current.ISO.SelectedPlaybook.Options == null && option.IsChecked);
            System.Windows.Controls.CheckBox checkbox = new System.Windows.Controls.CheckBox
            {
                IsChecked = new bool?(isChecked),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Tag = option.Name,
                Content = new TextBlock
                {
                    Text = option.Text
                }
            };
            if (!isChecked)
            {
                checkbox.SetResourceReference(StyleProperty, "Unchecked");
            }
            checkbox.Unchecked += OptionDeselected;
            checkbox.Checked += OptionSelected;
            return checkbox;
        }

        private System.Windows.Controls.RadioButton RadioOption(RadioOption option, bool isDefault, string pageDependsOn, string pageWindowsVersion)
        {
            System.Windows.Controls.RadioButton button = new System.Windows.Controls.RadioButton
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Inlines = { ((Option)option).Text }
                },
                Tag = ((Option)option).Name
            };
            if ((((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options != null && (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains(((Option)option).Name) || (((((Option)option).DependsOn != null && !((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains(((Option)option).DependsOn)) || (pageDependsOn != null && !((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains(pageDependsOn))) && isDefault))) || (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options == null && isDefault))
            {
                button.IsChecked = true;
                button.SetResourceReference(StyleProperty, "RadioButton");
            }
            else
            {
                button.SetResourceReference(StyleProperty, "RadioButtonUnchecked");
            }
            button.Unchecked += OptionDeselected;
            button.Checked += OptionSelected;
            return button;
        }

        private RadioImageButton RadioImageOption(RadioImageOption option, bool isDefault, string pageDependsOn, string pageWindowsVersion)
        {
            RadioImageButton button = new RadioImageButton
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Tag = ((Option)option).Name
            };
            button.Text.Text = ((Option)option).Text;
            if (option.FileName != null)
            {
                foreach (BitmapImage image in GlobalsGUI.Current.ISO.SelectedPlaybook.Images)
                {
                    string fileName = Path.GetFileName(image.UriSource.AbsoluteUri);
                    if (option.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        button.Image.Source = image;
                    }
                    else if (option.FileName.Equals(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase))
                    {
                        button.Image.Source = image;
                    }
                }
            }
            else if (option.None)
            {
                button.Image.Height = 20.0;
                button.Image.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/cancel_32.png"));
            }
            if (option.None)
            {
                if (((Option)option).Text == null)
                {
                    button.Text.Text = "None";
                }
                NoneCount++;
                button.Tag = "none-" + NoneCount;
                button.TopGradient.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d8d6d6");
                button.BottomGradient.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d8d6d6");
            }
            if (option.GradientTopColor != null)
            {
                button.TopGradient.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(option.GradientTopColor);
            }
            if (option.GradientBottomColor != null)
            {
                button.BottomGradient.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(option.GradientBottomColor);
            }
            button.MouseEnter += delegate
            {
                Storyboard storyboard = new Storyboard();
                DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160.0)),
                        Value = 1.0
                    }
                }
                };
                Storyboard.SetTarget(doubleAnimationUsingKeyFrames, button.Text);
                Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames, new PropertyPath("Opacity"));
                storyboard.Children.Add(doubleAnimationUsingKeyFrames);
                DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160.0)),
                        Value = 1.0
                    }
                }
                };
                Storyboard.SetTarget(doubleAnimationUsingKeyFrames2, button);
                Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames2, new PropertyPath("Opacity"));
                storyboard.Children.Add(doubleAnimationUsingKeyFrames2);
                storyboard.Begin();
            };
            button.MouseLeave += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                if (!((RadioImageButton)sender).Selected)
                {
                    Storyboard storyboard = new Storyboard();
                    DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                        KeyFrames = new DoubleKeyFrameCollection
                    {
                        new EasingDoubleKeyFrame
                        {
                            EasingFunction = new SineEase
                            {
                                EasingMode = EasingMode.EaseInOut
                            },
                            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160.0)),
                            Value = 0.85
                        }
                    }
                    };
                    Storyboard.SetTarget(doubleAnimationUsingKeyFrames, button.Text);
                    Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames, new PropertyPath("Opacity"));
                    storyboard.Children.Add(doubleAnimationUsingKeyFrames);
                    DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                        KeyFrames = new DoubleKeyFrameCollection
                    {
                        new EasingDoubleKeyFrame
                        {
                            EasingFunction = new SineEase
                            {
                                EasingMode = EasingMode.EaseInOut
                            },
                            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160.0)),
                            Value = 0.8
                        }
                    }
                    };
                    Storyboard.SetTarget(doubleAnimationUsingKeyFrames2, button);
                    Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames2, new PropertyPath("Opacity"));
                    storyboard.Children.Add(doubleAnimationUsingKeyFrames2);
                    storyboard.Begin();
                }
            };
            if ((((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options != null && (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains((string)button.Tag) || (((((Option)option).DependsOn != null && !((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains(((Option)option).DependsOn)) || (pageDependsOn != null && !((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains(pageDependsOn))) && isDefault))) || (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options == null && isDefault))
            {
                button.IsChecked = true;
            }
            button.Checked += SwitchRadioImage;
            button.Unchecked += OptionDeselected;
            button.Checked += OptionSelected;
            return button;
        }

        private void OptionDeselected(object sender, RoutedEventArgs e)
        {
            StackPanel obj = (StackPanel)((ToggleButton)sender).Parent;
            int i = -1;
            List<string> nestedOptions = new List<string>();
            List<string> uncheckedOptions = (from ToggleButton x in obj.Children
                                             where !x.IsChecked.Value
                                             select (string)x.Tag).ToList();
            foreach (IsoFeaturePage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index)
                {
                    if ((page.DependsOn == null || (!uncheckedOptions.Contains(page.DependsOn) && !nestedOptions.Contains(page.DependsOn))) && (page.WindowsVersion == null || AmeliorationUtil.IsApplicableWindowsVersion(page.WindowsVersion, true, GlobalsGUI.Current.ISO.WinVer?.ToString() ?? "26100", GlobalsGUI.Current.ISO.WinUpdateVer?.ToString() ?? "0")))
                    {
                        return;
                    }
                    nestedOptions.AddRange(from System.Windows.Controls.Primitives.ButtonBase x in page.OptionsContainer.Children
                                           select (string)x.Tag);
                }
            }
            NextText.Text = "OK";
        }

        private void OptionSelected(object sender, RoutedEventArgs e)
        {
            string name = (string)((System.Windows.Controls.Primitives.ButtonBase)sender).Tag;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            int i = -1;
            foreach (IsoFeaturePage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index && page.DependsOn == name && (page.WindowsVersion == null || AmeliorationUtil.IsApplicableWindowsVersion(page.WindowsVersion, true, GlobalsGUI.Current.ISO.WinVer?.ToString() ?? "26100", GlobalsGUI.Current.ISO.WinUpdateVer?.ToString() ?? "0")))
                {
                    NextText.Text = "Next";
                    break;
                }
            }
        }

        public static void SwitchRadioImage(object sender, RoutedEventArgs e)
        {
            RadioImageButton newButton = (RadioImageButton)sender;
            List<RadioImageButton> children = ((StackPanel)newButton.Parent).Children.Cast<RadioImageButton>().ToList();
            int activeIndex = children.FindIndex((RadioImageButton x) => x.Selected);
            if (children.FindIndex((RadioImageButton x) => x.Text == newButton.Text) == activeIndex && e.Source.ToString() != "Deselect")
            {
                return;
            }
            RadioImageButton activeButton = ((activeIndex == -1) ? new RadioImageButton() : children[activeIndex]);
            activeButton.Selected = false;
            int num;
            if (e.Source != null)
            {
                num = ((e.Source.ToString() == "Deselect") ? 1 : 0);
                if (num != 0)
                {
                    goto IL_00ce;
                }
            }
            else
            {
                num = 0;
            }
            newButton.Selected = true;
            goto IL_00ce;
        IL_00ce:
            Storyboard board = new Storyboard();
            DoubleAnimationUsingKeyFrames ofadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 0.0
                }
            }
            };
            Storyboard.SetTarget(ofadeAnim, activeButton.OverlayContainer);
            Storyboard.SetTargetProperty(ofadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(ofadeAnim);
            DoubleAnimationUsingKeyFrames ofadeInAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                }
            }
            };
            Storyboard.SetTarget(ofadeInAnim, newButton.OverlayContainer);
            Storyboard.SetTargetProperty(ofadeInAnim, new PropertyPath("Opacity"));
            if (num == 0)
            {
                board.Children.Add(ofadeInAnim);
            }
            DoubleAnimationUsingKeyFrames fadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 0.8
                }
            }
            };
            Storyboard.SetTarget(fadeAnim, activeButton);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(fadeAnim);
            DoubleAnimationUsingKeyFrames fadeInAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                }
            }
            };
            Storyboard.SetTarget(fadeInAnim, newButton);
            Storyboard.SetTargetProperty(fadeInAnim, new PropertyPath("Opacity"));
            if (num == 0)
            {
                board.Children.Add(fadeInAnim);
            }
            DoubleAnimationUsingKeyFrames textFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(100.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160.0)),
                    Value = 0.85
                }
            }
            };
            Storyboard.SetTarget(textFadeAnim, activeButton.Text);
            Storyboard.SetTargetProperty(textFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(textFadeAnim);
            board.Begin();
        }

        public void NextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (animating)
            {
                return;
            }
            if (Index >= MainContainerGrid.Children.Count)
            {
                return;
            }
            foreach (object option in ((IsoFeaturePage)MainContainerGrid.Children[Index]).OptionsContainer.Children)
            {
                if (option.GetType() == typeof(System.Windows.Controls.CheckBox))
                {
                    if (((System.Windows.Controls.CheckBox)option).IsChecked.Value)
                    {
                        Choices.Add((string)((System.Windows.Controls.CheckBox)option).Tag);
                    }
                }
                else if (option.GetType() == typeof(System.Windows.Controls.RadioButton))
                {
                    if (((System.Windows.Controls.RadioButton)option).IsChecked.Value)
                    {
                        Choices.Add((string)((System.Windows.Controls.RadioButton)option).Tag);
                    }
                }
                else if (option.GetType() == typeof(RadioImageButton) && ((RadioImageButton)option).IsChecked.Value && (string)((RadioImageButton)option).Tag != null)
                {
                    Choices.Add((string)((RadioImageButton)option).Tag);
                }
            }
            if (NextText.Text == "OK")
            {
                FinishAction(Choices);
                return;
            }
            animating = true;
            int activeIndex = Index;
            IsoFeaturePage nextPage = null;
            do
            {
                Index++;
                if (Index >= MainContainerGrid.Children.Count)
                {
                    Index = MainContainerGrid.Children.Count - 1;
                    break;
                }
                nextPage = (IsoFeaturePage)MainContainerGrid.Children[Index];
            }
            while (nextPage != null && ((nextPage.DependsOn != null && !Choices.Contains(nextPage.DependsOn)) || (nextPage.WindowsVersion != null && !AmeliorationUtil.IsApplicableWindowsVersion(nextPage.WindowsVersion, true, GlobalsGUI.Current.ISO.WinVer?.ToString() ?? "26100", GlobalsGUI.Current.ISO.WinUpdateVer?.ToString() ?? "0"))));
            if (MainContainerGrid.Children.Count - 1 == Index)
            {
                NextText.Text = "OK";
            }
            int i = -1;
            bool validPage = false;
            foreach (IsoFeaturePage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index && (page.DependsOn == null || Choices.Contains(page.DependsOn)) && (page.WindowsVersion == null || AmeliorationUtil.IsApplicableWindowsVersion(page.WindowsVersion, true, GlobalsGUI.Current.ISO.WinVer?.ToString() ?? "26100", GlobalsGUI.Current.ISO.WinUpdateVer?.ToString() ?? "0")))
                {
                    validPage = true;
                }
            }
            if (!validPage)
            {
                NextText.Text = "OK";
            }
            int subtract = 0;
            while (activeIndex + 1 < Index)
            {
                MainContainerGrid.Children.RemoveAt(activeIndex + 1 - subtract);
                MainContainerGrid.ColumnDefinitions.RemoveAt(activeIndex + 1 - subtract);
                foreach (IsoFeaturePage page2 in MainContainerGrid.Children.Cast<IsoFeaturePage>().Skip(activeIndex + 1 - subtract))
                {
                    Grid.SetColumn(page2, Grid.GetColumn(page2) - 1);
                }
                activeIndex++;
                subtract++;
            }
            Index -= subtract;
            foreach (object option2 in nextPage.OptionsContainer.Children)
            {
                if (option2.GetType() == typeof(System.Windows.Controls.CheckBox))
                {
                    if (!((System.Windows.Controls.CheckBox)option2).IsChecked.Value)
                    {
                        OptionDeselected((System.Windows.Controls.CheckBox)option2, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((System.Windows.Controls.CheckBox)option2, new RoutedEventArgs());
                    }
                }
                else if (option2.GetType() == typeof(System.Windows.Controls.RadioButton))
                {
                    if (!((System.Windows.Controls.RadioButton)option2).IsChecked.Value)
                    {
                        OptionDeselected((System.Windows.Controls.RadioButton)option2, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((System.Windows.Controls.RadioButton)option2, new RoutedEventArgs());
                    }
                }
                else if (option2.GetType() == typeof(RadioImageButton))
                {
                    if (!((RadioImageButton)option2).IsChecked.Value)
                    {
                        OptionDeselected((RadioImageButton)option2, new RoutedEventArgs());
                    }
                    else
                    {
                        OptionSelected((RadioImageButton)option2, new RoutedEventArgs());
                    }
                }
            }
            Storyboard storyboard = new Storyboard();
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 180))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(MainContainerGrid.Margin.Left, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Value = new Thickness(MainContainerGrid.Margin.Left - 363.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            Storyboard.SetTarget(transitionAnim, MainContainerGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(transitionAnim);
            storyboard.Completed += async delegate
            {
                if (nextPage.CheckDefaultBrowser)
                {
                    await nextPage.CheckCurrentBrowser(NextButton, e);
                }
                animating = false;
            };
            storyboard.Begin();
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            NextButton.IsEnabled = true;
            CancelAction();
        }
    }
}
