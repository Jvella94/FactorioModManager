using Avalonia.Controls;
using Avalonia.Layout;

namespace FactorioModManager.Views.Dialogs
{
    /// <summary>
    /// A confirmation dialog with Yes/No buttons
    /// </summary>
    public class ConfirmationDialog(string title, string message) : MessageDialogBase<bool>(title, message)
    {
        protected override Panel CreateButtonPanel()
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Width = 100,
                Height = 32,
                IsDefault = true
            };
            yesButton.Click += (s, e) => CloseWithResult(true);

            var noButton = new Button
            {
                Content = "No",
                Width = 100,
                Height = 32,
                IsCancel = true
            };
            noButton.Click += (s, e) => CloseWithResult(false);

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            return buttonPanel;
        }
    }
}