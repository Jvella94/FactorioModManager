using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FactorioModManager.Views.Base;
using System;

namespace FactorioModManager.Views.Dialogs
{
    /// <summary>
    /// A simple message box dialog for displaying information to the user
    /// </summary>
    public class MessageBoxDialog : DialogWindowBase<bool>
    {
        public MessageBoxDialog(string title, string message)
        {
            Title = title;
            Width = 450;
            Height = 200;
            MinWidth = 350;
            MinHeight = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = true;

            BuildUI(message);
        }

        private void BuildUI(string message)
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20
            };

            // Message text
            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top
            };

            // OK Button
            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsDefault = true
            };

            okButton.Click += OkButton_Click;

            mainPanel.Children.Add(messageBlock);
            mainPanel.Children.Add(okButton);

            Content = mainPanel;
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseWithResult(true);
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);
            // Focus is handled automatically by IsDefault on OK button
        }
    }
}