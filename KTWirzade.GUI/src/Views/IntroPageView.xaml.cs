using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Models;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using KTWirzade.Shared.I18n;
using static KTWirzade.Shared.Requirements;

namespace KTWirzade.GUI.Views
{
    public partial class IntroPageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        // Session-wide record of the checks the user explicitly chose to override
        // (in-memory only; which check id was overridden and when).
        private sealed class OverrideRecord
        {
            public string CheckId;

            public DateTime TimestampUtc;
        }

        private static readonly List<OverrideRecord> _sessionOverrides = new List<OverrideRecord>();

        // True while a third-party antivirus is still present (bypassable warning).
        private bool _antivirusFailed;

        // True while the user has explicitly acknowledged the antivirus risk.
        private bool _antivirusOverrideAccepted;

        // Set while the override checkbox is changed programmatically so the
        // Checked/Unchecked handlers do not re-enter (or re-prompt the user).
        private bool _settingOverride;

        // Shows/hides the override section; hidden whenever there is nothing left
        // to override, so the state is never stale on screen.
        private void UpdateAntivirusOverrideUi()
        {
            if (_antivirusFailed)
            {
                OverrideWarningText.Text = Text("Intro.Guidance.Antivirus", "Remova o antivírus de terceiros para continuar.");
                OverrideBox.Visibility = Visibility.Visible;
                OverrideBadgeText.Visibility = _antivirusOverrideAccepted ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Situation changed: nothing left to override.
                SetOverrideAccepted(false);
                _antivirusOverrideAccepted = false;
                OverrideBox.Visibility = Visibility.Collapsed;
            }
        }

        private void SetOverrideAccepted(bool accepted)
        {
            _settingOverride = true;
            try
            {
                OverrideAcceptBox.IsChecked = accepted;
            }
            finally
            {
                _settingOverride = false;
            }
        }

        private void RecordOverride(string checkId)
        {
            _sessionOverrides.Add(new OverrideRecord { CheckId = checkId, TimestampUtc = DateTime.UtcNow });
            Core.Log.WriteSafe((Core.LogType)1, "Verificação opcional ignorada pelo usuário: " + checkId + ".", (Core.SerializableTrace)null, Array.Empty<(string, object)>());
        }

