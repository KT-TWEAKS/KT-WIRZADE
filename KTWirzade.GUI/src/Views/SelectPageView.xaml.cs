using Core;
using Interprocess;
using iso_mode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Pages.SelectPage;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using static iso_mode.OSDownload;

namespace KTWirzade.GUI.Views
{
    public partial class SelectPageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private bool _boardRunning;


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out IsoOptionsPageView.RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public SelectPageView()
        {
            this.InitializeComponent();
            this.OptionsPane.CancelAction = delegate ()
            {
                this.OptionsPopup.IsOpen = false;
            };
            this.OptionsPane.FinishAction = new Action<List<string>>(this.DownloadISO);
            base.DataContextChanged += delegate (object sender, DependencyPropertyChangedEventArgs args)
            {
                if (!this._boardRunning && args.OldValue != null && args.NewValue != null && args.OldValue.GetType() == typeof(SelectPageViewModel) && args.NewValue.GetType() == typeof(SelectPageViewModel))
                {
                    Storyboard storyboard = new Storyboard();
                    DoubleAnimationUsingKeyFrames highlightAnimation = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = TimeSpan.FromMilliseconds(400.0),
                        KeyFrames = new DoubleKeyFrameCollection
                        {
                            new EasingDoubleKeyFrame(0.075, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)), new SineEase
                            {
                                EasingMode = EasingMode.EaseInOut
                            }),
                            new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400.0)), new CubicEase
                            {
                                EasingMode = EasingMode.EaseInOut
                            })
                        }
                    };
                    Storyboard.SetTarget(highlightAnimation, HighlightBorder);
                    Storyboard.SetTargetProperty(highlightAnimation, new PropertyPath("Opacity", Array.Empty<object>()));
                    storyboard.Children.Add(highlightAnimation);
                    this._boardRunning = true;
                    storyboard.Completed += delegate (object o, EventArgs eventArgs)
                    {
                        this._boardRunning = false;
                    };
                    storyboard.Begin();
                }
            };
            this.OptionsPopup.Opened += delegate (object sender, EventArgs args)
            {
                IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(this.OptionsPopup.Child)).Handle;
                IsoOptionsPageView.RECT rect;
                if (SelectPageView.GetWindowRect(hwnd, out rect))
                {
                    SelectPageView.SetWindowPos(hwnd, -2, rect.Left, rect.Top, (int)base.Width, (int)base.Height, 0);
                }
                try
                {
                    MainWindow.CurrentDispatcher.Invoke(delegate ()
                    {
                        SelectPageView.SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First<MainWindow>()).Handle);
                    });
                }
                catch
                {
                }
                SelectISOPane optionsPane = this.OptionsPane;
                Playbook.RadioImagePage[] array = new Playbook.RadioImagePage[1];
                int num = 0;
                Playbook.RadioImagePage radioImagePage = new Playbook.RadioImagePage();
                radioImagePage.Description = "Please select the operating system ISO file you would like to download.";
                radioImagePage.TopLine = new Playbook.FeaturePage.Line
                {
                    Text = "Playbook-injection only supports Windows"
                };
                Playbook.FeaturePage featurePage = radioImagePage;
                Playbook.FeaturePage.Option[] options = new Playbook.RadioImagePage.RadioImageOption[]
                {
                    new Playbook.RadioImagePage.RadioImageOption
                    {
                        FileName = "binbows11.png",
                        Text = "Win 11",
                        Name = "windows",
                        GradientTopColor = "#BCE9FE",
                        GradientBottomColor = "#ECF9FF"
                    },
                    new Playbook.RadioImagePage.RadioImageOption
                    {
                        FileName = "win10.png",
                        Text = "Win 10",
                        Name = "windows10",
                        GradientTopColor = "#BCE9FE",
                        GradientBottomColor = "#ECF9FF"
                    },
                    new Playbook.RadioImagePage.RadioImageOption
                    {
                        FileName = "localiso.png",
                        Text = "ISO Local",
                        Name = "localiso",
                        GradientTopColor = "#9FD8FF",
                        GradientBottomColor = "#E9F6FF"
                    }
                };
                featurePage.Options = options;
                radioImagePage.BottomLine = new Playbook.FeaturePage.Line
                {
                    Text = "Learn more",
                    Link = "https://learn.microsoft.com/windows/"
                };
                array[num] = radioImagePage;
                Playbook.FeaturePage[] pages = array;
                optionsPane.LoadPages(pages);
            };
        }
        private async void DownloadISO(List<string> choices)
        {
            _ = 4;
            try
            {
                string choice = choices.First();
                if (choice == "localiso")
                {
                    OptionsPopup.IsOpen = false;
                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        DefaultExt = ".iso",
                        Filter = "Imagens ISO|*.iso;*.img|Todos os arquivos|*",
                        Multiselect = false
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }
                    MainWindow localMainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                    await localMainWindow.LoadISO(dialog.FileName);
                    return;
                }
                OptionsPane.LoadContainer.Visibility = Visibility.Visible;
                Spinner spinner = new Spinner
                {
                    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
                };
                OptionsPane.LoadContainer.Children.Add(spinner);
                OptionsPane.MainContainerGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, new Duration(TimeSpan.FromMilliseconds(200.0))));
                OptionsPane.MainContainerGrid.IsHitTestVisible = false;
                OptionsPane.NextButton.IsEnabled = false;
                OptionsPane.CancelButton.IsEnabled = false;
                OS os = (OS)(choice switch
                {
                    "windows" => 0,
                    "ubuntu" => 1,
                    "arch" => 2,
                    "steamos" => 3,
                    "windows10" => 4,
                    _ => throw new Exception("Invalid choice: " + choice),
                });

                // Windows 10 download is discontinued by Microsoft - open browser instead
                if (os == OS.Windows10)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://www.microsoft.com/software-download/windows10ISO",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(this, "Não foi possível abrir o navegador.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    OptionsPopup.IsOpen = false;
                    return;
                }

                (string Link, string Version, string Hash) downloadInfo;
                try
                {
                    Task<(string Link, string Version, string Hash)> linkTask = OSDownload.GetDownloadLinkAsyncResilient(os);
                    await Task.WhenAll(Task.Delay(3000), linkTask);
                    downloadInfo = await linkTask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error fetching ISO download link: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                finally
                {
                    OptionsPane.LoadContainer.Visibility = Visibility.Collapsed;
                    OptionsPane.LoadContainer.Children.Remove(spinner);
                    OptionsPane.MainContainerGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(200.0))));
                    OptionsPane.MainContainerGrid.IsHitTestVisible = true;
                    OptionsPane.CancelButton.IsEnabled = true;
                    OptionsPane.NextButton.IsEnabled = true;
                    OptionsPopup.IsOpen = false;
                }
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                bool nbe = mainWindow.NextButton.IsEnabled;
                bool pbe = mainWindow.PreviousButton.IsEnabled;
                bool cbe = mainWindow.CancelButton.IsEnabled;
                bool clbe = mainWindow.CloseButton.IsEnabled;
                bool ubbe = mainWindow.UpdatesButton.IsEnabled;
                mainWindow.CurrentMainWindow.NextButtonActive = false;
                mainWindow.CurrentMainWindow.PreviousButtonActive = false;
                mainWindow.CurrentMainWindow.CancelButtonActive = false;
                mainWindow.CurrentMainWindow.CloseButtonActive = false;
                mainWindow.CurrentMainWindow.UpdatesButtonActive = false;
                foreach (IDragItem item2 in GlobalsGUI.Current.Items)
                {
                    item2.ItemClickable = false;
                }
                ISO iSO = new ISO();
                iSO.Architecture = ImageParsers.ImageArchitecture.x64;
                ISO iSO2 = iSO;
                iSO2.Name = (int)os switch
                {
                    0 => "Windows 11",
                    1 => "Ubuntu",
                    2 => "Arch Linux",
                    3 => "SteamOS",
                    4 => "Windows 10",
                    _ => throw new Exception("Invalid choice: " + choice),
                };
                ISO iSO3 = iSO;
                iSO3.Title = (int)os switch
                {
                    0 => "Windows 11" + ((downloadInfo.Version == null) ? "" : (" " + downloadInfo.Version)) + " ISO",
                    1 => "Ubuntu" + ((downloadInfo.Version == null) ? "" : (" " + downloadInfo.Version)) + " ISO",
                    2 => "Arch Linux" + ((downloadInfo.Version == null) ? "" : (" " + downloadInfo.Version)) + " ISO",
                    3 => "SteamOS" + ((downloadInfo.Version == null) ? "" : (" " + downloadInfo.Version)) + " ISO",
                    4 => "Windows 10" + ((downloadInfo.Version == null) ? "" : (" " + downloadInfo.Version)) + " ISO",
                    _ => throw new Exception("Invalid choice: " + choice),
                };
                ISO iSO4 = iSO;
                iSO4.ShortDescription = (int)os switch
                {
                    0 => "Standard Windows 11 ISO file",
                    1 => "Standard Ubuntu ISO file",
                    2 => "Arch Linux ISO file",
                    3 => "SteamOS repair image",
                    4 => "Standard Windows 10 ISO file",
                    _ => throw new Exception("Invalid choice: " + choice),
                };
                ISO iSO5 = iSO;
                iSO5.Username = (int)os switch
                {
                    0 => "Microsoft",
                    1 => "Canonical",
                    2 => "Arch",
                    3 => "Valve",
                    4 => "Microsoft",
                    _ => throw new Exception("Invalid choice: " + choice),
                };
                ISO item = iSO;
                item.ProgressVisibility = Visibility.Visible;
                item.ShortDescription = "0% (0 MB/s)";
                item.CurrentPage = new IsoPageViewModel
                {
                    Downloading = true
                };
                mainWindow.AddItem(item);
                SemaphoreSlim finished = new SemaphoreSlim(0, 1);
                try
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    try
                    {
                        Task.Run(delegate
                        {
                            while (!finished.Wait(0))
                            {
                                if (!GlobalsGUI.Current.Items.Contains(item))
                                {
                                    tokenSource.Cancel();
                                    break;
                                }
                                Thread.Sleep(100);
                            }
                        });
                        string destination = Path.GetTempFileName();
                        await OSDownload.DownloadISOAsync(downloadInfo.Link, destination, downloadInfo.Hash, tokenSource.Token, (Action<int, string>)delegate (int progress, string speed)
                        {
                            item.ShortDescription = $"{Math.Round((double)progress * 0.97)}% ({speed}/s)";
                            item.ProgressValue = (double)progress * 0.97;
                        });
                        if (tokenSource.IsCancellationRequested)
                        {
                            return;
                        }
                        item.ShortDescription = "97% (Verifying...)";
                        if (!string.IsNullOrWhiteSpace(downloadInfo.Hash))
                        {
                            if (OSDownload.GetSHA256(destination) != downloadInfo.Hash)
                            {
                                File.Delete(destination);
                                throw new Exception("SHA256 hash mismatch.");
                            }
                            item.ProgressValue = 98.0;
                            item.ShortDescription = "98% (Verifying...)";
                        }
                        string fileName = item.Title.Replace(" ", "_").Replace("_ISO", "") + ".iso";
                        await InterLink.ExecuteSafeAsync((Expression<Action>)(() => MoveISO(destination, fileName)), false, -1);
                        await Task.WhenAll(mainWindow.LoadISO(Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Images"), fileName), item), Task.Delay(1000));
                    }
                    finally
                    {
                        if (tokenSource != null)
                        {
                            ((IDisposable)tokenSource).Dispose();
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Log.EnqueueExceptionSafe(ex2, Array.Empty<(string, object)>());
                    MessageBox.Show(this, "Error downloading ISO: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    GlobalsGUI.Current.Items.Remove(item);
                }
                finally
                {
                    mainWindow.CurrentMainWindow.NextButtonActive = nbe;
                    mainWindow.CurrentMainWindow.PreviousButtonActive = pbe;
                    mainWindow.CurrentMainWindow.CancelButtonActive = cbe;
                    mainWindow.CurrentMainWindow.CloseButtonActive = clbe;
                    mainWindow.CurrentMainWindow.UpdatesButtonActive = ubbe;
                    foreach (IDragItem item3 in GlobalsGUI.Current.Items)
                    {
                        item3.ItemClickable = true;
                    }
                    finished.Release();
                }
            }
            catch (Exception ex3)
            {
                Log.WriteExceptionSafe(ex3, Array.Empty<(string, object)>());
                MessageBox.Show(this, "Error attempting to download ISO link: " + ex3.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void MoveISO(string source, string destinationFileName)
        {
            string destination = Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Images"), destinationFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            File.Move(source, destination);
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

        private async void UseExistingButton_OnClick(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            dialog.DefaultExt = ".apbx";
            dialog.Filter = "KT WIRZADE Playbooks|*.apbx|ISO Files|*.iso;*.img;*.gz;*.bz2;*.bzip2|All Files|*";
            dialog.Multiselect = false;
            bool? dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }
            string extension = System.IO.Path.GetExtension(dialog.FileName).ToLower();
            await MainWindow.CurrentDispatcher.Invoke((Func<Task>)async delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                if (new string[5] { ".iso", ".img", ".gz", ".bz2", ".bzip2" }.Contains(extension))
                {
                    await mainWindow.LoadISO(dialog.FileName);
                }
                else
                {
                    await mainWindow.LoadPlaybook(dialog.FileName);
                }
            });
        }

        private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/kelvenapk");
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Error opening link.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DownloadISOButton_OnClick(object sender, RoutedEventArgs e)
        {
            OptionsPopup.IsOpen = true;
        }
    }
}
