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
    /// A confirmation dialog with Yes/No buttons
    /// </summary>
    public class ConfirmationDialog : DialogWindowBase<bool>
    {
        public ConfirmationDialog(string title, string message)
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

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
                FontSize = 14
            };

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            // Yes button
            var yesButton = new Button
            {
                Content = "Yes",
                Width = 100,
                Height = 32,
                IsDefault = true
            };
            yesButton.Click += YesButton_Click;

            // No button
            var noButton = new Button
            {
                Content = "No",
                Width = 100,
                Height = 32,
                IsCancel = true
            };
            noButton.Click += NoButton_Click;

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            mainPanel.Children.Add(messageBlock);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseWithResult(true);
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseWithResult(false);
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);
            // Focus is handled automatically by IsDefault on Yes button
        }
    }
}