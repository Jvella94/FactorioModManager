using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using FactorioModManager.Views.Base;

namespace FactorioModManager.Views.Dialogs
{
    public abstract class MessageDialogBase<TResult> : DialogWindowBase<TResult>
    {
        protected MessageDialogBase(string title, string message, double width = 400, double height = 180)
        {
            Title = title;
            Width = width;
            Height = height;
            MinWidth = 350;
            MinHeight = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            Content = BuildContent(message);
        }

        protected virtual StackPanel BuildContent(string message)
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

            mainPanel.Children.Add(messageBlock);
            mainPanel.Children.Add(CreateButtonPanel());

            return mainPanel;
        }

        protected abstract Panel CreateButtonPanel();
    }
}