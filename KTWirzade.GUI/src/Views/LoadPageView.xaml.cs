using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.ViewModels;

namespace KTWirzade.GUI.Views
{
    public partial class LoadPageView : System.Windows.Controls.UserControl
    {
        // Keeps a reference to the waiting spinner so Loaded + DataContextChanged
        // cannot add a second one to the container.
        private Spinner _waitSpinner;

        public LoadPageView()
        {
            InitializeComponent();
            base.DataContextChanged += delegate
            {
                if (base.IsLoaded)
                {
                    OnLoaded(this, null);
                }
            };
            base.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            IDragItem playbook = GlobalsGUI.Current.Playbook;
            IDragItem item = playbook ?? GlobalsGUI.Current.ISO;
            if (item == null)
            {
                return;
            }
            if (item.Checked)
            {
                NextPage();
                return;
            }
            if (_waitSpinner == null)
            {
                _waitSpinner = new Spinner
                {
                    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
                    Opacity = 0.1
                };
                LoadContainer.Children.Add(_waitSpinner);
            }
            Spinner spinner = _waitSpinner;
            DateTime deadline = DateTime.UtcNow.AddSeconds(90);
            while (!item.Checked)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    // Bounded wait: never spin forever if verification never completes.
                    RemoveSpinner(spinner);
                    MessageBox.Show(typeof(MainWindow), "Tempo esgotado aguardando a verificacao do arquivo selecionado. Tente novamente.", "Aguarde", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await Task.Delay(100);
                if (item != GlobalsGUI.Current.ISO && item != GlobalsGUI.Current.Playbook)
                {
                    if (item.FileNameWithoutExtension != GlobalsGUI.Current.ISO?.FileNameWithoutExtension && item.FileNameWithoutExtension != GlobalsGUI.Current.Playbook?.FileNameWithoutExtension)
                    {
                        RemoveSpinner(spinner);
                        return;
                    }
                    playbook = GlobalsGUI.Current.Playbook;
                    item = playbook ?? GlobalsGUI.Current.ISO;
                    if (item == null)
                    {
                        RemoveSpinner(spinner);
                        return;
                    }
                }
            }
            RemoveSpinner(spinner);
            NextPage();
        }

        private void RemoveSpinner(Spinner spinner)
        {
            if (_waitSpinner == spinner)
            {
                _waitSpinner = null;
            }
            if (spinner != null && LoadContainer.Children.Contains(spinner))
            {
                LoadContainer.Children.Remove(spinner);
            }
        }

        private static void NextPage()
        {
            MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
            ViewModelBase newVM = ((GlobalsGUI.Current.Playbook != null) ? ((ViewModelBase)new IntroPageViewModel()) : ((ViewModelBase)new IsoPageViewModel()));
            if (GlobalsGUI.Current.Playbook != null && GlobalsGUI.Current.Playbook.CurrentPage.GetType() != newVM.GetType())
            {
                GlobalsGUI.Current.Playbook.CurrentPage = newVM;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = newVM;
            }
            else if (GlobalsGUI.Current.ISO != null && GlobalsGUI.Current.ISO.CurrentPage.GetType() != newVM.GetType())
            {
                GlobalsGUI.Current.ISO.CurrentPage = newVM;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = newVM;
            }
        }

    }
}
