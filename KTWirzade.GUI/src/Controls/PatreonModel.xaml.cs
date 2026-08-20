using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KTWirzade.GUI.Controls
{
    public partial class PatreonModel : System.Windows.Controls.UserControl
    {

        private static readonly List<KeyValuePair<string, FontWeight>> Patrons = new List<KeyValuePair<string, FontWeight>>
    {
        new KeyValuePair<string, FontWeight>("lv1mp", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("OhJeezy", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Son Luu", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Jacob Huffine", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Erick Rodriguez", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("WasdGamer", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Ronix", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("ali kozat", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Grzechu Ra", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("James Esposito", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Comers Sila", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Dew", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Manuel", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Uzade", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Fuad Poroshtica", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Don Dingo", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Adam K", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Bogdan Syrodoyev", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("FrackleSmith", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("IAM JDSCS", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Daniel", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Di", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Enquirer9403", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Brady", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Petros Ziogas", FontWeights.Bold),
        new KeyValuePair<string, FontWeight>("Fernando Casanova", FontWeights.Normal),
        new KeyValuePair<string, FontWeight>("Xp4", FontWeights.Bold)
    };

        private Storyboard board;

        private List<KeyValuePair<double, double>> resetList = new List<KeyValuePair<double, double>>();

        private Storyboard currentBoard;

        public TranslateTransform Transform;

        private double itemHeight = 19.0;

        public PatreonModel()
        {
            InitializeComponent();
            Items.ItemsSource = Patrons.Concat(Patrons);
        }

        public async void OnOpened(object sender, EventArgs args)
        {
            SearchBox.TextChanged -= SearchBox_OnTextChanged;
            SearchBox.Text = "";
            SearchBox.TextChanged += SearchBox_OnTextChanged;
            await StartAnimation(30);
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

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

        private void PopupCloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((Popup)((FrameworkElement)((FrameworkElement)base.Parent).Parent).Parent).IsOpen = false;
        }

        private async void SearchBox_OnTextChanged(object sender, RoutedEventArgs e)
        {
            TextBlock block = FindVisualChildren<TextBlock>(SearchBox).First((TextBlock x) => x.Name == "BackgroundText");
            string lastText = SearchBox.Text;
            if (SearchBox.Text.Length > 0)
            {
                block.Visibility = Visibility.Hidden;
                await Task.Delay(400);
                if (SearchBox.Text != lastText)
                {
                    return;
                }
                currentBoard.Pause(this);
                int skip = (int)Math.Ceiling((0.0 - Transform.Y) / itemHeight);
                List<KeyValuePair<string, FontWeight>> matches = Patrons.Where((KeyValuePair<string, FontWeight> x) => x.Key.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) != -1).ToList();
                board = new Storyboard();
                List<KeyValuePair<string, FontWeight>> items = Items.Items.Cast<KeyValuePair<string, FontWeight>>().ToList();
                List<int> foundIndexes = new List<int>();
                foreach (KeyValuePair<string, FontWeight> match in matches)
                {
                    int index = -1;
                    while (index < skip)
                    {
                        int tmp = items.FindIndex(index + 1, (KeyValuePair<string, FontWeight> x) => x.Key == match.Key);
                        if (tmp == -1)
                        {
                            break;
                        }
                        index = tmp;
                    }
                    foundIndexes.Add(index);
                }
                foundIndexes.Sort();
                foreach (int index2 in foundIndexes)
                {
                    TextBlock container = (TextBlock)VisualTreeHelper.GetChild(Items.ItemContainerGenerator.ContainerFromIndex(index2), 0);
                    double num = (double)skip * itemHeight;
                    double offset = 0.0 - (num + Transform.Y);
                    int matchedIndex = foundIndexes.IndexOf(index2);
                    double current = (double)index2 * itemHeight;
                    double diff = num + (double)matchedIndex * itemHeight - current;
                    TranslateTransform move = (TranslateTransform)container.FindName("Move");
                    DoubleAnimation anim = new DoubleAnimation(offset + diff, new Duration(TimeSpan.FromMilliseconds(Math.Min(250.0, Math.Abs(diff + offset - move.Y) * 6.0))))
                    {
                        EasingFunction = new QuadraticEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    Storyboard.SetTargetName(anim, "TR" + (index2 + 1));
                    Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.YProperty));
                    DoubleAnimation scaleAnimY = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(170.0)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(30.0)
                    };
                    Storyboard.SetTargetName(scaleAnimY, "TB" + (index2 + 1));
                    Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                    DoubleAnimation anim2 = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(170.0)))
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        BeginTime = TimeSpan.FromMilliseconds(30.0)
                    };
                    Storyboard.SetTarget(anim2, container);
                    Storyboard.SetTargetProperty(anim2, new PropertyPath(UIElement.OpacityProperty));
                    board.Children.Add(anim);
                    board.Children.Add(scaleAnimY);
                    board.Children.Add(anim2);
                }
                Storyboard fadeBoard = new Storyboard();
                bool addToList = resetList.Count == 0;
                int i = -1;
                foreach (KeyValuePair<string, FontWeight> item2 in items)
                {
                    _ = item2;
                    i++;
                    TextBlock container2 = (TextBlock)VisualTreeHelper.GetChild(Items.ItemContainerGenerator.ContainerFromIndex(i), 0);
                    ScaleTransform scale = (ScaleTransform)container2.FindName("Transform");
                    if (addToList)
                    {
                        resetList.Add(new KeyValuePair<double, double>(container2.Opacity, scale.ScaleY));
                    }
                    if (!foundIndexes.Contains(i))
                    {
                        DoubleAnimation anim3 = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(300.0)))
                        {
                            EasingFunction = new SineEase
                            {
                                EasingMode = EasingMode.EaseOut
                            }
                        };
                        Storyboard.SetTarget(anim3, container2);
                        Storyboard.SetTargetProperty(anim3, new PropertyPath(UIElement.OpacityProperty));
                        fadeBoard.Children.Add(anim3);
                    }
                }
                if (!matches.Any())
                {
                    DoubleAnimation anim4 = new DoubleAnimation(0.8, new Duration(TimeSpan.FromMilliseconds(170.0)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(500.0),
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    Storyboard.SetTarget(anim4, NoResults);
                    Storyboard.SetTargetProperty(anim4, new PropertyPath(UIElement.OpacityProperty));
                    board.Children.Add(anim4);
                }
                else
                {
                    DoubleAnimation anim5 = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(100.0)))
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    Storyboard.SetTarget(anim5, NoResults);
                    Storyboard.SetTargetProperty(anim5, new PropertyPath(UIElement.OpacityProperty));
                    board.Children.Add(anim5);
                }
                fadeBoard.Begin();
                await Task.Delay(100);
                if (!(SearchBox.Text != lastText))
                {
                    board.Begin(this);
                }
                return;
            }
            block.Visibility = Visibility.Visible;
            await Task.Delay(400);
            if (SearchBox.Text != lastText)
            {
                return;
            }
            Storyboard board1 = new Storyboard();
            DoubleAnimation anim6 = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(100.0)))
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };
            Storyboard.SetTarget(anim6, NoResults);
            Storyboard.SetTargetProperty(anim6, new PropertyPath(UIElement.OpacityProperty));
            board1.Children.Add(anim6);
            foreach (object item in Items.ItemContainerGenerator.Items)
            {
                DependencyObject rawContainer = Items.ItemContainerGenerator.ContainerFromItem(item);
                TextBlock container3 = (TextBlock)VisualTreeHelper.GetChild(rawContainer, 0);
                int index3 = Items.ItemContainerGenerator.IndexFromContainer(rawContainer);
                TranslateTransform move2 = (TranslateTransform)container3.FindName("Move");
                DoubleAnimation animM = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(Math.Min(Math.Abs(move2.Y * 11.0), 400.0))))
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    }
                };
                Storyboard.SetTargetName(animM, "TR" + (index3 + 1));
                Storyboard.SetTargetProperty(animM, new PropertyPath(TranslateTransform.YProperty));
                board1.Children.Add(animM);
                if (resetList.Count != 0)
                {
                    DoubleAnimation anim7 = new DoubleAnimation(resetList[index3].Key, new Duration(TimeSpan.FromMilliseconds(250.0)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(250.0),
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    Storyboard.SetTarget(anim7, container3);
                    Storyboard.SetTargetProperty(anim7, new PropertyPath(UIElement.OpacityProperty));
                    board1.Children.Add(anim7);
                    DoubleAnimation anims = new DoubleAnimation(resetList[index3].Value, new Duration(TimeSpan.FromMilliseconds(200.0)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(300.0),
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    Storyboard.SetTargetName(anims, "TB" + (index3 + 1));
                    Storyboard.SetTargetProperty(anims, new PropertyPath(ScaleTransform.ScaleYProperty));
                    board1.Children.Add(anims);
                }
            }
            board1.Begin(this);
            await Task.Delay(500);
            resetList = new List<KeyValuePair<double, double>>();
            if (!(SearchBox.Text != lastText))
            {
                if (board != null)
                {
                    board.Stop();
                }
                if (currentBoard.GetIsPaused(this))
                {
                    TimeSpan? time = currentBoard.GetCurrentTime(this);
                    currentBoard.Begin(this, isControllable: true);
                    currentBoard.Seek(this, time.Value, TimeSeekOrigin.Duration);
                }
            }
        }

        public async Task StartAnimation(int cycles)
        {
            currentBoard = new Storyboard();
            Transform = (TranslateTransform)Items.Template.FindName("Presenter", Items);
            double scrollDur = Patrons.Count * 700;
            if (PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M22 % 0.5 != 0.0)
            {
                itemHeight = 19.25;
            }
            DoubleAnimationUsingKeyFrames scrollAnim = new DoubleAnimationUsingKeyFrames
            {
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame(0.0 - (double)(Patrons.Count * 2) * itemHeight, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(Patrons.Count * 2 * 700))),
                new LinearDoubleKeyFrame(0.0 - (double)Patrons.Count * itemHeight, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(Patrons.Count * 2 * 700))),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 3.0))),
                new LinearDoubleKeyFrame(0.0 - (double)Patrons.Count * itemHeight, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 3.0))),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 4.0))),
                new LinearDoubleKeyFrame(0.0 - (double)Patrons.Count * itemHeight, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 4.0))),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 5.0))),
                new LinearDoubleKeyFrame(0.0 - (double)Patrons.Count * itemHeight, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * 5.0)))
            }
            };
            for (int c1 = 0; c1 <= cycles; c1++)
            {
                scrollAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * (double)(3 + c1)))));
                scrollAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0 - (double)Patrons.Count * itemHeight, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scrollDur * (double)(3 + c1)))));
            }
            RegisterName("PresenterTransform", Transform);
            Storyboard.SetTargetName(scrollAnim, "PresenterTransform");
            Storyboard.SetTargetProperty(scrollAnim, new PropertyPath(TranslateTransform.YProperty));
            currentBoard.Children.Add(scrollAnim);
            int i = 0;
            foreach (object item in Items.ItemContainerGenerator.Items)
            {
                i++;
                DependencyObject rawContainer = Items.ItemContainerGenerator.ContainerFromItem(item);
                TextBlock container = (TextBlock)VisualTreeHelper.GetChild(rawContainer, 0);
                int index = Items.ItemContainerGenerator.IndexFromContainer(rawContainer);
                int reverseIndex = Items.Items.Count - 1 - index;
                ScaleTransform transform = (ScaleTransform)container.FindName("Transform");
                RegisterName("TB" + i, transform);
                TranslateTransform move = (TranslateTransform)container.FindName("Move");
                RegisterName("TR" + i, move);
                TimeSpan beginTime = TimeSpan.FromMilliseconds(reverseIndex * 700);
                DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = beginTime,
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0))),
                    new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100.0)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    },
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(6700.0))),
                    new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(7500.0)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseIn
                        }
                    }
                }
                };
                DoubleAnimation transReset = new DoubleAnimation(0.0, new Duration(TimeSpan.Zero));
                Storyboard.SetTargetName(transReset, "TR" + (index + 1));
                Storyboard.SetTargetProperty(transReset, new PropertyPath(TranslateTransform.YProperty));
                currentBoard.Children.Add(transReset);
                DoubleAnimationUsingKeyFrames scaleAnimY = new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = beginTime,
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0))),
                    new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300.0)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    },
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(6500.0))),
                    new EasingDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(7500.0)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseIn
                        }
                    }
                }
                };
                for (int j = 0; j <= cycles; j++)
                {
                    int wait = Patrons.Count * (j + 3) * 700 - reverseIndex * 700 + (reverseIndex - Items.Items.Count) * 700;
                    wait += 300;
                    anim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait))));
                    anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 800)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    });
                    anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 800 + 5900))));
                    anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 800 + 6400)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseIn
                        }
                    });
                    scaleAnimY.KeyFrames.Add(new LinearDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait))));
                    scaleAnimY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 1000)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    });
                    scaleAnimY.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 1000 + 5300))));
                    scaleAnimY.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + 1000 + 6200)))
                    {
                        EasingFunction = new ExponentialEase
                        {
                            EasingMode = EasingMode.EaseIn
                        }
                    });
                }
                Storyboard.SetTarget(anim, container);
                Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
                currentBoard.Children.Add(anim);
                Storyboard.SetTargetName(scaleAnimY, "TB" + i);
                Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                currentBoard.Children.Add(scaleAnimY);
            }
            currentBoard.RepeatBehavior = RepeatBehavior.Forever;
            currentBoard.Begin(this, isControllable: true);
            currentBoard.Seek(this, TimeSpan.FromMilliseconds(scrollDur), TimeSeekOrigin.Duration);
        }

        private void PatreonButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://getnexus.cc");
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Error opening link.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}