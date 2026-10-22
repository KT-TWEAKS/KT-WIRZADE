using FluentIcons.Common;
using Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Utils;


namespace KTWirzade.GUI.Controls
{
    public partial class VerificationButton : System.Windows.Controls.UserControl
    {

        private PlaybookGUI selectedPB;

        private PlaybookGUI.VerificationLevel? lastVerificationLevel;

        // Guards the popup against re-entrant "Verificar novamente" clicks
        // while a forced verification is still running.
        private bool _reverifyRunning;

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
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Detach event handlers so navigating away does not leak this control.
            try
            {
                if (GlobalsGUI.Current != null)
                {
                    GlobalsGUI.Current.PropertyChanged -= GlobalsGUIOnPropertyChanged;
                }
                if (selectedPB != null)
                {
                    selectedPB.PropertyChanged -= SelectedPBOnPropertyChanged;
                }
            }
            catch (Exception)
            {
            }
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
                StatusText.Text = "Selecionar";
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

        private void SetContent(bool force = false)
        {
            if (force)
            {
                // A forced refresh must re-render even when the level did not
                // change (e.g. new "Verificado em" date or trust toggle) and
                // even when the level is null.
                lastVerificationLevel = null;
            }
            else if (lastVerificationLevel == selectedPB.VerificationStatus && StatusText.Text != "Selecionar")
            {
                return;
            }
            lastVerificationLevel = selectedPB.VerificationStatus;
            // Snapshot everything the dispatcher delegate needs so a concurrent
            // playbook switch cannot change the rendering mid-flight.
            PlaybookGUI.VerificationLevel? level = selectedPB.VerificationStatus;
            bool userTrusted = selectedPB.IsUserTrusted;
            DateTime lastChecked = selectedPB.LastChecked;
            // BeginInvoke (not Invoke): never block the caller, and tolerate the
            // dispatcher shutting down while a playbook change is being processed.
            try
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                bool showMeta = false;
                bool showTrust = false;
                bool showReverify = false;
                string reverifyLabel = "Verificar novamente";
                switch (level)
                {
                    case null:
                    case PlaybookGUI.VerificationLevel.Verified:
                        //StatusIcon.Symbol = (Symbol)60488;
                        StatusText.Text = "Verificado";
                        PopupHeader.Text = "Playbook verificado";
                        PopupText.Inlines.Clear();
                        PopupText.Text = "Este playbook foi verificado pelo KT WIRZADE como sendo de um desenvolvedor confiavel.";
                        PopupButtonText.Text = "Visitar site";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Web64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationVerifiedBrush");
                        // Só mostra a data quando houve uma verificação de
                        // verdade (LastChecked real, não o default de DateTime).
                        showMeta = lastChecked > DateTime.MinValue;
                        showReverify = true;
                        break;
                    case PlaybookGUI.VerificationLevel.Unverified:
                        //StatusIcon.Symbol = (Symbol)60506;
                        StatusText.Text = "Não verificado";
                        PopupHeader.Text = "Playbook não verificado";
                        PopupText.Text = string.Empty;
                        if (userTrusted)
                        {
                            // Confiança manual do usuário: nunca afirmar que a
                            // plataforma verificou este playbook.
                            PopupText.Inlines.Add(new Run("Você confia neste playbook manualmente."));
                            PopupText.Inlines.Add(new Run(" O KT WIRZADE não o verificou")
                            {
                                FontWeight = FontWeights.Bold
                            });
                            PopupText.Inlines.Add(new Run("."));
                        }
                        else
                        {
                            PopupText.Inlines.Add(new Run("Este playbook"));
                            PopupText.Inlines.Add(new Run(" não ")
                            {
                                FontWeight = FontWeights.Bold
                            });
                            PopupText.Inlines.Add(new Run("foi verificado pelo KT WIRZADE como sendo de um desenvolvedor confiável."));
                        }
                        PopupButtonText.Text = "Eu entendo";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "ShieldCheckmark64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationUnverifiedBrush");
                        showTrust = true;
                        showReverify = true;
                        break;
                    case PlaybookGUI.VerificationLevel.Malicious:
                        //StatusIcon.Symbol = (Symbol)60490;
                        StatusText.Text = "Malicioso";
                        PopupHeader.Text = "Playbook malicioso";
                        PopupText.Inlines.Clear();
                        PopupText.Text = "Este playbook foi detectado como intencionalmente malicioso e foi reportado ao KT WIRZADE.";
                        PopupButtonText.Text = "Excluir playbook";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Trash64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationMaliciousBrush");
                        // Sem "Verificar novamente" nem confiança manual para
                        // malicioso: um clique nunca pode melhorar este resultado.
                        break;
                    case PlaybookGUI.VerificationLevel.Unreached:
                        //StatusIcon.Symbol = (Symbol)59281;
                        StatusText.Text = "Sem conexão";
                        PopupHeader.Text = "Verificação indisponível";
                        PopupText.Text = string.Empty;
                        PopupText.Text = "Não foi possível acessar os servidores de verificação. Verifique sua conexão e tente novamente.";
                        PopupButtonText.Text = "Eu entendo";
                        PopupButtonImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "ShieldCheckmark64");
                        PopupButton.SetResourceReference(BackgroundProperty, "VerificationUnverifiedBrush");
                        showReverify = true;
                        reverifyLabel = "Tentar novamente";
                        break;
                }
                if (showMeta)
                {
                    PopupMeta.Text = "Verificado em " + lastChecked.ToString("dd/MM/yyyy");
                }
                PopupMeta.Visibility = (showMeta ? Visibility.Visible : Visibility.Collapsed);
                TrustButton.Visibility = (showTrust ? Visibility.Visible : Visibility.Collapsed);
                TrustButtonText.Text = (userTrusted ? "Remover confiança" : "Confiar neste playbook");
                AutomationProperties.SetName(TrustButton, TrustButtonText.Text);
                ReverifyButton.Visibility = (showReverify ? Visibility.Visible : Visibility.Collapsed);
                ReverifyButtonText.Text = (_reverifyRunning ? "Verificando..." : reverifyLabel);
                AutomationProperties.SetName(ReverifyButton, reverifyLabel);
                AutomationProperties.SetName(PopupButton, PopupButtonText.Text);
                // Never re-enable the actions while a re-verification is running.
                ReverifyButton.IsEnabled = !_reverifyRunning;
                TrustButton.IsEnabled = !_reverifyRunning;
                PopupButton.IsEnabled = !_reverifyRunning;
                // The popup keeps a fixed height; grow it to fit the optional
                // meta/trust/reverify rows and keep the shadow clip in sync.
                double extra = (showMeta ? 20.0 : 0.0) + (showTrust ? 26.0 : 0.0) + (showReverify ? 26.0 : 0.0);
                double popupHeight = 162.0 + extra;
                Popup.Height = popupHeight;
                ShadowClipOuter.Rect = new Rect(-15.0, 0.0, 206.0, popupHeight);
                ShadowClipInner.Rect = new Rect(0.0, 0.0, 176.0, popupHeight - 15.0);
                }));
            }
            catch (Exception)
            {
                // Dispatcher may be shutting down; ignore (mirrors previous Invoke behaviour).
            }
        }

        private void Popup_OnOpened(object sender, EventArgs e)
        {
        }

        private void PopupRoot_OnKeyDown(object sender, KeyEventArgs e)
        {
            // Enter já funciona nos botões focados (comportamento padrão do
            // Button); Esc apenas fecha o popup.
            if (e.Key == Key.Escape)
            {
                Popup.IsOpen = false;
                e.Handled = true;
            }
        }

        private async void ReverifyButton_OnClick(object sender, RoutedEventArgs e)
        {
            PlaybookGUI playbook = selectedPB;
            if (_reverifyRunning || playbook == null)
            {
                return;
            }
            // Malicioso nunca pode ser re-verificado a partir da UI: o
            // resultado não pode "melhorar" por um clique.
            if (playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Malicious)
            {
                return;
            }
            _reverifyRunning = true;
            ReverifyButton.IsEnabled = false;
            TrustButton.IsEnabled = false;
            PopupButton.IsEnabled = false;
            ReverifyButtonText.Text = "Verificando...";
            try
            {
                Task reverifyTask = playbook.ReverifyAsync();
                // Timeout de segurança: a UI nunca pode ficar presa "verificando"
                // para sempre, mesmo que a tarefa trave por baixo.
                Task timeoutTask = Task.Delay(30000);
                await Task.WhenAny(reverifyTask, timeoutTask);
                if (!reverifyTask.IsCompleted)
                {
                    Log.EnqueueExceptionSafe(new TimeoutException("Re-verification did not finish within 30s."), "Re-verification timed out in the verification popup.", new (string, object)[1] { ("Playbook", playbook.Name) });
                }
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Re-verification failed from the verification popup.", new (string, object)[1] { ("Playbook", playbook.Name) });
            }
            finally
            {
                _reverifyRunning = false;
                if (selectedPB == playbook)
                {
                    SetContent(true);
                }
                ReverifyButton.IsEnabled = true;
                TrustButton.IsEnabled = true;
                PopupButton.IsEnabled = true;
            }
        }

        private void TrustButton_OnClick(object sender, RoutedEventArgs e)
        {
            PlaybookGUI playbook = selectedPB;
            // Confiança manual só existe para Unverified; Malicious (e qualquer
            // outro nível) não tem caminho pela confiança do usuário.
            if (playbook == null || playbook.VerificationStatus != PlaybookGUI.VerificationLevel.Unverified)
            {
                return;
            }
            if (string.IsNullOrEmpty(playbook.Hash))
            {
                MessageBox.Show(this, "Não foi possível identificar este playbook para conceder confiança.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (playbook.IsUserTrusted)
            {
                MessageBoxResult confirmRevoke = MessageBox.Show(
                    this,
                    "Remover sua confiança neste playbook?\n\nEle voltará a ser tratado como não verificado.",
                    "Remover confiança",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirmRevoke != MessageBoxResult.Yes)
                {
                    return;
                }
                PlaybookTrustStore.Revoke(playbook.Hash);
            }
            else
            {
                MessageBoxResult confirmTrust = MessageBox.Show(
                    this,
                    "Confiar neste playbook?\n\nEle NÃO foi verificado pelo KT WIRZADE. Ao confirmar, você assume a responsabilidade por ele. Playbooks detectados como maliciosos continuam bloqueados e não podem receber confiança.",
                    "Confiar neste playbook",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirmTrust != MessageBoxResult.Yes)
                {
                    return;
                }
                PlaybookTrustStore.Trust(playbook.Hash);
            }
            playbook.RefreshUserTrust();
            SetContent(true);
        }

        private void PopupButton_OnClick(object sender, RoutedEventArgs e)
        {
            switch (selectedPB.VerificationStatus)
            {
                case null:
                case PlaybookGUI.VerificationLevel.Verified:
                    if ((selectedPB).Website == null)
                    {
                        MessageBox.Show(typeof(MainWindow), "Nenhum site disponivel para este playbook.", "Information");
                        break;
                    }
                    try
                    {
                        Process.Start(selectedPB.Website);
                        break;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(typeof(MainWindow), "Link do site invalido.", "Warning");
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
                    var confirmDelete = KTWirzade.GUI.MessageBox.Show(
                        this,
                        "Excluir este playbook?\n\nEle foi detectado como malicioso e sera removido da lista de playbooks.",
                        "Excluir playbook",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (confirmDelete != MessageBoxResult.Yes)
                    {
                        break;
                    }
                    WizardConfig.HidePlaybook(selectedPB);
                    GlobalsGUI.Current.Items.Remove(selectedPB);
                    break;
            }
        }
    }
}
