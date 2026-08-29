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
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
                Opacity = 0.1
            };
            LoadContainer.Children.Add(spinner);
            while (!item.Checked)
            {
                await Task.Delay(100);
                if (item != GlobalsGUI.Current.ISO && item != GlobalsGUI.Current.Playbook)
                {
                    if (item.FileNameWithoutExtension != GlobalsGUI.Current.ISO?.FileNameWithoutExtension && item.FileNameWithoutExtension != GlobalsGUI.Current.Playbook?.FileNameWithoutExtension)
                    {
                        LoadContainer.Children.Remove(spinner);
                        return;
                    }
                    playbook = GlobalsGUI.Current.Playbook;
                    item = playbook ?? GlobalsGUI.Current.ISO;
                    if (item == null)
                    {
                        LoadContainer.Children.Remove(spinner);
                        return;
                    }
                }
            }
            LoadContainer.Children.Remove(spinner);
            NextPage();
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
