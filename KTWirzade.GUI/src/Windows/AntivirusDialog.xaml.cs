using Interprocess;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq.Expressions;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using static Core.Win32;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace KTWirzade.GUI.Windows
{
    public partial class AntivirusDialog : AcrylicWindow
    {
        private class UninstallKey
        {
            public string DisplayName;

            public string SecurityName;

            public string InstallLocation;

            public string UninstallString;

            public string RootKey;

            public bool Remnant;
        }

        private struct AVPair
        {
            public string UninstallName;

            public string SecurityName;

            public string KeyOverride;
        }

        private struct ImageParams
        {
            internal int scaleY;

            internal int scaleX;

            internal BitmapImage raw;
        }

        private List<string> FoundAVKeys = new List<string>();

        private List<AVPair> Pairs = new List<AVPair>
    {
        new AVPair
        {
            UninstallName = "McAfee® Total Protection",
            SecurityName = "McAfee VirusScan",
            KeyOverride = null
        },
        new AVPair
        {
            UninstallName = "McAfee®",
            SecurityName = "McAfee VirusScan",
            KeyOverride = null
        },
        new AVPair
        {
            UninstallName = "Avira Security",
            SecurityName = "Avira Security",
            KeyOverride = "AviraSecurityUninstaller"
        }
    };

        private bool onlyRemnants = true;

        private bool RemnantsIgnored;

        private UninstallKey CurrentAV;

        private bool animating;

        private System.Diagnostics.Process UninstallProcess;

        private List<UninstallKey> GetAVs()
        {
            try
            {
                List<UninstallKey> Antiviruses = new List<UninstallKey>();
                List<UninstallKey> UninstallKeys = new List<UninstallKey>();
                RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                string[] subKeyNames = key.GetSubKeyNames();
                foreach (string subKey in subKeyNames)
                {
                    UninstallKeys.Add(new UninstallKey
                    {
                        RootKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey
                    });
                }
                key.Close();
                key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                subKeyNames = key.GetSubKeyNames();
                foreach (string subKey2 in subKeyNames)
                {
                    UninstallKeys.Add(new UninstallKey
                    {
                        RootKey = "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey2
                    });
                }
                key.Close();
                foreach (UninstallKey uninstallKey in UninstallKeys)
                {
                    try
                    {
                        RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(uninstallKey.RootKey);
                        object val = registryKey.GetValue("DisplayName");
                        uninstallKey.DisplayName = ((val == null) ? null : ((string)val));
                        val = registryKey.GetValue("UninstallString");
                        uninstallKey.UninstallString = ((val == null) ? null : ((string)val));
                        val = registryKey.GetValue("InstallLocation");
                        uninstallKey.InstallLocation = ((val == null) ? null : ((string)val));
                        registryKey.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
                UninstallKeys.RemoveAll((UninstallKey x) => (string.IsNullOrEmpty(x.InstallLocation) && !Pairs.Any((AVPair pair) => pair.KeyOverride != null && x.RootKey.EndsWith(pair.KeyOverride, StringComparison.OrdinalIgnoreCase))) || string.IsNullOrEmpty(x.DisplayName));
                string computer = Environment.MachineName;
                string scope = "\\\\" + computer + "\\root\\SecurityCenter2";
                string query = "SELECT * FROM AntivirusProduct WHERE displayName != \"Windows Defender\"";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        ManagementObject result = (ManagementObject)item;
                        object value = result["productState"];
                        Hex(System.Convert.ToInt32(value));
                        string value2 = Reverse(Binary(System.Convert.ToInt32(value)));
                        bool enabled = GetBit(value2, 18);
                        GetBit(value2, 12);
                        GetBit(value2, 4);
                        string name = result["displayName"].ToString();
                        if (!enabled)
                        {
                            continue;
                        }
                        string exe = result["pathToSignedProductExe"].ToString();
                        UninstallKey match = UninstallKeys.FirstOrDefault((UninstallKey x) => Pairs.Any((AVPair pair) => pair.KeyOverride != null && x.RootKey.EndsWith(pair.KeyOverride, StringComparison.OrdinalIgnoreCase) && pair.SecurityName == name));
                        if (match == null)
                        {
                            match = UninstallKeys.FirstOrDefault((UninstallKey x) => Pairs.Any((AVPair pair) => pair.UninstallName == x.DisplayName && pair.SecurityName == name));
                        }
                        if (match == null)
                        {
                            match = UninstallKeys.FirstOrDefault(delegate (UninstallKey x)
                            {
                                string text = x.InstallLocation.TrimEnd('\\').TrimStart('\\', '?');
                                return exe.TrimStart('\\', '?').StartsWith(text + "\\", StringComparison.OrdinalIgnoreCase);
                            });
                        }
                        if (!File.Exists(exe) && (match == null || !Pairs.Any((AVPair pair) => pair.UninstallName == match.DisplayName && pair.SecurityName == name)))
                        {
                            Antiviruses.Add(new UninstallKey
                            {
                                DisplayName = name,
                                Remnant = true,
                                SecurityName = name
                            });
                        }
                        else if (match != null)
                        {
                            FoundAVKeys.Add(name);
                            match.SecurityName = name;
                            Antiviruses.Add(match);
                        }
                        else if (FoundAVKeys.Contains(name))
                        {
                            InterLink.Execute((Expression<System.Action>)(() => EraseRemnant(name)), false, -1);
                        }
                        else
                        {
                            Antiviruses.Add(new UninstallKey
                            {
                                DisplayName = name,
                                SecurityName = name
                            });
                        }
                    }
                }
                return Antiviruses;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [InterprocessMethod(Level.Administrator)]
        private static void EraseRemnant(string name)
        {
            string computer = Environment.MachineName;
            string scope = "\\\\" + computer + "\\root\\SecurityCenter2";
            string query = "SELECT * FROM AntivirusProduct WHERE displayName = \"" + name + "\"";
            using ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject item in searcher.Get())
            {
                item.Delete();
            }
        }

        private static string Binary(int value)
        {
            return System.Convert.ToString(value, 2).PadLeft(24, '0');
        }

        private static string Hex(int value)
        {
            return System.Convert.ToString(value, 16).PadLeft(6, '0');
        }

        private static bool GetBit(string value, int index)
        {
            return value.Substring(index, 1).Equals("1");
        }

        private static string Reverse(string value)
        {
            return new string(value.Reverse().ToArray());
        }

        public AntivirusDialog()
        {
            InitializeComponent();
            base.Loaded += OnLoaded;
        }

        public static BitmapSource Convert(Bitmap bitmap)
        {
            using MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0L;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using MemoryStream outStream = new MemoryStream();
            PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
            pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            pngBitmapEncoder.Save(outStream);
            return new Bitmap(new Bitmap(outStream));
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
                PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            double ppuX = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            double ppuY = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M22;
            List<ImageParams> images = new List<ImageParams>();
            List<System.Windows.Controls.Image> raw = (from image in FindVisualChildren<System.Windows.Controls.Image>(LeftContainer)
                                                       where image.Name != null && !image.Name.Contains("Icon")
                                                       select image).ToList();
            raw.ForEach(delegate (System.Windows.Controls.Image x)
            {
                images.Add(new ImageParams
                {
                    raw = (BitmapImage)FindResource(x.Name + "Bitmap"),
                    scaleY = (int)(ppuY * x.ActualHeight),
                    scaleX = (int)(ppuX * x.ActualWidth)
                });
            });
            List<BitmapSource> sources = new List<BitmapSource>();
            await System.Threading.Tasks.Task.Run(delegate
            {
                foreach (ImageParams current in images)
                {
                    sources.Add(Convert(ImageUtilities.ResizeImage(BitmapImage2Bitmap(current.raw), current.scaleX, current.scaleY)));
                }
            });
            int i = 0;
            foreach (BitmapSource source in sources)
            {
                raw[i].Source = source;
                i++;
            }
            ProgressBar.Start();
            Storyboard board = new Storyboard();
            ThicknessAnimationUsingKeyFrames pageScrollAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(8300.0)),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame(Scroller.Margin, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700.0))),
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050.0)),
                    Value = new Thickness(0.0, -50.0, 0.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1550.0)),
                    Value = new Thickness(0.0, -50.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900.0)),
                    Value = new Thickness(0.0, -100.0, 43.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, -100.0, 43.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(Scroller.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(pageScrollAnim, Scroller);
            Storyboard.SetTargetProperty(pageScrollAnim, new PropertyPath("Margin"));
            board.Children.Add(pageScrollAnim);
            ThicknessAnimationUsingKeyFrames scrollBarAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(8300.0)),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame(AppsScrollBar.Margin, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700.0))),
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050.0)),
                    Value = new Thickness(0.0, 61.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1550.0)),
                    Value = new Thickness(0.0, 61.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900.0)),
                    Value = new Thickness(0.0, 71.0, 43.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 71.0, 43.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(AppsScrollBar.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(scrollBarAnim, AppsScrollBar);
            Storyboard.SetTargetProperty(scrollBarAnim, new PropertyPath("Margin"));
            board.Children.Add(scrollBarAnim);
            ThicknessAnimationUsingKeyFrames cursorAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(5900.0)),
                BeginTime = TimeSpan.FromMilliseconds(2400.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(297.0, 258.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(328.0, 257.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(328.0, 257.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000.0))
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(322.0, 277.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 286.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 286.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4200.0))
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 293.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4400.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 300.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4600.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame(new Thickness(310.0, 300.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(MouseCursor.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(cursorAnim, MouseCursor);
            Storyboard.SetTargetProperty(cursorAnim, new PropertyPath("Margin"));
            board.Children.Add(cursorAnim);
            DoubleAnimationUsingKeyFrames dotsHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(5100.0)),
                BeginTime = TimeSpan.FromMilliseconds(3200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400.0)),
                    Value = 0.5
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(DotsHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(dotsHoverAnim, DotsHoverEffect);
            Storyboard.SetTargetProperty(dotsHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(dotsHoverAnim);
            ThicknessAnimationUsingKeyFrames modifyDropdownAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4700.0)),
                BeginTime = TimeSpan.FromMilliseconds(3600.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250.0)),
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 0.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(SettingsAppsModifyBox.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyDropdownAnim, SettingsAppsModifyBox);
            Storyboard.SetTargetProperty(modifyDropdownAnim, new PropertyPath("Margin"));
            board.Children.Add(modifyDropdownAnim);
            DoubleAnimationUsingKeyFrames modifyFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4700.0)),
                BeginTime = TimeSpan.FromMilliseconds(3600.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(SettingsAppsModifyBox.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyFadeAnim, SettingsAppsModifyBox);
            Storyboard.SetTargetProperty(modifyFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(modifyFadeAnim);
            DoubleAnimationUsingKeyFrames modifyShadowAnim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(4150.0),
                Duration = new Duration(TimeSpan.FromMilliseconds(4150.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.16,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
                },
                new LinearDoubleKeyFrame(0.16, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(((DropShadowEffect)AppsModifyShadowBorder.Effect).Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyShadowAnim, AppsModifyShadowBorder);
            Storyboard.SetTargetProperty(modifyShadowAnim, new PropertyPath("(Effect).Opacity"));
            board.Children.Add(modifyShadowAnim);
            DoubleAnimationUsingKeyFrames uninstallHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(3400.0)),
                BeginTime = TimeSpan.FromMilliseconds(4900.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(UninstallHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallHoverAnim, UninstallHoverEffect);
            Storyboard.SetTargetProperty(uninstallHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallHoverAnim);
            DoubleAnimationUsingKeyFrames modifyFadeOutAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2700.0)),
                BeginTime = TimeSpan.FromMilliseconds(5700.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 0.0
                }
            }
            };
            Storyboard.SetTarget(modifyFadeOutAnim, AppsModifyShadowBorder);
            Storyboard.SetTargetProperty(modifyFadeOutAnim, new PropertyPath("Opacity"));
            board.Children.Add(modifyFadeOutAnim);
            ThicknessAnimationUsingKeyFrames uninstallDropdownAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2350.0)),
                BeginTime = TimeSpan.FromMilliseconds(5950.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0)),
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 0.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(SettingsAppsUninstallBox.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallDropdownAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallDropdownAnim, new PropertyPath("Margin"));
            board.Children.Add(uninstallDropdownAnim);
            DoubleAnimationUsingKeyFrames uninstallFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2350.0)),
                BeginTime = TimeSpan.FromMilliseconds(5950.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(SettingsAppsUninstallBox.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallFadeAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallFadeAnim);
            DoubleAnimationUsingKeyFrames uninstallShadowAnim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(5500.0),
                Duration = new Duration(TimeSpan.FromMilliseconds(2800.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.16,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
                },
                new LinearDoubleKeyFrame(0.16, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(((DropShadowEffect)AppsUninstallShadowBorder.Effect).Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallShadowAnim, AppsUninstallShadowBorder);
            Storyboard.SetTargetProperty(uninstallShadowAnim, new PropertyPath("(Effect).Opacity"));
            board.Children.Add(uninstallShadowAnim);
            DoubleAnimationUsingKeyFrames uninstallButtonHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(1900.0)),
                BeginTime = TimeSpan.FromMilliseconds(6800.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180.0)),
                    Value = 0.6
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650.0)),
                    Value = 0.6
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(750.0)),
                    Value = 0.1
                },
                new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(UninstallButtonHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallButtonHoverAnim, UninstallButtonHoverEffect);
            Storyboard.SetTargetProperty(uninstallButtonHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallButtonHoverAnim);
            DoubleAnimationUsingKeyFrames uninstallFadeOutAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(800.0)),
                BeginTime = TimeSpan.FromMilliseconds(7500.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 0.0
                }
            }
            };
            Storyboard.SetTarget(uninstallFadeOutAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallFadeOutAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallFadeOutAnim);
            board.RepeatBehavior = RepeatBehavior.Forever;
            board.Begin();
            while (base.IsLoaded)
            {
                List<UninstallKey> Antiviruses = await System.Threading.Tasks.Task.Run(() => GetAVs());
                if (Antiviruses == null)
                {
                    MessageBox.Show(typeof(AntivirusDialog), "Error checking Antivirus. Please uninstall any Antivirus software manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    OpenAntivirusButton.IsEnabled = false;
                    ProgressBar.Visibility = Visibility.Collapsed;
                    FinishText.Visibility = Visibility.Visible;
                    StatusText.Text = "Antivirus software is disabled";
                    CheckMark.Visibility = Visibility.Visible;
                    board.Stop();
                    break;
                }
                if (Antiviruses.Any((UninstallKey x) => !x.Remnant))
                {
                    onlyRemnants = false;
                }
                if (Antiviruses.Any())
                {
                    CurrentAV = (Antiviruses.Any((UninstallKey x) => !x.Remnant) ? Antiviruses.First((UninstallKey x) => !x.Remnant) : Antiviruses.First());
                }
                if (!Antiviruses.Any() || (Antiviruses.All((UninstallKey x) => x.Remnant) && RemnantsIgnored))
                {
                    try
                    {
                        if (UninstallProcess != null && !UninstallProcess.HasExited)
                        {
                            await WaitForExitAsync(UninstallProcess, new CancellationTokenSource(500000).Token);
                        }
                    }
                    catch (Exception)
                    {
                    }
                    try
                    {
                        System.Diagnostics.Process.GetProcessesByName("SystemSettings").FirstOrDefault()?.Kill();
                    }
                    catch (Exception)
                    {
                    }
                    if (!onlyRemnants)
                    {
                        System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(delegate
                        {
                            try
                            {
                                TaskDefinition val = TaskService.Instance.NewTask();
                                val.Principal.LogonType = (TaskLogonType)3;
                                val.Triggers.Add(new LogonTrigger
                                {
                                    UserId = WindowsIdentity.GetCurrent().Name
                                });
                                val.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location, null, null));
                                val.Actions.Add(new ExecAction("SCHTASKS", "/delete /tn \"KTWirzade\" /f", null));
                                val.Settings.DisallowStartIfOnBatteries = false;
                                val.Settings.StopIfGoingOnBatteries = false;
                                val.Settings.AllowHardTerminate = false;
                                val.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                                TaskService.Instance.RootFolder.RegisterTaskDefinition("KTWirzade", val);
                            }
                            catch (Exception)
                            {
                            }
                        });
                        await ProgressBar.WaitForAnimation();
                        await task;
                        StatusText.Text = "Antivirus software is removed";
                    }
                    else
                    {
                        await ProgressBar.WaitForAnimation();
                        StatusText.Text = "Antivirus remnants are removed";
                        FinishText.Text = "This window can now be closed";
                    }
                    OpenAntivirusButton.IsEnabled = false;
                    ProgressBar.Visibility = Visibility.Collapsed;
                    FinishText.Visibility = Visibility.Visible;
                    CheckMark.Visibility = Visibility.Visible;
                    UninstallerButton.IsEnabled = false;
                    board.Stop();
                    DoubleAnimation opacityAnimSteps = new DoubleAnimation();
                    opacityAnimSteps.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                    opacityAnimSteps.To = 0.4;
                    Steps.BeginAnimation(OpacityProperty, opacityAnimSteps);
                    break;
                }
                Step2.Inlines = new List<Inline>
            {
                new Run("Uninstall " + CurrentAV.DisplayName + " and reboot the system.")
                {
                    FontSize = 13.0
                }
            };
                Step2.RefreshInlines();
                if (!string.IsNullOrEmpty(CurrentAV.UninstallString))
                {
                    UninstallerButton.Visibility = Visibility.Visible;
                }
                else
                {
                    UninstallerButton.Visibility = Visibility.Hidden;
                }
                if (!Antiviruses.All((UninstallKey x) => x.Remnant) || RemnantsIgnored || RemnantGrid.Visibility == Visibility.Visible)
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    continue;
                }
                await System.Threading.Tasks.Task.Delay(1500);
                Storyboard storyboard = new Storyboard();
                DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240)),
                    KeyFrames =
                {
                    new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))),
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240)))
                }
                };
                DoubleAnimation opacityAnimSteps2 = new DoubleAnimation
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200)),
                    To = 0.4
                };
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -96.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnim.KeyFrames.Add(transitionKey1);
                transitionAnim.KeyFrames.Add(transitionKey2);
                transitionAnim.KeyFrames.Add(transitionKey3);
                transitionAnim.KeyFrames.Add(transitionKey4);
                ThicknessAnimationUsingKeyFrames obj = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKeyStep2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                obj.KeyFrames.Add(transitionKeyStep1);
                obj.KeyFrames.Add(transitionKeyStep2);
                obj.KeyFrames.Add(transitionKeyStep3);
                obj.KeyFrames.Add(transitionKeyStep4);
                Storyboard.SetTarget(opacityAnim, ModuleGrid);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimSteps2, Steps);
                Storyboard.SetTargetProperty(opacityAnimSteps2, new PropertyPath("Opacity"));
                Storyboard.SetTarget(transitionAnim, ModuleGrid);
                Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                storyboard.Children.Add(opacityAnim);
                storyboard.Children.Add(opacityAnimSteps2);
                storyboard.Children.Add(transitionAnim);
                DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(200.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame
                    {
                        Value = 0.9,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
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
                        Value = 0.9,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    }
                }
                };
                Storyboard.SetTarget(scale_x, remnanttransform);
                Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
                storyboard.Children.Add(scale_x);
                Storyboard.SetTarget(scale_y, remnanttransform);
                Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleYProperty));
                storyboard.Children.Add(scale_y);
                storyboard.Begin();
                RemnantGrid.Visibility = Visibility.Visible;
            }
        }

        public bool? ShowDialog(Window owner, string playbookName)
        {
            base.Owner = owner;
            ToStartText.Text = "To start using the " + playbookName + " Playbook. Antivirus software will otherwise interfere with the process.";
            ShowDialog();
            if (!UninstallerButton.IsEnabled && onlyRemnants)
            {
                return null;
            }
            return !UninstallerButton.IsEnabled;
        }

        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow(securityscale);
        }

        private void OpenAntivirus(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("ms-settings:appsfeatures");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(AntivirusDialog), "Could not open installed apps. Please do it manually.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ImGoodButton_OnClick(object sender, RoutedEventArgs e)
        {
            RemnantsIgnored = true;
            WizardConfig.Current.IgnoreRemnants.Set(value: true);
            SlideModule();
        }

        private async void SlideModule()
        {
            if (!animating)
            {
                animating = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation opacityAnim = new DoubleAnimation
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 400)),
                    To = 0.0
                };
                DoubleAnimationUsingKeyFrames opacityAnimSteps = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 440)),
                    KeyFrames =
                {
                    new LinearDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))),
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440)))
                }
                };
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = new Thickness(380.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(380.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -96.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnim.KeyFrames.Add(transitionKey1);
                transitionAnim.KeyFrames.Add(transitionKey2);
                transitionAnim.KeyFrames.Add(transitionKey3);
                transitionAnim.KeyFrames.Add(transitionKey4);
                ThicknessAnimationUsingKeyFrames transitionAnimStep = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKeyStep2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnimStep.KeyFrames.Add(transitionKeyStep1);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep2);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep3);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep4);
                Storyboard.SetTarget(opacityAnim, ModuleGrid);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimSteps, Steps);
                Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
                Storyboard.SetTarget(transitionAnim, ModuleGrid);
                Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                Storyboard.SetTarget(transitionAnimStep, Steps);
                Storyboard.SetTargetProperty(transitionAnimStep, new PropertyPath("Margin"));
                storyboard.Children.Add(opacityAnim);
                storyboard.Children.Add(opacityAnimSteps);
                storyboard.Children.Add(transitionAnim);
                storyboard.Children.Add(transitionAnimStep);
                storyboard.Begin();
                await System.Threading.Tasks.Task.Delay(540);
                RemnantGrid.Visibility = Visibility.Hidden;
                ImGoodButton.Visibility = Visibility.Visible;
                animating = false;
            }
        }

        private async void UninstallerButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string exe = CurrentAV.UninstallString;
                if (!exe.StartsWith("\"") && !File.Exists(exe.Split(' ').FirstOrDefault()))
                {
                    exe = exe.Substring(0, CurrentAV.UninstallString.IndexOf('\\', 0));
                    do
                    {
                        int length1 = CurrentAV.UninstallString.IndexOf('\\', exe.Length + 1);
                        int length2 = CurrentAV.UninstallString.IndexOf(' ', exe.Length + 1);
                        int length3 = Math.Min(length1, length2);
                        if (length1 == -1)
                        {
                            length3 = length2;
                        }
                        if (length2 == -1)
                        {
                            length3 = length1;
                        }
                        exe = CurrentAV.UninstallString.Substring(0, length3);
                    }
                    while (!File.Exists(exe));
                }
                else if (exe.StartsWith("\""))
                {
                    exe = exe.Substring(1, exe.IndexOf('"', 1) - 1);
                }
                else
                {
                    exe = exe.Split(' ').FirstOrDefault();
                    if (exe == null)
                    {
                        exe = CurrentAV.UninstallString;
                    }
                }
                try
                {
                    UninstallProcess = System.Diagnostics.Process.Start(new ProcessStartInfo(exe, CurrentAV.UninstallString.Substring(Math.Min(CurrentAV.UninstallString.StartsWith("\"") ? (exe.Length + 2) : exe.Length, CurrentAV.UninstallString.Length)).TrimStart(' '))
                    {
                        Verb = "RunAs",
                        UseShellExecute = true
                    });
                }
                catch (Win32Exception)
                {
                }
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(AntivirusDialog), "Error starting uninstaller. Please do it manually.", "Warning");
            }
        }

        public static System.Threading.Tasks.Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += delegate
            {
                tcs.TrySetResult(null);
            };
            if (cancellationToken != default)
            {
                cancellationToken.Register(delegate
                {
                    tcs.SetCanceled();
                });
            }
            if (!process.HasExited)
            {
                return tcs.Task;
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private async void RemnantButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
            RemnantsIgnored = true;
            LoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            LoadContainer.Children.Add(spinner);
            ImGoodButton.Visibility = Visibility.Collapsed;
            System.Threading.Tasks.Task wait = System.Threading.Tasks.Task.Delay(new Random().Next(700, 1300));
            List<UninstallKey> Antiviruses = await System.Threading.Tasks.Task.Run(() => GetAVs());
            if (Antiviruses == null)
            {
                ((System.Windows.Controls.Button)sender).IsEnabled = true;
                LoadContainer.Visibility = Visibility.Collapsed;
                LoadContainer.Children.Remove(spinner);
                SlideModule();
                return;
            }
            foreach (UninstallKey AV in Antiviruses.Where((UninstallKey x) => x.Remnant))
            {
                await InterLink.ExecuteAsync((Expression<System.Action>)(() => EraseRemnant(AV.DisplayName)), false, -1);
            }
            await wait;
            ((System.Windows.Controls.Button)sender).IsEnabled = true;
            LoadContainer.Visibility = Visibility.Collapsed;
            LoadContainer.Children.Remove(spinner);
            SlideModule();
        }
    }
}