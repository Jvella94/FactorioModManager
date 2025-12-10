using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FactorioModManager.Views.Dialogs
{
    /// <summary>
    /// A simple message box dialog for displaying information to the user
    /// </summary>
    public class MessageBoxDialog : MessageDialogBase
    {
        public MessageBoxDialog(string title, string message)
            : base(title, message, width: 450, height: 180)
        {
            CanResize = false;
        }

        protected override Panel CreateButtonPanel()
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 36,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsDefault = true,
                Background = new SolidColorBrush(Color.Parse("#4CAF50")),
                Foreground = Brushes.White
            };
            okButton.Click += (s, e) => CloseWithResult(true);

            panel.Children.Add(okButton);
            return panel;
        }
    }
}