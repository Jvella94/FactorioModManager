using Avalonia.Controls;
using Avalonia.Layout;

namespace FactorioModManager.Views.Dialogs
{
    /// <summary>
    /// A simple message box dialog for displaying information to the user
    /// </summary>
    // MessageBoxDialog.cs
    public class MessageBoxDialog : MessageDialogBase<bool>
    {
        public MessageBoxDialog(string title, string message)
            : base(title, message, width: 450, height: 200)
        {
            CanResize = true;
        }

        protected override Panel CreateButtonPanel()
        {
            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsDefault = true
            };
            okButton.Click += (s, e) => CloseWithResult(true);

            var panel = new StackPanel();
            panel.Children.Add(okButton);
            return panel;
        }
    }
}