using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services;

namespace FactorioModManager.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
            this.FindControl<ListBox>("LogListBox")!.ItemsSource = LogService.Instance.Logs;
        }

        private async void CopyLogs(object? sender, RoutedEventArgs e)
        {
            var logs = LogService.Instance.GetAllLogs();
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(logs);
            }
        }

        private void ClearLogs(object? sender, RoutedEventArgs e)
        {
            LogService.Instance.Logs.Clear();
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
