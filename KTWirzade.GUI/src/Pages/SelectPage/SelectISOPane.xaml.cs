using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Pages.ModePage;
using KTWirzade.Shared;
using static KTWirzade.Shared.Playbook;
using static KTWirzade.Shared.Playbook.CheckboxPage;
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


namespace KTWirzade.GUI.Pages.SelectPage
{
    public partial class SelectISOPane : System.Windows.Controls.UserControl
    {
        public Action<List<string>> FinishAction;

        public Action CancelAction;

        private Playbook.FeaturePage[] _pages;

        public static bool HasShownSteamEULA;

        public static bool HasShownBinbowsEULA;

        private int Index;

        private int NoneCount;

        private List<string> Choices = new List<string>();

        private bool animating;


        public SelectISOPane()
        {
            InitializeComponent();
        }

        public void LoadPages(Playbook.FeaturePage[] pages)
        {
            Index = 0;
            NoneCount = 0;
            Choices = new List<string>();
            MainContainerGrid.Children.Clear();
            MainContainerGrid.ColumnDefinitions.Clear();
            _pages = pages;
            NextText.Text = ((_pages.Length > 1) ? "Next" : "OK");
            foreach (Playbook.FeaturePage page in _pages)
            {
                if (page.GetType() == typeof(Playbook.CheckboxPage))
                {
                    LoadCheckboxPage((Playbook.CheckboxPage)page);
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
            MainContainerGrid.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness(0.0), new Duration(TimeSpan.Zero)));
            foreach (object option in ((SelectISOPage)MainContainerGrid.Children[Index]).OptionsContainer.Children)
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
            SelectISOPage template = new SelectISOPage();
            template.DependsOn = page.DependsOn;
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
            this.MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
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
                RadioImageButton panel = this.RadioImageOption(option, option.Name == page.DefaultOption, page.DependsOn);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
                if (panel.IsChecked.Value)
                {
                    this.SwitchRadioImage(panel, new RoutedEventArgs());
                }
            }
            Grid.SetColumn(template, this.MainContainerGrid.ColumnDefinitions.Count - 1);
            this.MainContainerGrid.Children.Add(template);
        }

        private void LoadRadioPage(Playbook.RadioPage page)
        {
            SelectISOPage template = new SelectISOPage();
            template.DependsOn = page.DependsOn;
            if (page.TopLine != null)
            {
                template.TopLine = page.TopLine;
            }
            if (page.BottomLine != null)
            {
                template.BottomLine = page.BottomLine;
            }
            template.Text = page.Description;
            this.MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
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
                System.Windows.Controls.RadioButton panel = this.RadioOption(option, option.Name == page.DefaultOption, page.DependsOn);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(0.0, 9.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
            }
            Grid.SetColumn(template, this.MainContainerGrid.ColumnDefinitions.Count - 1);
            this.MainContainerGrid.Children.Add(template);
        }

