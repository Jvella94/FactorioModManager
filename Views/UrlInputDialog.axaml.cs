using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Views.Base;
using System;

namespace FactorioModManager.Views
{
    public partial class UrlInputDialog : DialogWindowBase<string?>
    {
        public UrlInputDialog()
        {
            InitializeComponent();
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);
            UrlTextBox.Focus();
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                CloseWithResult(url);
            }
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Cancel();
        }
    }
}
