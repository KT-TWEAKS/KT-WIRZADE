using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static KTWirzade.Shared.Playbook;
using static KTWirzade.Shared.Playbook.CheckboxPage;
using static KTWirzade.Shared.Playbook.FeaturePage;
using static KTWirzade.Shared.Requirements;

namespace KTWirzade.GUI.Views
{
    public partial class IsoModePageView : System.Windows.Controls.UserControl
    {
        private enum Page
        {
            Username = 0,
            Password = -363,
            AdminPassword = -726,
            Unknown = 1
        }

        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private bool _isMSAccount;

        private string Username;

        private List<string> defaultOptions;

        private async void LoadUserDetails(object sender, RoutedEventArgs e)
        {
            WindowsIdentity.GetCurrent().Name.Split('\\').FirstOrDefault();
            UserText.Text = "ISO\\" + WindowsIdentity.GetCurrent().Name.Split('\\').Last();
            DomainTextUsername.Text = "ISO\\";
            UsernameTextUsername.Text = WindowsIdentity.GetCurrent().Name.Split('\\').Last();
            UsernameBox.Text = WindowsIdentity.GetCurrent().Name.Split('\\').Last();
            Username = UsernameTextUsername.Text;
            AdminUserText.Text = "ISO\\Administrator";
            await Task.Run(delegate
            {
                try
                {
                    string text = (string)Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AccountPicture\\Users\\" + WindowsIdentity.GetCurrent().User)?.GetValue("Image96");
                    if (File.Exists(text))
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.UriSource = new Uri(text, UriKind.Absolute);
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                        PasswordPopup.Dispatcher.Invoke(delegate
                        {
                            UserImage.Source = bitmapImage;
                            UserImageUsername.Source = bitmapImage;
                            Person.Visibility = Visibility.Hidden;
                            PersonUsername.Visibility = Visibility.Hidden;
                        });
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        private void MainNextButton_OnClick()
        {
            PasswordBox.Password = string.Empty;
            AdminPasswordBox.Password = string.Empty;
            UsernameBox.Text = string.Empty;
            WindowsIdentity.GetCurrent().Name.Split('\\').FirstOrDefault();
            UserText.Text = "ISO\\" + WindowsIdentity.GetCurrent().Name.Split('\\').Last();
            CheckBox.IsChecked = false;
            NextText.Text = "Next";
            BlankPassword.Visibility = Visibility.Hidden;
            AdminBlankPassword.Visibility = Visibility.Hidden;
            InvalidUsername.Visibility = Visibility.Hidden;
            Page page = GetPages().First();
            MainContainerGrid.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness((double)page, 0.0, 0.0, 0.0), new Duration(new TimeSpan(0L))));
            PasswordPopup.IsOpen = true;
            FocusPage(page);
        }

        private void ModelNextButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            if (NextText.Text == "Next")
            {
                GetNextPage(GetCurrentPage());
                return;
            }
            if (string.IsNullOrEmpty(AdminPasswordBox.Password) && ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements.Contains((Requirement)8))
            {
                AdminBlankPassword.Visibility = Visibility.Visible;
                return;
            }
            PasswordPopup.IsOpen = false;
            List<Page> pages = GetPages();
            if (pages.Contains(Page.Username))
            {
                GlobalsGUI.Username = UsernameBox.Text;
            }
            if (pages.Contains(Page.Password))
            {
                GlobalsGUI.UserPassword = PasswordBox.Password;
                GlobalsGUI.AutoLogon = CheckBox.IsChecked.Value;
            }
            if (pages.Contains(Page.AdminPassword))
            {
                GlobalsGUI.AdminPassword = AdminPasswordBox.Password;
            }
            if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options == null)
            {
                ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options = defaultOptions;
            }
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                new IsoProgressDialog(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains("ame-drivers-network"), ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains("ame-drivers-graphics"), ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.Contains("ame-drivers-system")).ShowDialog(owner);
            });
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            PasswordPopup.IsOpen = false;
        }

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox obj = (PasswordBox)sender;
            TextBlock block = FindVisualChildren<TextBlock>(obj).FirstOrDefault((TextBlock x) => x.Name == "PasswordText");
            if (obj.Password.Length > 0)
            {
                if (block != null)
                {
                    block.Visibility = Visibility.Hidden;
                }
                BlankPassword.Visibility = Visibility.Hidden;
                AdminBlankPassword.Visibility = Visibility.Hidden;
            }
            else if (block != null)
            {
                block.Visibility = Visibility.Visible;
            }
        }

        private async void UsernameBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox TextBox = (System.Windows.Controls.TextBox)sender;
            TextBlock block = FindVisualChildren<TextBlock>(TextBox).FirstOrDefault((TextBlock x) => x.Name == "PasswordText");
            if (ValidUsername(TextBox.Text))
            {
                InvalidUsername.Visibility = Visibility.Hidden;
            }
            if (string.IsNullOrEmpty(TextBox.Text))
            {
                if (block != null)
                {
                    block.Visibility = Visibility.Visible;
                }
                return;
            }
            if (block != null)
            {
                block.Visibility = Visibility.Hidden;
            }
            string text = TextBox.Text;
            await Task.Delay(200);
            if (TextBox.Text == text)
            {
                UsernameTextUsername.Text = text;
            }
        }

        private Page GetCurrentPage()
        {
            if (!Enum.TryParse<Page>(MainContainerGrid.Margin.Left.ToString(CultureInfo.InvariantCulture), out var result))
            {
                return Page.Unknown;
            }
            return result;
        }

        private List<Page> GetPages()
        {
            List<Page> result = new List<Page>();
            result.Add(Page.Username);
            result.Add(Page.Password);
            if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements.Contains((Requirement)8))
            {
                result.Add(Page.AdminPassword);
            }
            return result;
        }

        private bool IsLastPage(Page page)
        {
            return GetPages().LastOrDefault() == page;
        }

        private void InputBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Page page = GetCurrentPage();
                if (!IsLastPage(page))
                {
                    GetNextPage(page);
                }
            }
        }

        private async void GetNextPage(Page page)
        {
            switch (page)
            {
                case Page.Unknown:
                    return;
                case Page.Username:
                    if (!ValidUsername(UsernameBox.Text))
                    {
                        InvalidUsername.Visibility = Visibility.Visible;
                        return;
                    }
                    break;
            }
            if (page == Page.Password && string.IsNullOrEmpty(PasswordBox.Password))
            {
                BlankPassword.Visibility = Visibility.Visible;
                return;
            }
            if (page == Page.AdminPassword && string.IsNullOrEmpty(AdminPasswordBox.Password))
            {
                AdminBlankPassword.Visibility = Visibility.Visible;
                return;
            }
            if (page == Page.Username)
            {
                LoadContainer.Visibility = Visibility.Visible;
                Spinner spinner = new Spinner
                {
                    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
                };
                LoadContainer.Children.Add(spinner);
                UsernameBox.IsEnabled = false;
                UsernameContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, new Duration(TimeSpan.FromMilliseconds(200.0))));
                ModelNextButton.IsEnabled = false;
                ModelCancelButton.IsEnabled = false;
                await Task.Delay(3000);
                ModelNextButton.IsEnabled = true;
                ModelCancelButton.IsEnabled = true;
                LoadContainer.Visibility = Visibility.Collapsed;
                LoadContainer.Children.Remove(spinner);
            }
            List<Page> pages = GetPages();
            if (pages[pages.Count - 2] == page)
            {
                NextText.Text = "OK";
            }
            Page newPage = pages[pages.IndexOf(page) + 1];
            Storyboard storyboard = new Storyboard();
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 180))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness((double)page, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Value = new Thickness((double)(page - 363), 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            Storyboard.SetTarget(transitionAnim, MainContainerGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(transitionAnim);
            storyboard.Begin();
            FocusPage(newPage);
            if (newPage == Page.Password)
            {
                WindowsIdentity.GetCurrent().Name.Split('\\').FirstOrDefault();
                UserText.Text = "ISO\\" + UsernameBox.Text;
            }
        }

        private void FocusPage(Page page)
        {
            if (page == Page.Username)
            {
                PasswordBox.FocusVisualStyle = null;
                PasswordBox.Focus();
            }
            if (page == Page.Password)
            {
                PasswordBox.FocusVisualStyle = null;
                PasswordBox.Focus();
            }
            if (page == Page.AdminPassword)
            {
                AdminPasswordBox.FocusVisualStyle = null;
                AdminPasswordBox.Focus();
            }
        }

        private static bool ValidUsername(string input)
        {
            return Regex.IsMatch(input, "^\\w[\\w\\.\\- ]{0,63}$");
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public IsoModePageView()
        {
            InitializeComponent();
            Hyperlink hyperlink = new Hyperlink();
            hyperlink.TextDecorations = null;
            hyperlink.Inlines.Add("Why do I need to do this?");
            hyperlink.MouseEnter += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
            };
            hyperlink.MouseLeave += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                ((Hyperlink)sender).TextDecorations = null;
            };
            hyperlink.Click += delegate
            {
                try
                {
                    Process.Start("https://docs.ameliorated.io/general/ms-account-conversion.html");
                }
                catch (Exception)
                {
                    KTWirzade.GUI.MessageBox.Show(typeof(MainWindow), "Link is invalid.", "Warning");
                }
            };
            BottomLineBlock.Inlines.Clear();
            FeaturesPane.FinishAction = async delegate (List<string> list)
            {
                ((IsoModePageViewModel)base.DataContext).MainNextButtonActive = true;
                FeaturesPopup.IsOpen = false;
                bool noDifference = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options != null && ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.SequenceEqual(list);
                ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options = list;
                if (DefaultBox.Visibility == Visibility.Visible && !noDifference)
                {
                    await SlideModuleDown();
                }
                if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages.Any((Playbook.FeaturePage x) => x.IsRequired))
                {
                    RequiredCompletedBox.Visibility = Visibility.Visible;
                }
                else if (!defaultOptions.SequenceEqual(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options))
                {
                    DefaultBox.Visibility = Visibility.Visible;
                    ResetButton.Visibility = Visibility.Visible;
                    DefaultText.Text = "Custom configuration will be applied";
                }
                if (!noDifference)
                {
                    SlideModuleUp();
                }
            };
            FeaturesPane.CancelAction = delegate
            {
                FeaturesPopup.IsOpen = false;
            };
            base.DataContextChanged += delegate
            {
                if (base.DataContext is ViewModelBase viewModelBase)
                {
                    defaultOptions = new List<string>();
                    if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages != null)
                    {
                        Playbook.FeaturePage[] featurePages = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages;
                        foreach (Playbook.FeaturePage val in featurePages)
                        {
                            if ((val.DependsOn == null || defaultOptions.Contains(val.DependsOn)) && (val.WindowsVersion == null || AmeliorationUtil.IsApplicableWindowsVersion(val.WindowsVersion, true, GlobalsGUI.Current.ISO.WinVer?.ToString() ?? "26100", GlobalsGUI.Current.ISO.WinUpdateVer?.ToString() ?? "0")))
                            {
                                if (((object)val).GetType() == typeof(CheckboxPage))
                                {
                                    foreach (Option current in ((FeaturePage)(CheckboxPage)val).Options.Where((Option x) => ((CheckboxOption)x).IsChecked))
                                    {
                                        defaultOptions.Add(current.Name);
                                    }
                                }
                                if (((object)val).GetType() == typeof(RadioPage))
                                {
                                    defaultOptions.Add(((RadioPage)val).DefaultOption);
                                }
                                if (((object)val).GetType() == typeof(RadioImagePage))
                                {
                                    defaultOptions.Add(((RadioImagePage)val).DefaultOption);
                                }
                            }
                        }
                    }
                    defaultOptions.Add("ame-drivers-network");
                    DefaultBox.Visibility = Visibility.Hidden;
                    CustomFeaturesHeader.Text = "Customize Features";
                    CustomFeaturesDescriptor.Text = "Modify Playbook functionality";
                    FeaturesButtonText.Text = "Select features";
                    viewModelBase.MainNextButtonCommand = new GlobalsGUI.CommandHandler(MainNextButton_OnClick, () => true);
                    if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options != null)
                    {
                        FeaturePage[] featurePages2 = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages;
                        if (featurePages2 != null && featurePages2.Any((FeaturePage x) => x.IsRequired))
                        {
                            CustomFeaturesHeader.Text = "Configure Options";
                            CustomFeaturesDescriptor.Text = "Setup Playbook functionality";
                            FeaturesButtonText.Text = "Select options";
                            DefaultBox.Visibility = Visibility.Hidden;
                            RequiredCompletedBox.Visibility = Visibility.Visible;
                            SlideModuleUp();
                        }
                        else if (!defaultOptions.SequenceEqual(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options))
                        {
                            DefaultBox.Visibility = Visibility.Visible;
                            ResetButton.Visibility = Visibility.Visible;
                            DefaultText.Text = "Custom configuration will be applied";
                            SlideModuleUp();
                        }
                    }
                    else
                    {
                        FeaturePage[] featurePages3 = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages;
                        if (featurePages3 != null && featurePages3.Any((FeaturePage x) => x.IsRequired))
                        {
                            CustomFeaturesHeader.Text = "Configure Options";
                            CustomFeaturesDescriptor.Text = "Setup Playbook functionality";
                            FeaturesButtonText.Text = "Select options";
                            DefaultBox.Visibility = Visibility.Visible;
                            ResetButton.Visibility = Visibility.Hidden;
                            DefaultText.Text = "You must select options before proceeding";
                            viewModelBase.MainNextButtonActive = false;
                            SlideModuleUp();
                        }
                    }
                }
            };
            PasswordPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(PasswordPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
                try
                {
                    MainWindow.CurrentDispatcher.Invoke(delegate
                    {
                        SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First()).Handle);
                    });
                }
                catch
                {
                }
                UsernameBox.IsEnabled = true;
                UsernameContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(0.0))));
                UsernameTextUsername.Text = Username;
            };
            FeaturesPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(FeaturesPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
                try
                {
                    MainWindow.CurrentDispatcher.Invoke(delegate
                    {
                        SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First()).Handle);
                    });
                }
                catch
                {
                }
                FeaturesPane.LoadPages();
            };
            base.Loaded += LoadUserDetails;
        }

        private void SlideModuleUp()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 260)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 260)))
            }
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 460))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
            };
            ThicknessKeyFrame transitionKey3 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                Value = new Thickness(0.0, -53.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 460))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            transitionAnim.KeyFrames.Add(transitionKey3);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(460.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 460))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            storyboard.Begin();
        }

        private async Task SlideModuleDown()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 280)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 280)))
            }
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 210))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -53.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 210))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            storyboard.Begin();
            await Task.Delay(300);
            DefaultBox.Visibility = Visibility.Hidden;
            RequiredCompletedBox.Visibility = Visibility.Hidden;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }
                foreach (T item in FindVisualChildren<T>(child))
                {
                    yield return item;
                }
            }
        }

        private void FocusWindow(object sender, EventArgs e)
        {
            try
            {
                MainWindow.CurrentDispatcher.Invoke(delegate
                {
                    SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First()).Handle);
                });
            }
            catch
            {
            }
        }

        private void FeaturesButton_OnClick(object sender, RoutedEventArgs e)
        {
            FeaturesPopup.IsOpen = true;
        }

        private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options != null)
            {
                ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options = null;
                if (((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages?.Any((FeaturePage x) => x.IsRequired) ?? false)
                {
                    ((IsoModePageViewModel)base.DataContext).MainNextButtonActive = false;
                    await SlideModuleDown();
                    DefaultText.Text = "You must select options before proceeding";
                    DefaultBox.Visibility = Visibility.Visible;
                    ResetButton.Visibility = Visibility.Hidden;
                    SlideModuleUp();
                }
                else
                {
                    await SlideModuleDown();
                }
            }
        }

    }
}
