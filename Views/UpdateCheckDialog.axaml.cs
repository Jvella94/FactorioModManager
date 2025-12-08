using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Views.Base;
using System;

namespace FactorioModManager.Views
{
    public partial class UpdateCheckDialog : DialogWindowBase<(bool Success, int Hours)>
    {
        public UpdateCheckDialog()
        {
            InitializeComponent();
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);
            HoursInput.Focus();
        }

        private void CheckUpdates(object? sender, RoutedEventArgs e)
        {
            var hours = (int?)HoursInput.Value ?? 0;
            if (hours < 1 || hours > 168)
            {
                ShowValidationError("Hours must be between 1 and 168 (1 week)");
                return;
            }

            CloseWithResult((true, hours));
        }

        private void Cancel(object? sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private async void ShowValidationError(string message)
        {
            var dialog = new Dialogs.MessageBoxDialog("Invalid Input", message);
            await dialog.ShowDialog(this);
        }

    }
}
