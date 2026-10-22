using Core;
using iso_mode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KTWirzade.GUI.Pages.IsoModePage;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static KTWirzade.Shared.Requirements;

namespace KTWirzade.GUI.Views
{
    public partial class IsoRequirementsPageView : System.Windows.Controls.UserControl
    {
        private enum IconType
        {
            Default,
            Checkmark,
            Warning
        }

        private int TickCount;

        private bool _buildBypassed;

        private IsoRequirementsPageViewModel ViewModel => (IsoRequirementsPageViewModel)base.DataContext;

        // Single timer for the requirements polling; re-created on each
        // DataContextChanged / re-check, after stopping and detaching the previous one.
        private DispatcherTimer _checkTimer;

        private IconType CurrentActivationIcon
        {
            set
            {
                switch (value)
                {
                    case IconType.Default:
                        ViewModel.ActivationIcon = null;
                        break;
                    case IconType.Checkmark:
                        ViewModel.ActivationIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/checkmark_green_gradient_64.png"));
                        break;
                    case IconType.Warning:
                        ViewModel.ActivationIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_64.png"));
                        break;
                }
            }
        }

        private IconType CurrentSystemCheckIcon
        {
            set
            {
                switch (value)
                {
                    case IconType.Default:
                        ViewModel.SystemCheckIcon = null;
                        break;
                    case IconType.Checkmark:
                        ViewModel.SystemCheckIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/checkmark_green_gradient_64.png"));
                        break;
                    case IconType.Warning:
                        ViewModel.SystemCheckIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_64.png"));
                        break;
                }
            }
        }

        private bool AreRequirementsMet()
        {
            if (ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue)
            {
                throw new Exception("AreRequirementsMet was called before requirements were checked.");
            }
            if (!ViewModel.IsBuildSupported.Value && !_buildBypassed)
            {
                return false;
            }
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements;
            if (requirements == null)
            {
                return true;
            }
            return !requirements.Except(ViewModel.MetRequirements).Any();
        }

        private sealed class FailedCheck
        {
            public string Id;

            public string Label;
        }

        // Session-wide record of the optional checks the user explicitly chose to
        // override (in-memory only; which check id was overridden and when).
        private sealed class RequirementOverrideRecord
        {
            public string CheckId;

            public DateTime TimestampUtc;
        }

        private static readonly List<RequirementOverrideRecord> _sessionOverrides = new List<RequirementOverrideRecord>();

        // Set while the override checkbox is changed programmatically so the
        // Checked/Unchecked handlers do not re-enter (or re-prompt the user).
        private bool _settingOverride;

        // Signature of the last seen set of failing bypassable checks; when it
        // changes, the previous acknowledgement no longer applies and is cleared.
        private string _bypassSignature;

        // BLOCKING checks: this page only evaluates image build compatibility and
        // connectivity; nothing here can corrupt the system (the image build
        // mismatch already ships its own explicit "apply anyway" confirmation),
        // so nothing is classified as an unconditional blocker on this page.
        private void CollectBlockingFailedChecks(List<FailedCheck> failed)
        {
        }

        // BYPASSABLE checks: the user may consciously continue past these after an
        // explicit acknowledgement (warn + confirm).
        private void CollectBypassableFailedChecks(List<FailedCheck> failed)
        {
            if (ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue)
            {
                return;
            }
            if (!ViewModel.IsBuildSupported.Value && !_buildBypassed)
            {
                failed.Add(new FailedCheck { Id = "image-build", Label = "Versão da imagem não suportada" });
            }
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements;
            if (requirements == null)
            {
                return;
            }
            if (requirements.Contains((Requirement)0) && !ViewModel.MetRequirements.Contains((Requirement)0))
            {
                failed.Add(new FailedCheck { Id = "internet", Label = "Sem conexão com a internet" });
            }
            if (requirements.Contains((Requirement)1) && !ViewModel.MetRequirements.Contains((Requirement)1))
            {
                failed.Add(new FailedCheck { Id = "no-internet", Label = "Conexão com a internet ativa (deve estar desconectada)" });
            }
            if (requirements.Contains((Requirement)9) && !ViewModel.MetRequirements.Contains((Requirement)9))
            {
                failed.Add(new FailedCheck { Id = "battery", Label = "Dispositivo não conectado à energia" });
            }
        }

