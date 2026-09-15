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

        public IsoRequirementsPageView()
        {
            InitializeComponent();
            base.DataContextChanged += delegate
            {
                if (base.DataContext != null)
                {
                    ViewModel.MainNextButtonActive = false;
                    bool flag = false;
                    ActivationContainer.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
                    SystemCheckContainer.Opacity = (flag ? 0.6 : 1.0);
                    PendingUpdatesActionText.Text = "Run action";
                    if (ViewModel.MetRequirements != null && ViewModel.IsBuildSupported.HasValue)
                    {
                        SystemCheckContainer.Opacity = 1.0;
                        UpdateSystemCheckDisplay().Wait();
                        ActivationBar.Visibility = Visibility.Collapsed;
                        SystemBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ActivationBar.Visibility = Visibility.Visible;
                        SystemBar.Visibility = Visibility.Visible;
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
                        DispatcherTimer dispatcherTimer = new DispatcherTimer();
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
                        dispatcherTimer.Tick += async delegate (object sender, EventArgs args)
                        {
                            await dispatcherTimer_Tick(sender, args);
                        };
                        dispatcherTimer.Start();
                        base.Loaded += CheckRequirements;
                    }
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (false)
            {
                ActivationBar.Start();
            }
            else
            {
                SystemBar.Start();
            }
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
            catch (NullReferenceException)
            {
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
                if (AreRequirementsMet())
                {
                    ViewModel.MainNextButtonActive = true;
                }
                ViewModel.MainPreviousButtonActive = true;
            }
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(((Hyperlink)sender).NavigateUri.ToString());
        }

        private async Task dispatcherTimer_Tick(object sender, EventArgs e)
        {
            _ = 2;
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
            catch (NullReferenceException)
            {
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
            ViewModel.ResultTextVisibility = Visibility.Collapsed;
            ViewModel.SystemCheckStatusText = "Analyzing Image...";
            ViewModel.SystemCheckIcon = null;
            SystemBar.Visibility = Visibility.Visible;
            SystemBar.Start();
            ViewModel.MetRequirements = null;
            TickCount = 0;
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
            dispatcherTimer.Tick += async delegate (object send, EventArgs args)
            {
                args = new RoutedEventArgs(null, sender);
                await dispatcherTimer_Tick(send, args);
            };
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
            var result = MessageBox.Show(
                typeof(MainWindow),
                $"Este playbook não foi feito para a versão do Windows desta imagem.\n\nBuild da imagem: {imageBuild}\nBuilds suportadas: {supportedBuilds}\n\nAplicar mesmo assim pode causar instabilidade ou erros. Deseja continuar por sua conta e risco?",
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
