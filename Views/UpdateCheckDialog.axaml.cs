using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FactorioModManager.Views
{
    public partial class UpdateCheckDialog : Window
    {
        public int Hours { get; private set; } = 1;

        public UpdateCheckDialog()
        {
            InitializeComponent();
        }

        private void CheckUpdates(object? sender, RoutedEventArgs e)
        {
            Hours = (int)(this.FindControl<NumericUpDown>("HoursInput")?.Value ?? 1);
            Close(true);
        }

        private void Cancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