        // Single gate for the Next button: enabled only when every blocking check
        // passes AND either every bypassable check passes or the user checked the
        // override box after being warned.
        private void ApplyOverrideStateAndGate()
        {
            List<FailedCheck> blocking = new List<FailedCheck>();
            List<FailedCheck> bypassable = new List<FailedCheck>();
            CollectBlockingFailedChecks(blocking);
            CollectBypassableFailedChecks(bypassable);
            if (blocking.Count > 0)
            {
                // Hard blockers can never be overridden.
                SetOverrideAccepted(false);
                _bypassSignature = null;
                OverrideBox.Visibility = Visibility.Collapsed;
                ViewModel.MainNextButtonActive = false;
                return;
            }
            if (bypassable.Count == 0)
            {
                // Everything passes again: the override is no longer needed.
                SetOverrideAccepted(false);
                _bypassSignature = null;
                OverrideBox.Visibility = Visibility.Collapsed;
                if (ViewModel.MetRequirements != null && ViewModel.IsBuildSupported.HasValue && AreRequirementsMet())
                {
                    ViewModel.MainNextButtonActive = true;
                }
                return;
            }
            string signature = string.Join("\n", bypassable.ConvertAll((FailedCheck x) => x.Id));
            if (signature != _bypassSignature)
            {
                // The situation changed: a fresh acknowledgement is required.
                _bypassSignature = signature;
                SetOverrideAccepted(false);
            }
            OverrideFailedText.Text = "Verificações pendentes que você pode ignorar por sua conta e risco:\n- " + string.Join("\n- ", bypassable.ConvertAll((FailedCheck x) => x.Label));
            OverrideBox.Visibility = Visibility.Visible;
            UpdateOverrideBadge(bypassable.Count);
            ViewModel.MainNextButtonActive = AreRequirementsMet() || OverrideAcceptBox.IsChecked == true;
        }

        private void UpdateOverrideBadge(int count)
        {
            bool accepted = OverrideAcceptBox.IsChecked == true;
            OverrideBadgeText.Visibility = accepted ? Visibility.Visible : Visibility.Collapsed;
            OverrideBadgeText.Text = count + ((count == 1) ? " verificação ignorada pelo usuário" : " verificações ignoradas pelo usuário");
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

        private void RecordRequirementOverrides(List<FailedCheck> bypassable)
        {
            List<string> ids = new List<string>();
            foreach (FailedCheck check in bypassable)
            {
                _sessionOverrides.Add(new RequirementOverrideRecord { CheckId = check.Id, TimestampUtc = DateTime.UtcNow });
                ids.Add(check.Id);
            }
            Log.WriteSafe((LogType)1, "Verificações opcionais ignoradas pelo usuário: " + string.Join(", ", ids.ToArray()) + ".", (SerializableTrace)null, Array.Empty<(string, object)>());
        }

        private void OverrideAcceptBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_settingOverride)
            {
                return;
            }
            List<FailedCheck> bypassable = new List<FailedCheck>();
            CollectBypassableFailedChecks(bypassable);
            if (bypassable.Count == 0)
            {
                SetOverrideAccepted(false);
                ApplyOverrideStateAndGate();
                return;
            }
            List<string> labels = bypassable.ConvertAll((FailedCheck x) => x.Label);
            var result = MessageBox.Show(
                typeof(MainWindow),
                "As verificações abaixo não foram atendidas:\n- " + string.Join("\n- ", labels) + "\n\nContinuar mesmo assim pode causar instabilidade ou erros. Deseja continuar por sua conta e risco?",
                "Continuar mesmo assim?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                // Cancelled: the override stays unchecked and Next remains disabled.
                SetOverrideAccepted(false);
                ApplyOverrideStateAndGate();
                return;
            }
            RecordRequirementOverrides(bypassable);
            ApplyOverrideStateAndGate();
        }

