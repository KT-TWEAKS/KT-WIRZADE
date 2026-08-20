using FluentIcons.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media.Animation;


namespace KTWirzade.GUI.Controls
{
    public partial class VerificationButton : System.Windows.Controls.UserControl
    {

        private PlaybookGUI selectedPB;

        private PlaybookGUI.VerificationLevel? lastVerificationLevel;

        public VerificationButton()
        {
            InitializeComponent();
            if (GlobalsGUI.Current.Playbook != null)
            {
                selectedPB = GlobalsGUI.Current.Playbook;
                selectedPB.PropertyChanged += SelectedPBOnPropertyChanged;
                Button.Click += VerificationButton_OnClick;
                Button.IsEnabled = true;
                SetContent();
            }
            GlobalsGUI.Current.PropertyChanged += GlobalsGUIOnPropertyChanged;
        }

        private void GlobalsGUIOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(e.PropertyName == "Playbook"))
            {
                return;
            }
            if (selectedPB != null)
            {
                selectedPB.PropertyChanged -= SelectedPBOnPropertyChanged;
            }
            if (GlobalsGUI.Current.Playbook != null)
            {
                Visibility = Visibility.Visible;
                Button.IsEnabled = true;
                selectedPB = GlobalsGUI.Current.Playbook;
                if (selectedPB.VerificationStatus.HasValue)
                {
                    Button.Click -= VerificationButton_OnClick;
                    Button.Click += VerificationButton_OnClick;
                }
                SetContent();
                selectedPB.PropertyChanged += SelectedPBOnPropertyChanged;
            }
            else
            {
                Visibility = Visibility.Collapsed;
                StatusText.Text = "Select";
                Button.IsEnabled = false;
                Button.Click -= VerificationButton_OnClick;
                //StatusIcon.Symbol = (Symbol)60482;
            }
        }

        private void SelectedPBOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "VerificationStatus")
            {
                if (!selectedPB.VerificationStatus.HasValue)
                {
                    Button.Click -= VerificationButton_OnClick;
                    SetContent();
                }
                SetContent();
            }
        }

        public async void Open()
        {
            VerificationButton_OnClick(this, new RoutedEventArgs());
        }

        private async void VerificationButton_OnClick(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimation mainAnim1S = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 0)),
                To = -5.0
            };
            DoubleAnimation mainAnim2S = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 0)),
                To = 85.0
            };
            Storyboard.SetTarget(mainAnim1S, Popup);
            Storyboard.SetTargetProperty(mainAnim1S, new PropertyPath("VerticalOffset"));
            Storyboard.SetTarget(mainAnim2S, Mains);
            Storyboard.SetTargetProperty(mainAnim2S, new PropertyPath("Height"));
            storyboard.Children.Add(mainAnim2S);
            storyboard.Children.Add(mainAnim1S);
            storyboard.Begin();
            ShadowBorder.Visibility = Visibility.Hidden;
            await Task.Delay(10);
            Popup.IsOpen = true;
            Storyboard storyboard2 = new Storyboard();
            DoubleAnimationUsingKeyFrames mainAnim1 = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240))
            };
            DoubleKeyFrame anim1Key1 = new LinearDoubleKeyFrame
            {
                Value = 1.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))
            };
            DoubleKeyFrame anim1Key2 = new LinearDoubleKeyFrame
            {
                Value = 3.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 140))
            };
            mainAnim1.KeyFrames.Add(anim1Key1);
            mainAnim1.KeyFrames.Add(anim1Key2);
            DoubleAnimationUsingKeyFrames mainAnim2 = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 150))
            };
            DoubleKeyFrame anim3Key1 = new LinearDoubleKeyFrame
            {
                Value = 103.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))
            };
            DoubleKeyFrame anim3Key2 = new LinearDoubleKeyFrame
            {
                Value = 108.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 150))
            };
            mainAnim2.KeyFrames.Add(anim3Key1);
            mainAnim2.KeyFrames.Add(anim3Key2);
            DoubleAnimationUsingKeyFrames shadowAnim1 = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 420))
            };
            DoubleKeyFrame anim2Key1 = new LinearDoubleKeyFrame
            {
                Value = 0.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            DoubleKeyFrame anim2Key2 = new LinearDoubleKeyFrame
            {
                Value = 0.0,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
            };
            DoubleKeyFrame anim2Key3 = new LinearDoubleKeyFrame
            {
                Value = 0.27,
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 420))
            };
            shadowAnim1.KeyFrames.Add(anim2Key1);
            shadowAnim1.KeyFrames.Add(anim2Key2);
            shadowAnim1.KeyFrames.Add(anim2Key3);
            Storyboard.SetTarget(mainAnim1, Popup);
            Storyboard.SetTargetProperty(mainAnim1, new PropertyPath("VerticalOffset"));
            Storyboard.SetTarget(mainAnim2, Mains);
            Storyboard.SetTargetProperty(mainAnim2, new PropertyPath("Height"));
            Storyboard.SetTarget(shadowAnim1, ShadowBorder);
            Storyboard.SetTargetProperty(shadowAnim1, new PropertyPath("(Effect).Opacity"));
            storyboard2.Children.Add(mainAnim1);
            storyboard2.Children.Add(mainAnim2);
            storyboard2.Children.Add(shadowAnim1);
            storyboard2.Begin();
            await Task.Delay(160);
            ShadowBorder.Visibility = Visibility.Visible;
        }

        private void SetContent()
        {
            if (lastVerificationLevel == selectedPB.VerificationStatus && StatusText.Text != "Select")
            {
                return;
            }
            lastVerificationLevel = selectedPB.VerificationStatus;
            System.Windows.Application.Current.Dispatcher.Invoke(delegate
            {
                switch (selectedPB.VerificationStatus)
                {
                    case null:
                    case PlaybookGUI.VerificationLevel.Verified:
                        //StatusIcon.Symbol = (Symbol)60488;
                        StatusText.Text = "Verified";
                        PopupHeader.Text = "Verified Playbook";
                        PopupText.Inlines.Clear();
                        PopupText.Text = "This Playbook was verified by Ameliorated to be from a trustworthy developer.";
                        PopupButtonText.Text = "Visit website";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Web64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationVerifiedBrush");
                        break;
                    case PlaybookGUI.VerificationLevel.Unverified:
                        //StatusIcon.Symbol = (Symbol)60506;
                        StatusText.Text = "Unverified";
                        PopupHeader.Text = "Unverified Playbook";
                        PopupText.Text = string.Empty;
                        PopupText.Inlines.Add(new Run("This Playbook has"));
                        PopupText.Inlines.Add(new Run(" not ")
                        {
                            FontWeight = FontWeights.Bold
                        });
                        PopupText.Inlines.Add(new Run("been verified by Ameliorated to be from a trustworthy developer."));
                        PopupButtonText.Text = "I understand";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "ShieldCheckmark64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationUnverifiedBrush");
                        break;
                    case PlaybookGUI.VerificationLevel.Malicious:
                        //StatusIcon.Symbol = (Symbol)60490;
                        StatusText.Text = "Malicious";
                        PopupHeader.Text = "Malicious Playbook";
                        PopupText.Inlines.Clear();
                        PopupText.Text = "This Playbook was detected as intentionally malicious, and has been reported to Ameliorated.";
                        PopupButtonText.Text = "Delete Playbook";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Trash64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationMaliciousBrush");
                        break;
                    case PlaybookGUI.VerificationLevel.Unreached:
                        //StatusIcon.Symbol = (Symbol)59281;
                        StatusText.Text = "Unverified";
                        PopupHeader.Text = "Unverified Playbook";
                        PopupText.Text = string.Empty;
                        PopupText.Text = "Unable to reach verification servers. This Playbook may be unverified or malicious.";
                        PopupButtonText.Text = "I understand";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "ShieldCheckmark64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationUnverifiedBrush");
                        break;
                }
            });
        }

        private void Popup_OnOpened(object sender, EventArgs e)
        {
        }

        private void PopupButton_OnClick(object sender, RoutedEventArgs e)
        {
            switch (selectedPB.VerificationStatus)
            {
                case null:
                case PlaybookGUI.VerificationLevel.Verified:
                    if ((selectedPB).Website == null)
                    {
                        MessageBox.Show(typeof(MainWindow), "No website available for this Playbook.", "Information");
                        break;
                    }
                    try
                    {
                        Process.Start(selectedPB.Website);
                        break;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(typeof(MainWindow), "Website link is invalid.", "Warning");
                        break;
                    }
                case PlaybookGUI.VerificationLevel.Unverified:
                    Popup.IsOpen = false;
                    break;
                case PlaybookGUI.VerificationLevel.Unreached:
                    Popup.IsOpen = false;
                    break;
                case PlaybookGUI.VerificationLevel.Malicious:
                    Popup.IsOpen = false;
                    GlobalsGUI.Current.Items.Remove(selectedPB);
                    break;
            }
        }
    }
}
