using Core;
using DiscUtils.Iso9660;
using Interprocess;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using static Core.Log;
using static Core.Win32;

namespace KTWirzade.GUI
{
    public partial class MainWindow : AcrylicWindow
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private bool loadingItem;

        private int ActiveItemIndex = GlobalsGUI.Current.Items.IndexOf(GlobalsGUI.Current.Items.FirstOrDefault(delegate (IDragItem x)
        {
            if (GlobalsGUI.Current.ISO != null)
            {
                return x.FileNameWithoutExtension == GlobalsGUI.Current.ISO.FileNameWithoutExtension;
            }
            return GlobalsGUI.Current.Playbook != null && x.FileNameWithoutExtension == GlobalsGUI.Current.Playbook.FileNameWithoutExtension;
        }));

        public static Dispatcher CurrentDispatcher;

        public static bool HasLoaded;

        private static BitmapImage scaledISOImage;

        internal MainWindowViewModel CurrentMainWindow => (MainWindowViewModel)base.DataContext;

        private ViewModelBase CurrentViewModel => CurrentMainWindow.CurrentViewModel;

        public async System.Threading.Tasks.Task LoadISO(string isoPath, ISO toBeRemoved = null)
        {
            loadingItem = true;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            if (toBeRemoved == null)
            {
                DragBox.SetResourceReference(StyleProperty, "DragBoxMainLoading");
                PBLoadContainer.Visibility = Visibility.Visible;
                PBLoadContainer.Children.Add(spinner);
            }
            bool nbe = NextButton.IsEnabled;
            bool pbe = PreviousButton.IsEnabled;
            bool cbe = CancelButton.IsEnabled;
            bool clbe = CloseButton.IsEnabled;
            bool ubbe = UpdatesButton.IsEnabled;
            CurrentMainWindow.NextButtonActive = false;
            CurrentMainWindow.PreviousButtonActive = false;
            CurrentMainWindow.CancelButtonActive = false;
            CurrentMainWindow.CloseButtonActive = false;
            CurrentMainWindow.UpdatesButtonActive = false;
            CurrentMainWindow.RemovePlaybookButtonActive = false;
            Storyboard board1 = new Storyboard();
            new RectAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 6, 0)),
                To = new Rect(0.0, 0.0, 60.0, 6.0)
            };
            ISO iso = null;
            try
            {
                if (!File.Exists(isoPath))
                {
                    throw new FileNotFoundException("Could not find ISO file.");
                }
                await System.Threading.Tasks.Task.Run(delegate
                {
                    long length = new FileInfo(isoPath).Length;
                    FileStream fileStream = File.Open(isoPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    try
                    {
                        iso = ImageParsers.Windows.TryGetInfo(fileStream);
                        if (iso == null)
                        {
                            CDReader value = Wrap.ExecuteSafe<CDReader>((Func<CDReader>)(() => new CDReader((Stream)fileStream, true)), false, (LogOptions)null).Value;
                            try
                            {
                                List<ImageParsers.IOSParser> list = new List<ImageParsers.IOSParser>();
                                ImageParsers.IOSParser[] oSParsers = ImageParsers.OSParsers;
                                bool flag = false;
                                ImageParsers.IOSParser[] array = oSParsers;
                                foreach (ImageParsers.IOSParser iOSParser in array)
                                {
                                    ISO iSO = iOSParser.MatchFileName(System.IO.Path.GetFileName(isoPath));
                                    if (iSO != null)
                                    {
                                        iso = iSO;
                                    }
                                    if (iSO != null && value != null)
                                    {
                                        iSO = iOSParser.TryGetInfo(value, System.IO.Path.GetFileName(isoPath), iso);
                                        if (iSO != null)
                                        {
                                            if (iso != null)
                                            {
                                                iSO.MergeFrom(iso);
                                            }
                                            iso = iSO;
                                            flag = true;
                                            break;
                                        }
                                        list.Add(iOSParser);
                                    }
                                }
                                if (!flag && value != null)
                                {
                                    foreach (ImageParsers.IOSParser item in oSParsers.Except(list))
                                    {
                                        ISO iSO2 = item.TryGetInfo(value, System.IO.Path.GetFileName(isoPath), iso);
                                        if (iSO2 != null)
                                        {
                                            iso = iSO2;
                                            break;
                                        }
                                    }
                                }
                                if (iso == null && value != null)
                                {
                                    iso = ImageParsers.Linux.TryGetInfo(value, System.IO.Path.GetFileName(isoPath));
                                }
                                if (iso == null)
                                {
                                    iso = ImageParsers.Linux.MatchFileName(System.IO.Path.GetFileName(isoPath));
                                }
                                if (iso == null)
                                {
                                    iso = ImageParsers.Unknown.TryGetInfo(System.IO.Path.GetFileName(isoPath));
                                }
                            }
                            finally
                            {
                                ((IDisposable)value)?.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        if (fileStream != null)
                        {
                            ((IDisposable)fileStream).Dispose();
                        }
                    }
                    if (iso != null)
                    {
                        iso.Size = length;
                    }
                });
            }
            catch (Exception ex)
            {
                board1.Stop();
                CurrentMainWindow.NextButtonActive = nbe;
                CurrentMainWindow.PreviousButtonActive = pbe;
                CurrentMainWindow.CancelButtonActive = cbe;
                CurrentMainWindow.CloseButtonActive = clbe;
                CurrentMainWindow.UpdatesButtonActive = ubbe;
                CurrentMainWindow.RemovePlaybookButtonActive = true;
                MessageBox.Show(this, "Error while attempting to load ISO: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning, $"[{ex.GetType()}]" + ex.StackTrace);
            }
            CurrentMainWindow.RemovePlaybookButtonActive = true;
            if (toBeRemoved == null)
            {
                PBLoadContainer.Visibility = Visibility.Collapsed;
                PBLoadContainer.Children.Remove(spinner);
                DragBox.Visibility = Visibility.Hidden;
                DragBox.SetResourceReference(StyleProperty, "DragBoxMain");
            }
            if (toBeRemoved != null)
            {
                GlobalsGUI.Current.Items.Remove(toBeRemoved);
            }
            if (iso != null)
            {
                iso.FilePath = isoPath;
                int isoIndex = GlobalsGUI.Current.Items.FindISOIndex((ISO x) => x.FilePath != null && string.Equals(System.IO.Path.GetFullPath(x.FilePath), System.IO.Path.GetFullPath(iso.FilePath), StringComparison.OrdinalIgnoreCase));
                if (isoIndex != -1)
                {
                    ((ISO)GlobalsGUI.Current.Items[isoIndex]).Watcher?.Dispose();
                    GlobalsGUI.Current.Items.RemoveAt(isoIndex);
                }
                iso.Watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(isoPath), System.IO.Path.GetFileName(isoPath))
                {
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName,
                    IncludeSubdirectories = false
                };
                iso.Watcher.Renamed += delegate (object sender, RenamedEventArgs args)
                {
                    ((FileSystemWatcher)sender).Filter = System.IO.Path.GetFileName(args.FullPath);
                    CurrentDispatcher.Invoke(() => iso.FilePath = args.FullPath);
                };
                iso.Watcher.Deleted += delegate (object sender, FileSystemEventArgs args)
                {
                    CurrentDispatcher.Invoke(() => GlobalsGUI.Current.Items.Remove(iso));
                    ((FileSystemWatcher)sender).Dispose();
                };
                iso.Checked = true;
                AddItem(iso);
                DragBox.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                DragBox.RenderTransform = new ScaleTransform
                {
                    ScaleX = 0.9,
                    ScaleY = 0.9
                };
                DoubleAnimation scale_x = new DoubleAnimation
                {
                    From = 0.9,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200.0)
                };
                DoubleAnimation scale_y = new DoubleAnimation
                {
                    From = 0.9,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200.0)
                };
                DragBox.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
                DragBox.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            }
            loadingItem = false;
            DragBox.Visibility = Visibility.Visible;
        }

        public void AddItem(IDragItem item)
        {
            GlobalsGUI.Current.Items.Add(item);
            if (item is PlaybookGUI)
            {
                GlobalsGUI.Current.Playbook = (PlaybookGUI)GlobalsGUI.Current.Items[GlobalsGUI.Current.Items.Count - 1];
            }
            else if (item is ISO)
            {
                GlobalsGUI.Current.ISO = (ISO)GlobalsGUI.Current.Items[GlobalsGUI.Current.Items.Count - 1];
            }
            if (GlobalsGUI.Current.Items.Count > 1 && ActiveItemIndex >= 0)
            {
                int i = 0;
                foreach (System.Windows.Shapes.Rectangle childPB in FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems))
                {
                    if (i == ActiveItemIndex)
                    {
                        Storyboard storyboard = new Storyboard();
                        DoubleAnimation doubleAnimation1 = new DoubleAnimation
                        {
                            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 0)),
                            To = 0.0
                        };
                        Storyboard.SetTarget(doubleAnimation1, childPB);
                        Storyboard.SetTargetProperty(doubleAnimation1, new PropertyPath("Height"));
                        storyboard.Children.Add(doubleAnimation1);
                        storyboard.Begin();
                    }
                    i++;
                }
                i = 0;
                foreach (Border childPB2 in from x in FindVisualChildren<Border>(PlaybookSidebarItems)
                                            where x.Name.ToString() == "PlaybookContainer"
                                            select x)
                {
                    if (i == ActiveItemIndex)
                    {
                        Storyboard storyboard2 = new Storyboard();
                        DoubleAnimation doubleAnimation2 = new DoubleAnimation
                        {
                            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 150)),
                            To = 0.0
                        };
                        Storyboard.SetTarget(doubleAnimation2, childPB2);
                        Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(Border.Background).(SolidColorBrush.Opacity)"));
                        storyboard2.Children.Add(doubleAnimation2);
                        storyboard2.Begin();
                    }
                    i++;
                }
                GlobalsGUI.Current.Items[ActiveItemIndex].Selected = false;
                GlobalsGUI.Current.Items[ActiveItemIndex].SidebarInitialHeight = 37;
            }
            item.SidebarInitialHeight = 37;
            item.Selected = true;
            ActiveItemIndex = GlobalsGUI.Current.Items.Count - 1;
            if (item is PlaybookGUI { VerificationStatus: var verificationStatus } && verificationStatus == PlaybookGUI.VerificationLevel.Malicious)
            {
                VerificationButton.Open();
            }
        }

        private async void RemoveItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            if ((IDragItem)((FrameworkElement)sender).DataContext is ISO iso)
            {
                iso.Watcher?.Dispose();
                if (iso.ProgressVisibility == Visibility.Visible)
                {
                    if (MessageBox.Show(this, "Cancel ISO download?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        GlobalsGUI.Current.Items.Remove(iso);
                    }
                    return;
                }
                if (iso.FilePath != null && iso.FilePath.StartsWith(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Images"), StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = System.IO.Path.GetFileName(iso.FilePath);
                    await InterLink.ExecuteSafeAsync((Expression<System.Action>)(() => RemoveISOFile(fileName)), true, -1);
                }
            }
            GlobalsGUI.Current.Items.Remove((IDragItem)((FrameworkElement)sender).DataContext);
        }

        [InterprocessMethod(Level.Administrator)]
        public static void RemoveISOFile(string fileName)
        {
            File.Delete(System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Images"), fileName));
        }

        private async void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Remove)
            {
                return;
            }
            IDragItem item = (IDragItem)e.OldItems[0];
            if (item is PlaybookGUI pb)
            {
                await RemovePlaybook(pb);
            }
            else if (item is ISO iso)
            {
                await RemoveISO(iso);
            }
            if (GlobalsGUI.Current.Items.Count > 0)
            {
                if (e.OldStartingIndex > ActiveItemIndex)
                {
                    return;
                }
                if (ActiveItemIndex - 1 >= 0)
                {
                    ActiveItemIndex--;
                }
                else
                {
                    ActiveItemIndex = GlobalsGUI.Current.Items.Count - 1;
                }
                if (ActiveItemIndex > GlobalsGUI.Current.Items.Count - 1)
                {
                    ActiveItemIndex = GlobalsGUI.Current.Items.Count - 1;
                }
                WizardConfig.Current.LastSelectedItem.Set(GlobalsGUI.Current.Items[ActiveItemIndex].FileNameWithoutExtension);
                int i = 0;
                foreach (System.Windows.Shapes.Rectangle childPB in FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems))
                {
                    if (i == ActiveItemIndex)
                    {
                        Storyboard storyboard = new Storyboard();
                        DoubleAnimation doubleAnimation1 = new DoubleAnimation
                        {
                            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 0)),
                            To = 37.0
                        };
                        Storyboard.SetTarget(doubleAnimation1, childPB);
                        Storyboard.SetTargetProperty(doubleAnimation1, new PropertyPath("Height"));
                        storyboard.Children.Add(doubleAnimation1);
                        storyboard.Begin();
                    }
                    i++;
                }
                i = 0;
                foreach (Border childPB2 in from x in FindVisualChildren<Border>(PlaybookSidebarItems)
                                            where x.Name == "PlaybookContainer"
                                            select x)
                {
                    if (i == ActiveItemIndex)
                    {
                        Storyboard storyboard2 = new Storyboard();
                        DoubleAnimation doubleAnimation2 = new DoubleAnimation
                        {
                            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 150)),
                            To = 0.04
                        };
                        Storyboard.SetTarget(doubleAnimation2, childPB2);
                        Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(Border.Background).(SolidColorBrush.Opacity)"));
                        storyboard2.Children.Add(doubleAnimation2);
                        storyboard2.Begin();
                    }
                    i++;
                }
                GlobalsGUI.Current.Items[ActiveItemIndex].Selected = true;
                GlobalsGUI.Current.Items[ActiveItemIndex].SidebarInitialHeight = 37;
                if (GlobalsGUI.Current.Items[ActiveItemIndex] is PlaybookGUI)
                {
                    GlobalsGUI.Current.Playbook = (PlaybookGUI)GlobalsGUI.Current.Items[ActiveItemIndex];
                }
                else if (GlobalsGUI.Current.Items[ActiveItemIndex] is ISO)
                {
                    GlobalsGUI.Current.ISO = (ISO)GlobalsGUI.Current.Items[ActiveItemIndex];
                }
            }
            else
            {
                if (item is PlaybookGUI)
                {
                    GlobalsGUI.Current.Playbook = null;
                }
                else if (item is ISO)
                {
                    GlobalsGUI.Current.ISO = null;
                }
                ActiveItemIndex = -1;
            }
        }

        private static async System.Threading.Tasks.Task RemovePlaybook(PlaybookGUI pb)
        {
            if (!(((Playbook)pb).Path != "Ignore"))
            {
                return;
            }
            try
            {
                await InterLink.ExecuteAsync(() => App.RemovePlaybookAdmin(pb.FileNameWithoutExtension + ".apbx"), false, -1);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(null, "Error while removing Playbook files: " + ex.Message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static async System.Threading.Tasks.Task RemoveISO(ISO iso)
        {
        }

        private async void SelectItem(object sender, RoutedEventArgs e)
        {
            if (!GlobalsGUI.Current.Items.Any() || loadingItem)
            {
                return;
            }
            IDragItem selectedItem = GlobalsGUI.Current.Items.FirstOrDefault((IDragItem x) => x == ((FrameworkElement)sender).DataContext);
            if (selectedItem == null)
            {
                return;
            }
            int newIndex = GlobalsGUI.Current.Items.IndexOf(selectedItem);
            if (newIndex == ActiveItemIndex)
            {
                return;
            }
            GlobalsGUI.Current.Items[newIndex].Selected = true;
            if (selectedItem is PlaybookGUI)
            {
                GlobalsGUI.Current.Playbook = (PlaybookGUI)selectedItem;
            }
            else if (selectedItem is ISO)
            {
                GlobalsGUI.Current.ISO = (ISO)selectedItem;
            }
            WizardConfig.Current.LastSelectedItem.Set(GlobalsGUI.Current.Items[newIndex].FileNameWithoutExtension);
            int i = 0;
            foreach (Border childPB in from x in FindVisualChildren<Border>(PlaybookSidebarItems)
                                       where x.Name == "PlaybookContainer"
                                       select x)
            {
                if (i == newIndex)
                {
                    i++;
                    continue;
                }
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation1 = new DoubleAnimation
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 150)),
                    To = 0.0
                };
                Storyboard.SetTarget(doubleAnimation1, childPB);
                Storyboard.SetTargetProperty(doubleAnimation1, new PropertyPath("(Border.Background).(SolidColorBrush.Opacity)"));
                storyboard.Children.Add(doubleAnimation1);
                storyboard.Begin();
                i++;
            }
            System.Windows.Shapes.Rectangle ActivePB = new();
            System.Windows.Shapes.Rectangle SelectedPB = FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).ElementAt(newIndex);
            if (ActiveItemIndex != -1)
            {
                ActivePB = FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).ElementAt(ActiveItemIndex);
            }
            Storyboard board = new Storyboard();
            Thickness origMargin = new Thickness(0.0, 25.0, 0.0, 0.0);
            int origHeight = 37;
            if (newIndex > ActiveItemIndex)
            {
                DoubleAnimationUsingKeyFrames mainAnim1 = new DoubleAnimationUsingKeyFrames();
                mainAnim1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                DoubleKeyFrame anim1Key1 = new LinearDoubleKeyFrame
                {
                    Value = 62.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                };
                DoubleKeyFrame anim1Key2 = new LinearDoubleKeyFrame
                {
                    Value = 62.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                };
                DoubleKeyFrame anim1Key3 = new LinearDoubleKeyFrame
                {
                    Value = 0.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                mainAnim1.KeyFrames.Add(anim1Key1);
                mainAnim1.KeyFrames.Add(anim1Key2);
                mainAnim1.KeyFrames.Add(anim1Key3);
                ThicknessAnimationUsingKeyFrames marginAnim1 = new ThicknessAnimationUsingKeyFrames();
                marginAnim1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 40));
                marginAnim1.BeginTime = new TimeSpan(0, 0, 0, 0, 240);
                ThicknessKeyFrame marginAnim1Key1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 85.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                };
                ThicknessKeyFrame marginAnim1KeyDelay = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 85.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame marginAnim1Key2 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 25.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                marginAnim1.KeyFrames.Add(marginAnim1Key1);
                marginAnim1.KeyFrames.Add(marginAnim1KeyDelay);
                marginAnim1.KeyFrames.Add(marginAnim1Key2);
                DoubleAnimationUsingKeyFrames mainAnim2 = new DoubleAnimationUsingKeyFrames();
                mainAnim2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, (ActiveItemIndex != -1) ? 200 : 0));
                mainAnim2.BeginTime = new TimeSpan(0, 0, 0, 0, (ActiveItemIndex != -1) ? 200 : 0);
                DoubleKeyFrame anim2Key1 = new LinearDoubleKeyFrame
                {
                    Value = ((ActiveItemIndex == -1) ? origHeight : 62),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                DoubleKeyFrame anim2Key2 = new LinearDoubleKeyFrame
                {
                    Value = origHeight,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                };
                mainAnim2.KeyFrames.Add(anim2Key1);
                mainAnim2.KeyFrames.Add(anim2Key2);
                ThicknessAnimation marginAnim2 = new ThicknessAnimation();
                marginAnim2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, (ActiveItemIndex != -1) ? 200 : 0));
                marginAnim2.BeginTime = new TimeSpan(0, 0, 0, 0, (ActiveItemIndex != -1) ? 200 : 0);
                marginAnim2.From = new Thickness(0.0, 0.0, 0.0, 0.0);
                marginAnim2.To = origMargin;
                if (ActiveItemIndex != -1)
                {
                    Storyboard.SetTarget(mainAnim1, ActivePB);
                    Storyboard.SetTargetProperty(mainAnim1, new PropertyPath("Height"));
                    Storyboard.SetTarget(marginAnim1, ActivePB);
                    Storyboard.SetTargetProperty(marginAnim1, new PropertyPath("Margin"));
                }
                Storyboard.SetTarget(mainAnim2, SelectedPB);
                Storyboard.SetTargetProperty(mainAnim2, new PropertyPath("Height"));
                Storyboard.SetTarget(marginAnim2, SelectedPB);
                Storyboard.SetTargetProperty(marginAnim2, new PropertyPath("Margin"));
                if (ActiveItemIndex != -1)
                {
                    board.Children.Add(mainAnim1);
                    board.Children.Add(marginAnim1);
                }
                board.Children.Add(mainAnim2);
                board.Children.Add(marginAnim2);
                board.Begin();
            }
            else
            {
                DoubleAnimationUsingKeyFrames mainAnim3 = new DoubleAnimationUsingKeyFrames();
                mainAnim3.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                DoubleKeyFrame anim1Key4 = new LinearDoubleKeyFrame
                {
                    Value = 62.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                };
                DoubleKeyFrame anim1Key5 = new LinearDoubleKeyFrame
                {
                    Value = 62.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                };
                DoubleKeyFrame anim1Key6 = new LinearDoubleKeyFrame
                {
                    Value = 0.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                mainAnim3.KeyFrames.Add(anim1Key4);
                mainAnim3.KeyFrames.Add(anim1Key5);
                mainAnim3.KeyFrames.Add(anim1Key6);
                ThicknessAnimationUsingKeyFrames marginAnim3 = new ThicknessAnimationUsingKeyFrames();
                marginAnim3.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                ThicknessKeyFrame marginAnim1Key3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                };
                ThicknessKeyFrame marginAnim1KeyDelay2 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame marginAnim1Key4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 25.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                marginAnim3.KeyFrames.Add(marginAnim1Key3);
                marginAnim3.KeyFrames.Add(marginAnim1KeyDelay2);
                marginAnim3.KeyFrames.Add(marginAnim1Key4);
                DoubleAnimationUsingKeyFrames mainAnim4 = new DoubleAnimationUsingKeyFrames();
                mainAnim4.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                mainAnim4.BeginTime = new TimeSpan(0, 0, 0, 0, 200);
                DoubleKeyFrame anim2Key3 = new LinearDoubleKeyFrame
                {
                    Value = 62.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                DoubleKeyFrame anim2Key4 = new LinearDoubleKeyFrame
                {
                    Value = origHeight,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                };
                mainAnim4.KeyFrames.Add(anim2Key3);
                mainAnim4.KeyFrames.Add(anim2Key4);
                ThicknessAnimation setMargin2 = new ThicknessAnimation();
                setMargin2.Duration = new Duration(new TimeSpan(0L));
                setMargin2.To = new Thickness(0.0, 25.0, 0.0, 0.0);
                Storyboard.SetTarget(mainAnim3, ActivePB);
                Storyboard.SetTargetProperty(mainAnim3, new PropertyPath("Height"));
                Storyboard.SetTarget(marginAnim3, ActivePB);
                Storyboard.SetTargetProperty(marginAnim3, new PropertyPath("Margin"));
                Storyboard.SetTarget(mainAnim4, SelectedPB);
                Storyboard.SetTargetProperty(mainAnim4, new PropertyPath("Height"));
                Storyboard.SetTarget(setMargin2, SelectedPB);
                Storyboard.SetTargetProperty(setMargin2, new PropertyPath("Margin"));
                board.Children.Add(mainAnim3);
                board.Children.Add(marginAnim3);
                board.Children.Add(mainAnim4);
                board.Children.Add(setMargin2);
                board.Begin();
                ActivePB.Margin = new Thickness(0.0, 25.0, 0.0, 0.0);
            }
            if (ActiveItemIndex != -1)
            {
                GlobalsGUI.Current.Items[ActiveItemIndex].Selected = false;
            }
            ActiveItemIndex = newIndex;
        }

        private async void DragBox_OnClick(object sender, RoutedEventArgs e)
        {
            if (loadingItem)
            {
                return;
            }
            if (ActiveItemIndex != -1)
            {
                GlobalsGUI.Current.Items[ActiveItemIndex].Selected = false;
                System.Windows.Shapes.Rectangle activePB = FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).ElementAt(ActiveItemIndex);
                Storyboard board1 = new Storyboard();
                foreach (Border childPB in from x in FindVisualChildren<Border>(PlaybookSidebarItems)
                                           where x.Name == "PlaybookContainer"
                                           select x)
                {
                    DoubleAnimation doubleAnimation1 = new DoubleAnimation();
                    doubleAnimation1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 150));
                    doubleAnimation1.To = 0.0;
                    Storyboard.SetTarget(doubleAnimation1, childPB);
                    Storyboard.SetTargetProperty(doubleAnimation1, new PropertyPath("(Border.Background).(SolidColorBrush.Opacity)"));
                    board1.Children.Add(doubleAnimation1);
                }
                DoubleAnimation doubleAnimation2 = new DoubleAnimation();
                doubleAnimation2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 0));
                doubleAnimation2.To = 0.0;
                Storyboard.SetTarget(doubleAnimation2, activePB);
                Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("Height"));
                board1.Children.Add(doubleAnimation2);
                board1.Begin();
            }
            GlobalsGUI.Current.Playbook = null;
            GlobalsGUI.Current.ISO = null;
            ActiveItemIndex = -1;
        }

        private async void DragBox_OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            _ = 1;
            try
            {
                if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, autoConvert: true))
                {
                    return;
                }
                string[] files = e.Data.GetData(System.Windows.DataFormats.FileDrop, autoConvert: true) as string[];
                string[] array = files;
                foreach (string file in array)
                {
                    string extension = System.IO.Path.GetExtension(file).ToLower();
                    if (new string[5] { ".iso", ".img", ".gz", ".bz2", ".bzip2" }.Contains(extension))
                    {
                        await LoadISO(file);
                    }
                    else
                    {
                        await LoadPlaybook(file);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ensure you've updated KT WIRZADE, and contact your Playbook creator for further assistance.", "Error loading item", MessageBoxButton.OK, MessageBoxImage.Warning, ex.ToString());
            }
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

        public async System.Threading.Tasks.Task LoadPlaybook(string apbx)
        {
            loadingItem = true;
            DragBox.SetResourceReference(StyleProperty, "DragBoxMainLoading");
            PBLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            PBLoadContainer.Children.Add(spinner);
            bool nbe = NextButton.IsEnabled;
            bool pbe = PreviousButton.IsEnabled;
            bool cbe = CancelButton.IsEnabled;
            bool clbe = CloseButton.IsEnabled;
            bool ubbe = UpdatesButton.IsEnabled;
            CurrentMainWindow.NextButtonActive = false;
            CurrentMainWindow.PreviousButtonActive = false;
            CurrentMainWindow.CancelButtonActive = false;
            CurrentMainWindow.CloseButtonActive = false;
            CurrentMainWindow.UpdatesButtonActive = false;
            CurrentMainWindow.RemovePlaybookButtonActive = false;
            Storyboard board1 = new Storyboard();
            new RectAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 6, 0)),
                To = new Rect(0.0, 0.0, 60.0, 6.0)
            };
            PlaybookGUI pb = null;
            try
            {
                pb = await System.Threading.Tasks.Task.Run(() => APBX.ImportAPBX(apbx));
                if (pb == null)
                {
                    board1.Pause();
                    if (MessageBox.Show(this, "Selected Playbook already exists. Overwrite?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        board1.Resume();
                        pb = await System.Threading.Tasks.Task.Run(() => APBX.ImportAPBX(apbx, overwrite: true));
                        int pbIndex = GlobalsGUI.Current.Items.FindPlaybookIndex((PlaybookGUI x) => (((Playbook)x).UniqueId.HasValue && ((Playbook)pb).UniqueId.HasValue && ((Playbook)x).UniqueId == ((Playbook)pb).UniqueId) || ((!((Playbook)x).UniqueId.HasValue || !((Playbook)pb).UniqueId.HasValue) && ((Playbook)x).Name == ((Playbook)pb).Name && ((Playbook)x).Username == ((Playbook)pb).Username));
                        if (pbIndex != -1)
                        {
                            ((Playbook)(PlaybookGUI)GlobalsGUI.Current.Items[pbIndex]).Path = "Ignore";
                            GlobalsGUI.Current.Items.RemoveAt(pbIndex);
                        }
                        if (string.IsNullOrEmpty(((Playbook)pb).Path))
                        {
                            pb = null;
                            ActiveItemIndex = -1;
                            if (GlobalsGUI.Current.Playbook != null)
                            {
                                GlobalsGUI.Current.Playbook.Selected = false;
                                GlobalsGUI.Current.Playbook.SidebarInitialHeight = 0;
                                GlobalsGUI.Current.Playbook = null;
                            }
                            throw new Exception("Could not remove existing Playbook files.");
                        }
                        ((Playbook)pb).Path = null;
                    }
                    else
                    {
                        board1.Stop();
                        CurrentMainWindow.NextButtonActive = nbe;
                        CurrentMainWindow.PreviousButtonActive = pbe;
                        CurrentMainWindow.CancelButtonActive = cbe;
                        CurrentMainWindow.CloseButtonActive = clbe;
                        CurrentMainWindow.UpdatesButtonActive = ubbe;
                        CurrentMainWindow.RemovePlaybookButtonActive = true;
                    }
                }
            }
            catch (Exception ex)
            {
                board1.Stop();
                CurrentMainWindow.NextButtonActive = nbe;
                CurrentMainWindow.PreviousButtonActive = pbe;
                CurrentMainWindow.CancelButtonActive = cbe;
                CurrentMainWindow.CloseButtonActive = clbe;
                CurrentMainWindow.UpdatesButtonActive = ubbe;
                CurrentMainWindow.RemovePlaybookButtonActive = true;
                Log.EnqueueExceptionSafe(ex, Array.Empty<(string, object)>());
                MessageBox.Show(this, "Error while attempting to load Playbook: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            CurrentMainWindow.RemovePlaybookButtonActive = true;
            PBLoadContainer.Visibility = Visibility.Collapsed;
            PBLoadContainer.Children.Remove(spinner);
            DragBox.Visibility = Visibility.Hidden;
            DragBox.SetResourceReference(StyleProperty, "DragBoxMain");
            if (pb != null)
            {
                pb.Checked = true;
                AddItem(pb);
                DragBox.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                DragBox.RenderTransform = new ScaleTransform
                {
                    ScaleX = 0.9,
                    ScaleY = 0.9
                };
                DoubleAnimation scale_x = new DoubleAnimation
                {
                    From = 0.9,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200.0)
                };
                DoubleAnimation scale_y = new DoubleAnimation
                {
                    From = 0.9,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200.0)
                };
                DragBox.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
                DragBox.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            }
            loadingItem = false;
            DragBox.Visibility = Visibility.Visible;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public MainWindow()
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                base.Style = (Style)System.Windows.Application.Current.FindResource("VMwareMainWindow");
            }
            else
            {
                base.Style = (Style)System.Windows.Application.Current.FindResource("Window");
            }
            try
            {
                TaskService.Instance.RootFolder.DeleteTask("KTWirzade", false);
            }
            catch (Exception)
            {
            }
            base.DataContext = new MainWindowViewModel();
            InitializeComponent();
            CurrentViewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(() => NextButton_OnClick(), () => true);
            CurrentViewModel.MainPreviousButtonCommand = new GlobalsGUI.CommandHandler(() => PreviousButton_OnClick(), () => true);
            CurrentViewModel.MainPulseNextButtonCommand = new GlobalsGUI.CommandHandler(() => NextButton_OnClick(), () => true);
            CurrentViewModel.MainCancelButtonCommand = new GlobalsGUI.CommandHandler(() => CancelButton_OnClick(), () => true);
            CurrentViewModel.MainCloseButtonCommand = new GlobalsGUI.CommandHandler(() => CancelButton_OnClick(), () => true);
            CurrentMainWindow.PropertyChanged += delegate (object _e, PropertyChangedEventArgs _a)
            {
                if (_a.PropertyName == "NextButtonStyle")
                {
                    if (CurrentMainWindow.NextButtonStyle == ViewModelBase.MainNextButtonStyles.Normal)
                    {
                        NextButton.SetResourceReference(StyleProperty, "BasicButton");
                    }
                    if (CurrentMainWindow.NextButtonStyle == ViewModelBase.MainNextButtonStyles.Pulse)
                    {
                        NextButton.SetResourceReference(StyleProperty, "PulseButton");
                    }
                }
                if (_a.PropertyName == "PlaybookColumnVisibility")
                {
                    if (CurrentMainWindow.PlaybookColumnVisibility == Visibility.Hidden)
                    {
                        PlaybooksColumn.Visibility = Visibility.Hidden;
                    }
                    if (CurrentMainWindow.PlaybookColumnVisibility == Visibility.Collapsed)
                    {
                        PlaybooksColumn.Visibility = Visibility.Collapsed;
                        ViewModelBorder.BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0);
                        ViewModelBorder.CornerRadius = new CornerRadius(0.0, 0.0, 0.0, 0.0);
                    }
                    if (CurrentMainWindow.PlaybookColumnVisibility == Visibility.Visible)
                    {
                        PlaybooksColumn.Visibility = Visibility.Visible;
                        ViewModelBorder.BorderThickness = new Thickness(1.0, 1.0, 0.0, 0.0);
                        ViewModelBorder.CornerRadius = new CornerRadius(8.0, 0.0, 0.0, 0.0);
                    }
                }
            };
            PlaybookSidebarItems.ItemsSource = GlobalsGUI.Current.Items;
            CurrentDispatcher = Dispatcher.CurrentDispatcher;
            App.PreparationCompleted += delegate
            {
                GlobalsGUI.Current.Items.CollectionChanged += ItemsOnCollectionChanged;
                foreach (IDragItem current in GlobalsGUI.Current.Items.Where((IDragItem x) => !x.Checked).ToList())
                {
                    GlobalsGUI.Current.Items.Remove(current);
                }
            };
            base.Loaded += async delegate
            {
                HasLoaded = true;
                if (GlobalsGUI.Current.Playbook != null)
                {
                    GlobalsGUI.Current.Playbook.CurrentPage = new LoadPageViewModel();
                }
                else if (GlobalsGUI.Current.ISO != null)
                {
                    GlobalsGUI.Current.ISO.CurrentPage = new LoadPageViewModel();
                }
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic);
                Activate();
                base.Topmost = true;
                base.Topmost = false;
                Focus();
                if (App.DeCrippleDefender)
                {
                    DeCrippleLoadContainer.Visibility = Visibility.Visible;
                    Spinner spinner = new Spinner
                    {
                        Foreground = System.Windows.Media.Brushes.White
                    };
                    DeCrippleLoadContainer.Children.Add(spinner);
                    System.Threading.Tasks.Task wait = System.Threading.Tasks.Task.Delay(3000);
                    try
                    {
                        await InterLink.ExecuteDisposableAsync((TargetLevel)3, (Expression<Func<string, int>>)((string arguments) => Defender.StartElevatedProcess(ProcessEx.GetCurrentProcessFileLocation(), arguments)), (Expression<System.Action>)(() => Defender.DeCripple()), 60000, false);
                    }
                    catch (Exception ex2)
                    {
                        Log.EnqueueExceptionSafe(ex2, Array.Empty<(string, object)>());
                        MessageBox.Show(this, "Could not remove Windows Defender. Check the log and contact the team for more information and assistance.", "Warning.", MessageBoxButton.ShowLog, MessageBoxImage.Warning, Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade\\Logs"));
                    }
                    await wait;
                    DeCrippleLoadContainer.Visibility = Visibility.Collapsed;
                    DeCrippleLoadContainer.Children.Remove(spinner);
                    App.DeCrippleDefender = false;
                }
            };
            base.ContentRendered += async delegate
            {
                SetForegroundWindow(new WindowInteropHelper(this).Handle);
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic);
                string[] arguments = Environment.GetCommandLineArgs();
                if (arguments.Length == 2 && arguments[1] == "--updated")
                {
                    //new UpdatesDialog().ShowDialog(this);
                }
            };
            PatreonPopup.Opened += async delegate (object sender, EventArgs args)
            {
                IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(PatreonPopup.Child)).Handle;
                if (GetWindowRect(hwnd, out var rect))
                {
                    SetWindowPos(hwnd, -2, rect.Left, rect.Top, (int)base.Width, (int)base.Height, 0);
                }
                try
                {
                    SetForegroundWindow(new WindowInteropHelper(this).Handle);
                }
                catch
                {
                }
                PatreonControl.OnOpened(this, args);
            };

            System.Windows.MessageBox.Show(Assembly.GetExecutingAssembly().GetName().Name);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        private async void CancelButton_OnClick()
        {
            if (!(CurrentViewModel is IntroPageViewModel) && !(CurrentViewModel is SelectPageViewModel) && !(CurrentViewModel is LoadPageViewModel) && !(CurrentViewModel is IsoPageViewModel) && MessageBox.Show(this, "Are you sure you want to exit the process?", GlobalsGUI.AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            IDragItem progressingItem = GlobalsGUI.Current.Items.FirstOrDefault((IDragItem x) => x.ProgressVisibility == Visibility.Visible);
            if (progressingItem != null)
            {
                if (MessageBox.Show(this, "Cancel ISO download?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    GlobalsGUI.Current.Items.Remove(progressingItem);
                }
            }
            else
            {
                await CloseWindow(windowscale);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void AboutButton_OnClick(object sender, RoutedEventArgs e)
        {
            PatreonPopup.IsOpen = true;
        }

        public void NextButton_OnClick()
        {
            CurrentMainWindow.CurrentViewModel = CurrentViewModel.GetNextPage(CurrentMainWindow.state);
            if (GlobalsGUI.Current.Playbook != null)
            {
                GlobalsGUI.Current.Playbook.CurrentPage = CurrentMainWindow.CurrentViewModel;
            }
            else if (GlobalsGUI.Current.ISO != null)
            {
                GlobalsGUI.Current.ISO.CurrentPage = CurrentMainWindow.CurrentViewModel;
            }
        }

        private void PreviousButton_OnClick()
        {
            CurrentMainWindow.CurrentViewModel = CurrentViewModel.GetPreviousPage(CurrentMainWindow.state);
            if (GlobalsGUI.Current.Playbook != null)
            {
                GlobalsGUI.Current.Playbook.CurrentPage = CurrentMainWindow.CurrentViewModel;
            }
            else if (GlobalsGUI.Current.ISO != null)
            {
                GlobalsGUI.Current.ISO.CurrentPage = CurrentMainWindow.CurrentViewModel;
            }
        }

        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void UpdatesButton_OnClick(object sender, RoutedEventArgs e)
        {
            //new UpdatesDialog().ShowDialog(this);
        }

        private void ItemImage_OnLoaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Image image = (System.Windows.Controls.Image)sender;
            if ((IDragItem)image.DataContext is ISO iso)
            {
                image.Height = 32.0;
                image.Width = 32.0;
                image.UpdateLayout();
                if (scaledISOImage != null)
                {
                    image.Source = scaledISOImage;
                    iso.Icon = scaledISOImage;
                    return;
                }
                int height = (int)Math.Round(VisualTreeHelper.GetDpi(image).DpiScaleY * image.ActualHeight);
                scaledISOImage = GUIUtil.GetIconResource(Environment.ExpandEnvironmentVariables("%SYSTEMROOT%\\System32\\imageres.dll"), -5205, height, height);
                image.Source = scaledISOImage;
                iso.Icon = scaledISOImage;
            }
        }
    }
}