        private void LoadCheckboxPage(Playbook.CheckboxPage page)
        {
            SelectISOPage template = new SelectISOPage();
            template.DependsOn = page.DependsOn;
            if (page.TopLine != null)
            {
                template.TopLine = page.TopLine;
            }
            if (page.BottomLine != null)
            {
                template.BottomLine = page.BottomLine;
            }
            template.Text = page.Description;
            this.MainContainerGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(363.0)
            });
            foreach (Playbook.CheckboxPage.CheckboxOption option in page.Options)
            {
                option.DependsOn = page.DependsOn;
                System.Windows.Controls.CheckBox panel = this.CheckOption(option, page.DependsOn);
                if (template.OptionsContainer.Children.Count > 0)
                {
                    panel.Margin = new Thickness(0.0, 9.0, 0.0, 0.0);
                }
                template.OptionsContainer.Children.Add(panel);
            }
            Grid.SetColumn(template, this.MainContainerGrid.ColumnDefinitions.Count - 1);
            this.MainContainerGrid.Children.Add(template);
        }

        private System.Windows.Controls.CheckBox CheckOption(CheckboxOption option, string pageDependsOn)
        {
            bool isChecked = option.IsChecked;
            System.Windows.Controls.CheckBox checkbox = new System.Windows.Controls.CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Tag = (option).Name,
                Content = new TextBlock
                {
                    Text = (option).Text
                },
                Opacity = (option.IsEnabled ? 1.0 : 0.5),
                IsEnabled = option.IsEnabled
            };
            if (!isChecked)
            {
                checkbox.SetResourceReference(StyleProperty, "Unchecked");
            }
            checkbox.Unchecked += OptionDeselected;
            checkbox.Checked += OptionSelected;
            return checkbox;
        }

        private System.Windows.Controls.RadioButton RadioOption(RadioOption option, bool isDefault, string pageDependsOn)
        {
            System.Windows.Controls.RadioButton button = new System.Windows.Controls.RadioButton
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Inlines = { option.Text }
                },
                Tag = option.Name
            };
            if (isDefault)
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

        private RadioImageButton RadioImageOption(RadioImageOption option, bool isDefault, string pageDependsOn)
        {
            RadioImageButton button = new RadioImageButton
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Tag = option.Name
            };
            button.Text.Text = option.Text;
            if (option.FileName != null)
            {
                button.Image.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/OS/" + option.FileName.Replace(".png", "") + ".png"));
            }
            else if (option.None)
            {
                button.Image.Height = 20.0;
                button.Image.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/cancel_32.png"));
            }
            if (option.None)
            {
                if (option.Text == null)
                {
                    button.Text.Text = "None";
                }
                NoneCount++;
                button.Tag = "none-" + NoneCount.ToString();
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
            button.MouseEnter += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                Storyboard storyboard = new Storyboard();
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
                            Value = 1.0
                        }
                    }
                };
                Storyboard.SetTarget(textFadeAnim, button.Text);
                Storyboard.SetTargetProperty(textFadeAnim, new PropertyPath("Opacity", Array.Empty<object>()));
                storyboard.Children.Add(textFadeAnim);
                DoubleAnimationUsingKeyFrames fadeInAnim = new DoubleAnimationUsingKeyFrames
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
                Storyboard.SetTarget(fadeInAnim, button);
                Storyboard.SetTargetProperty(fadeInAnim, new PropertyPath("Opacity", Array.Empty<object>()));
                storyboard.Children.Add(fadeInAnim);
                storyboard.Begin();
            };
            button.MouseLeave += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                if (((RadioImageButton)sender).Selected)
                {
                    return;
                }
                Storyboard storyboard = new Storyboard();
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
                Storyboard.SetTarget(textFadeAnim, button.Text);
                Storyboard.SetTargetProperty(textFadeAnim, new PropertyPath("Opacity", Array.Empty<object>()));
                storyboard.Children.Add(textFadeAnim);
                DoubleAnimationUsingKeyFrames fadeAnim = new DoubleAnimationUsingKeyFrames
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
                Storyboard.SetTarget(fadeAnim, button);
                Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity", Array.Empty<object>()));
                storyboard.Children.Add(fadeAnim);
                storyboard.Begin();
            };
            if (isDefault)
            {
                button.IsChecked = new bool?(true);
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
            foreach (SelectISOPage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index)
                {
                    if (page.DependsOn == null || (!uncheckedOptions.Contains(page.DependsOn) && !nestedOptions.Contains(page.DependsOn)))
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
            foreach (SelectISOPage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index && page.DependsOn == name)
                {
                    NextText.Text = "Next";
                    break;
                }
            }
        }

        public async void SwitchRadioImage(object sender, RoutedEventArgs e)
        {
            RadioImageButton newButton = (RadioImageButton)sender;
            List<RadioImageButton> children = ((StackPanel)newButton.Parent).Children.Cast<RadioImageButton>().ToList();
            int activeIndex = children.FindIndex((RadioImageButton x) => x.Selected);
            if (children.FindIndex((RadioImageButton x) => x.Text == newButton.Text) != activeIndex || !(e.Source.ToString() != "Deselect"))
            {
                RadioImageButton activeButton = ((activeIndex == -1) ? new RadioImageButton() : children[activeIndex]);
                activeButton.Selected = false;
                bool num = e.Source != null && e.Source.ToString() == "Deselect";
                if (!num)
                {
                    newButton.Selected = true;
                }
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
                if (!num)
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
                if (!num)
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
                if (newButton.Text.Text == "SteamOS 3" && !HasShownSteamEULA)
                {
                    await ((SelectISOPage)MainContainerGrid.Children[0]).ShowSteamEULA(NextButton, e);
                }
            }
        }

        public void NextButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (animating)
            {
                return;
            }
            foreach (object option in ((SelectISOPage)MainContainerGrid.Children[Index]).OptionsContainer.Children)
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
            SelectISOPage nextPage;
            do
            {
                Index++;
                nextPage = (SelectISOPage)MainContainerGrid.Children[Index];
            }
            while (nextPage.DependsOn != null && !Choices.Contains(nextPage.DependsOn));
            if (MainContainerGrid.Children.Count - 1 == Index)
            {
                NextText.Text = "OK";
            }
            int i = -1;
            bool validPage = false;
            foreach (SelectISOPage page in MainContainerGrid.Children)
            {
                i++;
                if (i > Index && (page.DependsOn == null || Choices.Contains(page.DependsOn)))
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
                foreach (SelectISOPage page2 in MainContainerGrid.Children.Cast<SelectISOPage>().Skip(activeIndex + 1 - subtract))
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