        private void OverrideAcceptBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_settingOverride)
            {
                return;
            }
            MessageBoxResult result = KTWirzade.GUI.MessageBox.Show(
                this,
                OverrideWarningText.Text + "\n\nContinuar mesmo assim pode causar instabilidade ou problemas no sistema. Deseja continuar por sua conta e risco?",
                "Continuar mesmo assim?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                // Cancelled: the override stays unchecked and Next remains disabled.
                SetOverrideAccepted(false);
                UpdateAntivirusOverrideUi();
                if (base.DataContext is ViewModelBase vmCancel)
                {
                    vmCancel.MainNextButtonActive = false;
                }
                return;
            }
            _antivirusOverrideAccepted = true;
            RecordOverride("antivirus");
            UpdateAntivirusOverrideUi();
            if (base.DataContext is ViewModelBase vm)
            {
                vm.MainNextButtonActive = true;
            }
        }

        private void OverrideAcceptBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_settingOverride)
            {
                return;
            }
            _antivirusOverrideAccepted = false;
            UpdateAntivirusOverrideUi();
            if (base.DataContext is ViewModelBase vm && _antivirusFailed)
            {
                vm.MainNextButtonActive = false;
            }
        }

        private void OverrideRecheckButton_OnClick(object sender, RoutedEventArgs e)
        {
            RunAgainButton_OnClick(sender, e);
        }

        public IntroPageView()
        {
            InitializeComponent();
            base.DataContextChanged += async delegate
            {
                object dataContext = base.DataContext;
                if (!(dataContext is ViewModelBase viewModel))
                {
                    return;
                }
                try
                {
                    ApplyLocalizedLabels();
                    viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                    RunAgainButton.IsEnabled = true;
                    RunAgainErrorButton.IsEnabled = true;
                    UpgradeButton.IsEnabled = true;
                    AntivirusBox.Visibility = Visibility.Collapsed;
                    NotRequiredBox.Visibility = Visibility.Collapsed;
                    AppliedBox.Visibility = Visibility.Collapsed;
                    AppliedErrorBox.Visibility = Visibility.Collapsed;
                    UpgradeBox.Visibility = Visibility.Collapsed;
                    DefenderBox.Visibility = Visibility.Collapsed;
                    VerificationUnavailableBox.Visibility = Visibility.Collapsed;
                    NextStepText.Visibility = Visibility.Collapsed;
                    FooterGuidanceText.Text = Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.");
                    SlideModuleUp();
                    bool nextButton = true;
                    viewModel.MainNextButtonActive = false;
                    SetSecurityActionVisible(false);
                    // A fresh check cycle invalidates any previously accepted override.
                    _antivirusFailed = false;
                    _antivirusOverrideAccepted = false;
                    SetOverrideAccepted(false);
                    OverrideBox.Visibility = Visibility.Collapsed;
                    PlaybookGUI lastAppliedMatch = GlobalsGUI.Current.Playbook.LastAppliedMatch(GlobalsGUI.Current.AppliedPlaybooks);
                    if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version != ((Playbook)GlobalsGUI.Current.Playbook).Version && ((Playbook)GlobalsGUI.Current.Playbook).IsUpgradeApplicable(((Playbook)lastAppliedMatch).Version))
                    {
                        UpgradeBox.Visibility = Visibility.Visible;
                        FooterGuidanceText.Text = Text("Intro.Guidance.Upgrade", "Use Atualizar para instalar a versão mais recente deste APBX.");
                        nextButton = false;
                    }
                    else if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version == ((Playbook)GlobalsGUI.Current.Playbook).Version)
                    {
                        AppliedText.Text = Text("Intro.Applied.Current", "Este APBX já está aplicado");
                        FooterGuidanceText.Text = Text("Intro.Guidance.Applied", "Este APBX já está aplicado. Use “Executar novamente” para reaplicá-lo.");
                        nextButton = false;
                        if ((int)((Playbook)lastAppliedMatch).ErrorLevel == 1)
                        {
                            AppliedErrorBoxText.Text = Text("Intro.Applied.Errors", "Este APBX foi aplicado com erros");
                            AppliedErrorBox.Visibility = Visibility.Visible;
                        }
                        else if ((int)((Playbook)lastAppliedMatch).ErrorLevel == 2)
                        {
                            AppliedErrorBoxText.Text = Text("Intro.Applied.Failed", "Falha ao aplicar este APBX");
                            AppliedErrorBox.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            AppliedBox.Visibility = Visibility.Visible;
                        }
                    }
                    else if (((Playbook)GlobalsGUI.Current.Playbook).Username == "KTWirzade" && GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && HasKTWirzadeIntegriy())
                    {
                        nextButton = false;
                        AppliedText.Text = Text("Intro.Applied.Current", "Este APBX já está aplicado");
                        FooterGuidanceText.Text = Text("Intro.Guidance.Applied", "Este APBX já está aplicado. Use “Executar novamente” para reaplicá-lo.");
                        AppliedBox.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        bool flag = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6);
                        if (flag)
                        {
                            flag = !(await Task.Run(delegate
                            {
                                try
                                {
                                    if (Convert.ToUInt32(Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Winmgmt").GetValue("Start")) == 4)
                                    {
                                        GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                                    }
                                }
                                catch
                                {
                                    GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                                }
                                return (!WizardConfig.Current.IgnoreRemnants.Get()) ? (!WinUtil.GetEnabledAvList(false).Any()) : (!WinUtil.GetEnabledAvList(false).Any((ProviderStatus x) => x.FileExists));
                            }));
                        }
                        if (flag)
                        {
                            nextButton = false;
                            FooterGuidanceText.Text = Text("Intro.Guidance.Antivirus", "Remova o antivírus de terceiros para continuar.");
                            NextStepText.Visibility = Visibility.Hidden;
                            AntivirusBox.Visibility = Visibility.Visible;
                        }
                        else if (((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)2) || ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)3))
                        {
                            if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                            {
                                nextButton = false;
                                NotRequiredBox.Visibility = Visibility.Hidden;
                                NextStepText.Visibility = Visibility.Visible;
                                SetSecurityActionVisible(true);
                                FooterGuidanceText.Text = Text("Intro.Guidance.Security", "Conclua a ação de segurança acima para continuar.");
                            }
                        }
                        else if (!((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6))
                        {
                            NotRequiredBox.Visibility = Visibility.Visible;
                        }
                        _antivirusFailed = flag;
                        UpdateAntivirusOverrideUi();
                    }
                    // Dead branch removed: MainNextButtonActive was forced to false above,
                    // so the old "if (MainNextButtonActive)" guard was never true. The
                    // computed `nextButton` result is applied exactly once at the end below.
                    {
                        if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version != ((Playbook)GlobalsGUI.Current.Playbook).Version && !((Playbook)GlobalsGUI.Current.Playbook).IsUpgradeApplicable(((Playbook)lastAppliedMatch).Version))
                        {
                            viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(delegate
                            {
                                ConflictPopup.IsOpen = true;
                            }, () => true);
                            ConflictCurrentImage.Source = lastAppliedMatch.Icon;
                            ConflictCurrentTitle.Text = "v" + ((Playbook)lastAppliedMatch).Version;
                            ConflictImage.Source = GlobalsGUI.Current.Playbook.Icon;
                            ConflictTitle.Text = "v" + ((Playbook)GlobalsGUI.Current.Playbook).Version;
                            ConflictPopupText.Text = "This Playbook is already applied to your system and doesn't support this upgrade.";
                            ConflictTopLine.Text = "This can cause major conflicts and issues!";
                            ConflictCancelButton.Visibility = Visibility.Visible;
                            ConflictContinueButton.Visibility = Visibility.Visible;
                            ConflictOKButton.Visibility = Visibility.Collapsed;
                            if (((Playbook)GlobalsGUI.Current.Playbook).AllowUnsupportedUpgrades == false)
                            {
                                ConflictPopupText.Text = "An older version of this Playbook is already applied to your system.";
                                ConflictTopLine.Text = ((Playbook)GlobalsGUI.Current.Playbook).Name + " does not allow upgrades.";
                                ConflictCancelButton.Visibility = Visibility.Collapsed;
                                ConflictContinueButton.Visibility = Visibility.Collapsed;
                                ConflictOKButton.Visibility = Visibility.Visible;
                            }
                        }
                        if (lastAppliedMatch == null && GlobalsGUI.Current.AppliedPlaybooks.Any((Playbook x) => x.Overhaul) && ((Playbook)GlobalsGUI.Current.Playbook).Overhaul)
                        {
                            viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(delegate
                            {
                                ConflictPopup.IsOpen = true;
                            }, () => true);
                            Playbook conflict = GlobalsGUI.Current.AppliedPlaybooks.First((Playbook x) => x.Overhaul);
                            ConflictPopupText.Text = "You already have a total conversion Playbook applied to your system. Continue anyway?";
                            ConflictTopLine.Text = "This can cause major conflicts and issues!";
                            ConflictCancelButton.Visibility = Visibility.Visible;
                            ConflictContinueButton.Visibility = Visibility.Visible;
                            ConflictOKButton.Visibility = Visibility.Collapsed;
                            if (conflict.ImageBytes != null)
                            {
                                try
                                {
                                    using MemoryStream stream = new MemoryStream(conflict.ImageBytes);
                                    BitmapImage bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = stream;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    bitmapImage.Freeze();
                                    ConflictCurrentImage.Source = bitmapImage;
                                }
                                catch (Exception)
                                {
                                }
                            }
                            ConflictCurrentTitle.Text = conflict.Name;
                            ConflictImage.Source = GlobalsGUI.Current.Playbook.Icon;
                            ConflictTitle.Text = ((Playbook)GlobalsGUI.Current.Playbook).Name;
                        }
                        string cache = GlobalsGUI.Current.Playbook.FileNameWithoutExtension;
                        try
                        {
                            // Bounded wait (~60s): if verification never completes, break the
                            // loop and leave the Next button disabled instead of waiting forever.
                            DateTime verificationDeadline = DateTime.UtcNow.AddSeconds(60);
                            while (App.DeCrippleDefender ||
                                   (GlobalsGUI.Current?.Playbook?.FileNameWithoutExtension == cache &&
                                    GlobalsGUI.Current?.Playbook?.VerificationTask?.IsCompleted == false))
                            {
                                if (DateTime.UtcNow >= verificationDeadline)
                                {
                                    return;
                                }
                                await Task.Delay(100);
                            }
                        }
                        catch (NullReferenceException)
                        {
                            return;
                        }
                        if (!(cache != GlobalsGUI.Current.Playbook.FileNameWithoutExtension))
                        {
                            VerificationUnavailableBox.Visibility =
                                GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Unreached
                                    ? Visibility.Visible
                                    : Visibility.Collapsed;
                            if (nextButton || _antivirusOverrideAccepted)
                            {
                                viewModel.MainNextButtonActive = true;
                            }
                            viewModel.MainUpdatesButtonActive = true;
                            viewModel.MainStatusButtonActive = GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Malicious || GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified;
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    if (GlobalsGUI.Current.Playbook == null)
                    {
                        return;
                    }
                    throw;
                }
            };
            ConflictPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(ConflictPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
            };
            Popup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(Popup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
            };
        }

        private static string Text(string key, string fallback)
        {
            return LanguageManager.T(key, fallback);
        }

        private void ApplyLocalizedLabels()
        {
            RunAgainText.Text = Text("Intro.Action.RunAgain", "Executar novamente");
            RunAgainErrorText.Text = Text("Intro.Action.RunAgain", "Executar novamente");
            UpgradeActionText.Text = Text("Intro.Action.Upgrade", "Atualizar");
            UpgradeText.Text = Text("Intro.Upgrade.Available", "Há uma atualização disponível para este APBX");
            AntivirusActionText.Text = Text("Intro.Action.Run", "Executar ação");
            OverrideRecheckText.Text = Text("Button.Recheck", "Verificar novamente");
            SecurityActionTitle.Text = Text("Intro.Security.Title", "Desativar segurança");
            SecurityActionDescription.Text = Text("Intro.Security.Description", "Permitir acesso ao sistema de arquivos do KT WIRZADE");
            SecurityActionText.Text = Text("Intro.Action.Run", "Executar ação");
            AppliedText.Text = Text("Intro.Applied.Current", "Este APBX já está aplicado");
            NextStepText.Text = Text("Intro.NextStep", "O próximo passo verificará se o sistema atende\naos requisitos para continuar.");
            FooterGuidanceText.Text = Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.");
        }

        private void SetSecurityActionVisible(bool visible)
        {
            DefenderBox.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            DisableSecurityStack.Opacity = 1.0;
            DisableSecurityButton.IsEnabled = visible;
        }

        private void Next()
        {
            try
            {
                if (GlobalsGUI.Current.WizardPlaybook.PendingUpdate != null && GlobalsGUI.Current.Playbook.PendingUpdate != null)
                {
                    Popup.IsOpen = true;
                    return;
                }
                if (GlobalsGUI.Current.WizardPlaybook.PendingUpdate != null)
                {
                    UpdatesPopupText.Text = "There are updates available for KT WIRZADE.";
                    Popup.IsOpen = true;
                    return;
                }
                if (GlobalsGUI.Current.Playbook.PendingUpdate != null)
                {
                    UpdatesPopupText.Text = "There are updates available for this Playbook.";
                    Popup.IsOpen = true;
                    return;
                }
            }
            catch
            {
            }
            Continue(this, new RoutedEventArgs());
        }

        private bool HasKTWirzadeIntegriy()
        {
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Windows Defender")))
            {
                return false;
            }
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramData%\\Microsoft\\Windows Defender")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\wuaueng.dll")))
            {
                return false;
            }
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Microsoft\\Edge")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\smartscreen.exe")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\SIHClient.exe")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\StorSvc.dll")))
            {
                return false;
            }
            if (!File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\ameck.exe")))
            {
                return false;
            }
            return true;
        }

        private void ConflictContinueButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            ConflictPopup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                RequirementsPageViewModel requirementsPageViewModel = new RequirementsPageViewModel(new RequirementsPage());
                GlobalsGUI.Current.Playbook.CurrentPage = requirementsPageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = requirementsPageViewModel;
            });
        }

        private void ConflictCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            ConflictPopup.IsOpen = false;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void SlideModuleUp()
        {
            Func<SineEase> ease = () => new SineEase { EasingMode = EasingMode.EaseOut };
            ModuleGrid.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease() });
            moduleTranslate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease() });
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.97, 1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease() });
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.97, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease() });
        }

        private async void Run_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                new SecurityDialog().ShowDialog(owner, ((Playbook)GlobalsGUI.Current.Playbook).Name);
            });
            if (((await GUIUtil.GetDefenderToggles()).All((bool x) => !x) || !Process.GetProcessesByName("MsMpEng").Any()) && base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = true;
                SetSecurityActionVisible(false);
                FooterGuidanceText.Text = Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.");
                NextStepText.Visibility = Visibility.Hidden;
            }
        }

        private async void AntivirusButton_OnClick(object sender, RoutedEventArgs e)
        {
            bool? removed = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                AntivirusDialog antivirusDialog = new AntivirusDialog();
                removed = antivirusDialog.ShowDialog(owner, ((Playbook)GlobalsGUI.Current.Playbook).Name);
            });
            if (!removed.HasValue)
            {
                if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                {
                    SetSecurityActionVisible(true);
                    FooterGuidanceText.Text = Text("Intro.Guidance.Security", "Conclua a ação de segurança acima para continuar.");
                }
                else
                {
                    if (!(base.DataContext is ViewModelBase viewModel))
                    {
                        return;
                    }
                    // Existing bypass path (the removal dialog was dismissed and no
                    // security action is pending): record it so the override state
                    // stays visible on the page.
                    _antivirusFailed = true;
                    _antivirusOverrideAccepted = true;
                    RecordOverride("antivirus");
                    UpdateAntivirusOverrideUi();
                    viewModel.MainNextButtonActive = true;
                    FooterGuidanceText.Text = Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.");
                }
                await SlideModuleDown();
            }
            else if (removed.HasValue && removed.Value)
            {
                System.Windows.Application.Current.Shutdown(0);
            }
        }

        private async void RunAgainButton_OnClick(object sender, RoutedEventArgs e)
        {
            object dataContext = base.DataContext;
            if (!(dataContext is ViewModelBase viewModel))
            {
                return;
            }
            viewModel.MainNextButtonActive = false;
            // A fresh check cycle invalidates any previously accepted override.
            _antivirusFailed = false;
            _antivirusOverrideAccepted = false;
            SetOverrideAccepted(false);
            OverrideBox.Visibility = Visibility.Collapsed;
            FooterGuidanceText.Text = Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.");
            NextStepText.Visibility = Visibility.Collapsed;
            NotRequiredBox.Visibility = Visibility.Collapsed;
            AntivirusBox.Visibility = Visibility.Collapsed;
            SetSecurityActionVisible(false);
            RunAgainButton.IsEnabled = false;
            RunAgainErrorButton.IsEnabled = false;
            UpgradeButton.IsEnabled = false;
            AppliedLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            AppliedLoadContainer.Children.Add(spinner);
            AppliedErrorLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner2 = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            AppliedErrorLoadContainer.Children.Add(spinner2);
            UpgradeLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner3 = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            UpgradeLoadContainer.Children.Add(spinner3);
            await Task.Delay(2000);
            AppliedLoadContainer.Visibility = Visibility.Collapsed;
            AppliedLoadContainer.Children.Remove(spinner);
            AppliedErrorLoadContainer.Visibility = Visibility.Collapsed;
            AppliedErrorLoadContainer.Children.Remove(spinner2);
            UpgradeLoadContainer.Visibility = Visibility.Collapsed;
            UpgradeLoadContainer.Children.Remove(spinner3);
            await SlideModuleDown();
            bool nextButton = true;
            if (GlobalsGUI.Current.Playbook != null)
            {
                bool flag = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6);
                if (flag)
                {
                    flag = !(await Task.Run(delegate
                    {
                        try
                        {
                            if (Convert.ToUInt32(Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Winmgmt").GetValue("Start")) == 4)
                            {
                                GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                            }
                        }
                        catch
                        {
                            GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                        }
                        return (!WizardConfig.Current.IgnoreRemnants.Get()) ? (!WinUtil.GetEnabledAvList(false).Any()) : (!WinUtil.GetEnabledAvList(false).Any((ProviderStatus x) => x.FileExists));
                    }));
                }
                if (flag)
                {
                    nextButton = false;
                    FooterGuidanceText.Text = Text("Intro.Guidance.Antivirus", "Remova o antivírus de terceiros para continuar.");
                    NextStepText.Visibility = Visibility.Hidden;
                    AntivirusBox.Visibility = Visibility.Visible;
                }
                else if (((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)2) || ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)3))
                {
                    if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                    {
                        nextButton = false;
                        NotRequiredBox.Visibility = Visibility.Hidden;
                        NextStepText.Visibility = Visibility.Visible;
                        SetSecurityActionVisible(true);
                        FooterGuidanceText.Text = Text("Intro.Guidance.Security", "Conclua a ação de segurança acima para continuar.");
                    }
                }
                else if (!((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6))
                {
                    NotRequiredBox.Visibility = Visibility.Visible;
                }
                _antivirusFailed = flag;
                UpdateAntivirusOverrideUi();
            }
            if (nextButton || _antivirusOverrideAccepted)
            {
                viewModel.MainNextButtonActive = true;
            }
            FooterGuidanceText.Text = nextButton
                ? Text("Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos.")
                : FooterGuidanceText.Text;
            RunAgainButton.IsEnabled = true;
            RunAgainErrorButton.IsEnabled = true;
            UpgradeButton.IsEnabled = true;
            SlideModuleUp();
        }

        private async Task SlideModuleDown()
        {
            Func<SineEase> ease = () => new SineEase { EasingMode = EasingMode.EaseOut };
            ModuleGrid.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease() });
            moduleTranslate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0.0, 8.0, TimeSpan.FromMilliseconds(210)) { EasingFunction = ease() });
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.0, 0.97, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease() });
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.0, 0.97, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease() });
            await Task.Delay(300);
            AppliedBox.Visibility = Visibility.Collapsed;
            AppliedErrorBox.Visibility = Visibility.Collapsed;
            UpgradeBox.Visibility = Visibility.Collapsed;
            AntivirusBox.Visibility = Visibility.Collapsed;
        }

        private void OpenUpdates(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;

            bool appUpdateAvailable = GlobalsGUI.Current.WizardPlaybook?.PendingUpdate != null;
            PlaybookGUI currentPlaybook = GlobalsGUI.Current.Playbook as PlaybookGUI;
            bool playbookUpdateAvailable = currentPlaybook?.PendingUpdate != null;

            if (appUpdateAvailable && playbookUpdateAvailable)
            {
                MessageBoxResult choice = KTWirzade.GUI.MessageBox.Show(
                    this,
                    "Há uma atualização do KT WIRZADE e outra deste APBX.\n\nSim: ver atualização do aplicativo.\nNão: ver atualização do APBX.",
                    "Escolher atualização",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (choice != MessageBoxResult.Yes && choice != MessageBoxResult.No) return;
                if (choice == MessageBoxResult.No)
                {
                    OpenPlaybookReleasePage(currentPlaybook);
                    return;
                }
            }
            else if (playbookUpdateAvailable)
            {
                OpenPlaybookReleasePage(currentPlaybook);
                return;
            }

            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                // Singleton dialog shared with the title bar; avoids duplicated dialogs.
                owner.ShowUpdatesDialog();
            });
        }

        private void OpenPlaybookReleasePage(Playbook playbook)
        {
            try
            {
                if (playbook == null || !Uri.TryCreate(playbook.Git, UriKind.Absolute, out Uri gitUri) ||
                    !string.Equals(gitUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("O link Git do APBX não é um endereço HTTPS válido.");
                }

                string repositoryPath = gitUri.AbsolutePath.Trim('/');
                if (repositoryPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    repositoryPath = repositoryPath.Substring(0, repositoryPath.Length - 4);
                if (string.IsNullOrWhiteSpace(repositoryPath))
                    throw new InvalidOperationException("O repositório deste APBX não foi identificado.");

                string releasesPath = string.Equals(gitUri.Host, "gitlab.com", StringComparison.OrdinalIgnoreCase)
                    ? repositoryPath + "/-/releases"
                    : repositoryPath + "/releases";
                string releasesUrl = new UriBuilder(Uri.UriSchemeHttps, gitUri.Host, -1, releasesPath).Uri.ToString();
                Process.Start(new ProcessStartInfo(releasesUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Não foi possível abrir as versões do APBX: " + ex.Message, "Erro");
            }
        }

        private void Continue(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                RequirementsPageViewModel requirementsPageViewModel = new RequirementsPageViewModel(new RequirementsPage());
                GlobalsGUI.Current.Playbook.CurrentPage = requirementsPageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = requirementsPageViewModel;
            });
        }
    }
}