        private void OverrideAcceptBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_settingOverride)
            {
                return;
            }
            ApplyOverrideStateAndGate();
        }

        private void OverrideRecheckButton_OnClick(object sender, RoutedEventArgs e)
        {
            CheckInternetButton_OnClick(sender, e);
        }

        public IsoRequirementsPageView()
        {
            InitializeComponent();
            base.DataContextChanged += async delegate
            {
                if (base.DataContext != null)
                {
                    ViewModel.MainNextButtonActive = false;
                    // A fresh check cycle invalidates any previously accepted override.
                    SetOverrideAccepted(false);
                    _bypassSignature = null;
                    OverrideBox.Visibility = Visibility.Collapsed;
                    bool flag = false;
                    ActivationContainer.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
                    SystemCheckContainer.Opacity = (flag ? 0.6 : 1.0);
                    PendingUpdatesActionText.Text = "Run action";
                    if (ViewModel.MetRequirements != null && ViewModel.IsBuildSupported.HasValue)
                    {
                        SystemCheckContainer.Opacity = 1.0;
                        // Awaited (never .Wait()): sync-over-async on the UI thread can deadlock/freeze.
                        await UpdateSystemCheckDisplay();
                        ActivationBar.Visibility = Visibility.Collapsed;
                        SystemBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ActivationBar.Visibility = Visibility.Visible;
                        SystemBar.Visibility = Visibility.Visible;
                        base.Loaded -= OnLoaded;
                        base.Loaded += OnLoaded;
                        if (flag)
                        {
                            SystemBar.Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            SystemBar.Visibility = Visibility.Visible;
                        }
                        ViewModel.ProgressBarVisibility = Visibility.Visible;
                        // Stop/detach any previous polling timer + handlers before resubscribing.
                        _checkTimer?.Stop();
                        _checkTimer = null;
                        base.Loaded -= CheckRequirements;
                        DispatcherTimer dispatcherTimer = new DispatcherTimer();
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
                        dispatcherTimer.Tick += async delegate (object sender, EventArgs args)
                        {
                            await dispatcherTimer_Tick(sender, args);
                        };
                        _checkTimer = dispatcherTimer;
                        dispatcherTimer.Start();
                        base.Loaded += CheckRequirements;
                    }
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SystemBar.Start();
            base.Loaded -= OnLoaded;
        }

        private async void CheckRequirements(object sender, RoutedEventArgs e)
        {
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements;
            string[] builds = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).SupportedBuilds;
            if (IsoFeaturesPane.SystemOEMDriversSize == null)
            {
                await Task.Run(delegate
                {
                    try
                    {
                        ulong num = 0uL;
                        string[] driverPathsFromOEMInfs = DriverManager.GetDriverPathsFromOEMInfs();
                        for (int i = 0; i < driverPathsFromOEMInfs.Length; i++)
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(driverPathsFromOEMInfs[i]);
                            num += (ulong)directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum((FileInfo file) => file.Length);
                        }
                        IsoFeaturesPane.SystemOEMDriversSize = StringUtils.HumanReadableBytes(num);
                    }
                    catch (Exception ex2)
                    {
                        Log.EnqueueExceptionSafe(ex2, Array.Empty<(string, object)>());
                    }
                });
            }
            try
            {
                ViewModel.IsBuildSupported = builds == null || !GlobalsGUI.Current.ISO.WinVer.HasValue || builds.Contains(GlobalsGUI.Current.ISO.WinVer.Value.ToString());
                IsoRequirementsPageViewModel viewModel = ViewModel;
                Requirement[] metRequirements = ((requirements == null) ? Enum.GetValues(typeof(Requirement)).Cast<Requirement>().ToArray() : (await Task.Run(delegate
                {
                    List<Requirement> list = ((Requirement[])Enum.GetValues(typeof(Requirement))).ToList();
                    if (requirements.Contains((Requirement)0))
                    {
                        Task<bool> task = new Internet().IsMet();
                        task.Wait();
                        if (!task.Result)
                        {
                            list.Remove((Requirement)0);
                        }
                    }
                    return list.ToArray();
                })));
                viewModel.MetRequirements = metRequirements;
            }
            catch (NullReferenceException ex)
            {
                // Previously swallowed silently, leaving the page stuck with no feedback.
                Log.EnqueueExceptionSafe(ex, Array.Empty<(string, object)>());
                if (base.DataContext is IsoRequirementsPageViewModel vm)
                {
                    SetOverrideAccepted(false);
                    OverrideBox.Visibility = Visibility.Collapsed;
                    vm.SystemCheckStatusText = "Falha ao verificar os requisitos";
                    vm.SystemCheckResultText = "Ocorreu um erro ao verificar os requisitos da imagem. Use o botao de re-verificar.";
                    vm.ResultTextVisibility = Visibility.Visible;
                    vm.MainNextButtonActive = false;
                }
            }
            base.Loaded -= CheckRequirements;
        }

        private async Task UpdateSystemCheckDisplay()
        {
            if (base.DataContext is ViewModelBase)
            {
                _ = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements;
                IconType systemCheckIcon = IconType.Checkmark;
                string systemCheckStatusText = "Requirements met";
                string systemCheckResultText = "The image meets all requirements for this Playbook";
                bool systemCheckLocked = false;
                if (!ViewModel.IsBuildSupported.Value)
                {
                    if (_buildBypassed)
                    {
                        BuildMismatchBox.Visibility = Visibility.Collapsed;
                        systemCheckIcon = IconType.Warning;
                        systemCheckStatusText = "Versao da imagem ignorada";
                        systemCheckResultText = "Aplicando por sua conta e risco";
                    }
                    else
                    {
                        BuildMismatchBox.Visibility = Visibility.Visible;
                        systemCheckIcon = IconType.Warning;
                        systemCheckStatusText = "Requirements not met";
                        systemCheckResultText = "This Windows image is not supported by this Playbook.";
                        systemCheckLocked = true;
                    }
                }
                else
                {
                    BuildMismatchBox.Visibility = Visibility.Collapsed;
                }
                if (!ViewModel.MetRequirements.Contains((Requirement)9) && !systemCheckLocked)
                {
                    CheckBatteryBox.Visibility = Visibility.Visible;
                    systemCheckStatusText = "Requirements not met";
                    systemCheckResultText = "Device must be plugged in before continuing";
                    systemCheckIcon = IconType.Warning;
                    systemCheckLocked = true;
                }
                else
                {
                    CheckBatteryBox.Visibility = Visibility.Collapsed;
                }
                if (!ViewModel.MetRequirements.Contains((Requirement)1) && !systemCheckLocked)
                {
                    CheckNoInternetBox.Visibility = Visibility.Visible;
                    systemCheckStatusText = "Requirements not met";
                    systemCheckResultText = "Internet must be disconnected before continuing";
                    systemCheckIcon = IconType.Warning;
                    systemCheckLocked = true;
                }
                else
                {
                    CheckNoInternetBox.Visibility = Visibility.Collapsed;
                }
                if (!ViewModel.MetRequirements.Contains((Requirement)0) && !systemCheckLocked)
                {
                    CheckInternetBox.Visibility = Visibility.Visible;
                    systemCheckStatusText = "Requirements not met";
                    systemCheckResultText = "Internet must be connected before continuing";
                    systemCheckIcon = IconType.Warning;
                }
                else
                {
                    CheckInternetBox.Visibility = Visibility.Collapsed;
                }
                CurrentSystemCheckIcon = systemCheckIcon;
                ViewModel.SystemCheckStatusText = systemCheckStatusText;
                ViewModel.SystemCheckResultText = systemCheckResultText;
                ViewModel.ResultTextVisibility = Visibility.Visible;
                ViewModel.ProgressBarVisibility = Visibility.Collapsed;
                ApplyOverrideStateAndGate();
                ViewModel.MainPreviousButtonActive = true;
            }
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(((Hyperlink)sender).NavigateUri.ToString());
        }

        private async Task dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if ((ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue) && TickCount < 2)
                {
                    TickCount++;
                    return;
                }
                DispatcherTimer dispatchTimer = (DispatcherTimer)sender;
                if (ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue)
                {
                    TickCount++;
                    return;
                }
                dispatchTimer.Stop();
                await SystemBar.WaitForAnimation();
                await Task.Delay(100);
                SystemBar.Visibility = Visibility.Hidden;
                await UpdateSystemCheckDisplay();
                try
                {
                    ((System.Windows.Controls.Button)((RoutedEventArgs)e).Source).IsEnabled = true;
                }
                catch (InvalidCastException)
                {
                }
            }
            catch (NullReferenceException ex)
            {
                // Previously swallowed silently, leaving the page stuck with no feedback.
                Log.EnqueueExceptionSafe(ex, Array.Empty<(string, object)>());
                if (base.DataContext is IsoRequirementsPageViewModel vm)
                {
                    SetOverrideAccepted(false);
                    OverrideBox.Visibility = Visibility.Collapsed;
                    vm.SystemCheckStatusText = "Falha ao verificar os requisitos";
                    vm.SystemCheckResultText = "Ocorreu um erro ao verificar os requisitos da imagem. Use o botao de re-verificar.";
                    vm.ResultTextVisibility = Visibility.Visible;
                    vm.MainNextButtonActive = false;
                }
            }
        }

        private void activationAccept_Checked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            if (base.DataContext is ViewModelBase ViewModel)
            {
                ViewModel.MainNextButtonActive = button.IsChecked == true;
            }
        }

        private async void CheckInternetButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
            // A new verification run invalidates any previously accepted override.
            SetOverrideAccepted(false);
            _bypassSignature = null;
            OverrideBox.Visibility = Visibility.Collapsed;
            ViewModel.ResultTextVisibility = Visibility.Collapsed;
            ViewModel.SystemCheckStatusText = "Analyzing Image...";
            ViewModel.SystemCheckIcon = null;
            SystemBar.Visibility = Visibility.Visible;
            SystemBar.Start();
            ViewModel.MetRequirements = null;
            TickCount = 0;
            // Stop the previous polling timer (if any) before starting a new one.
            _checkTimer?.Stop();
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
            dispatcherTimer.Tick += async delegate (object send, EventArgs args)
            {
                args = new RoutedEventArgs(null, sender);
                await dispatcherTimer_Tick(send, args);
            };
            _checkTimer = dispatcherTimer;
            dispatcherTimer.Start();
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Requirements;
            IsoRequirementsPageViewModel viewModel = ViewModel;
            Requirement[] metRequirements = ((requirements == null) ? Enum.GetValues(typeof(Requirement)).Cast<Requirement>().ToArray() : (await Task.Run(delegate
            {
                //IL_0028: Unknown result type (might be due to invalid IL or missing references)
                List<Requirement> list = ((Requirement[])Enum.GetValues(typeof(Requirement))).ToList();
                if (requirements.Contains((Requirement)0))
                {
                    Task<bool> task = new Internet().IsMet();
                    task.Wait();
                    if (!task.Result)
                    {
                        list.Remove((Requirement)0);
                    }
                }
                return list.ToArray();
            })));
            viewModel.MetRequirements = metRequirements;
        }

        // Intentionally a no-op: this handler is wired from the XAML (Prepare button)
        // but preparation is not applicable in ISO mode, so nothing is done here.
        private void PrepareButton_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void CheckBatteryButton_OnClick(object sender, RoutedEventArgs e)
        {
            CheckInternetButton_OnClick(sender, e);
        }

        private void TweaksButton_OnClick(object sender, RoutedEventArgs e)
        {
            new TweaksDialog().ShowDialog();
            CheckInternetButton_OnClick(sender, e);
        }

        private void CheckNoInternetButton_OnClick(object sender, RoutedEventArgs e)
        {
            CheckInternetButton_OnClick(sender, e);
        }

        private async void PendingUpdatesButton_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void BypassButton_OnClick(object sender, RoutedEventArgs e)
        {
            var playbook = (Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook;
            var result = MessageBox.Show(
                typeof(MainWindow),
                "Ignorar a condição de \"instalação fresca\" pode apontar o playbook para um sistema com restos de tweaks e configurações antigas, o que pode causar comportamento inesperado depois da aplicação.\n\nDeseja ignorar esta condição por sua conta e risco?",
                "Ignorar instalação fresca",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            if (playbook.Requirements != null)
            {
                playbook.Requirements = playbook.Requirements.Where((Requirement x) => (int)x != 12).ToArray();
            }
            FreshInstallBox.Visibility = Visibility.Collapsed;
        }

        private async void BypassBuildButton_OnClick(object sender, RoutedEventArgs e)
        {
            var playbook = (Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook;
            string imageBuild = GlobalsGUI.Current.ISO.WinVer.HasValue ? GlobalsGUI.Current.ISO.WinVer.Value.ToString() : "(desconhecida)";
            string supportedBuilds = playbook.SupportedBuilds != null ? string.Join(", ", playbook.SupportedBuilds) : "(qualquer)";
            int.TryParse(imageBuild, out int imageBuildNumber);
            string imageFamily = imageBuildNumber >= 22000 ? "Windows 11" : "Windows 10";
            var result = MessageBox.Show(
                typeof(MainWindow),
                $"Este playbook não foi feito para a versão do Windows desta imagem.\n\nBuild da imagem: {imageBuild} ({imageFamily})\nBuilds suportadas: {supportedBuilds}\n\nSe as builds listadas forem de outra família do Windows (por exemplo, um playbook do Windows 10 aplicado no Windows 11), o risco é alto. Se for apenas uma build mais nova da mesma família, o risco é menor, mas ainda existe.\n\nAplicar por sua conta e risco mesmo assim?",
                "Versão não suportada",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            _buildBypassed = true;
            BuildMismatchBox.Visibility = Visibility.Collapsed;
            await UpdateSystemCheckDisplay();
        }

        private void ViewInstallGuideButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).InstallGuide ?? "https://docs.ameliorated.io/using-wizard/install-windows.html");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(MainWindow), "Install guide link is invalid.", "Warning");
            }
        }

        private void Hyperlink_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
        }

        private void Hyperlink_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = null;
        }
    }
}
