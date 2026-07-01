using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OmniFlow.ViewModels;

namespace OmniFlow.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void LogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.VerticalOffset < e.ExtentHeight - e.ViewportHeight - 10)
                {
                    if (!vm.IsPaused) vm.IsPaused = true;
                }
                else
                {
                    if (vm.IsPaused)
                    {
                        vm.IsPaused = false;
                        if (vm.Logs.Count > 0) LogList.ScrollIntoView(vm.Logs[vm.Logs.Count - 1]);
                    }
                }
            }
        }
    }
}