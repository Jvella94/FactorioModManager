using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FactorioModManager.Views.Dialogs
{
    /// <summary>
    /// A generic confirmation dialog with customizable Yes/No buttons
    /// </summary>
    public class ConfirmationDialog(
        string title,
        string message,
        string yesButtonText = "Yes",
        string noButtonText = "No",
        string? yesButtonColor = null,
        string? noButtonColor = null) : MessageDialogBase(title, message)
    {
        private readonly string _yesButtonText = yesButtonText;
        private readonly string _noButtonText = noButtonText;
        private readonly string? _yesButtonColor = yesButtonColor;
        private readonly string? _noButtonColor = noButtonColor;

        protected override Panel CreateButtonPanel()
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 40,
                Margin = new Thickness(0, 0, 20, 20)
            };

            // Yes/confirm on the LEFT
            var yesButton = new Button
            {
                Content = _yesButtonText,
                Width = 110,
                Height = 32,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                IsDefault = true,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse(_yesButtonColor ?? "#4CAF50")),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            var noButton = new Button
            {
                Content = _noButtonText,
                Width = 110,
                Height = 32,
                FontSize = 13,
                IsCancel = true,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse(_noButtonColor ?? "#3A3A3A")),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            yesButton.Click += (s, e) => CloseWithResult(true);
            noButton.Click += (s, e) => CloseWithResult(false);

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            return buttonPanel;
        }
    }
}