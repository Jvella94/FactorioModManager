using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FactorioModManager.Views.Base;
using System;

namespace FactorioModManager.Views.Dialogs
{
    public abstract class MessageDialogBase : DialogWindowBase<bool>
    {
        protected string Message { get; }

        protected MessageDialogBase(string title, string message, double width = 450, double height = 180)
        {
            Title = title;
            Width = width;
            Height = height;
            MinWidth = 400;
            MinHeight = 120;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            CanMinimize = false;
            CanMaximize = false;
            Message = message;
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);

            // Build content only after derived constructor has run
            Content ??= BuildContent();
        }

        protected virtual StackPanel BuildContent()
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(0),
                Spacing = 0
            };

            // Message text with improved styling
            var messageBlock = CreateMessageTextBlock();
            mainPanel.Children.Add(messageBlock);

            // Button panel
            mainPanel.Children.Add(CreateButtonPanel());

            return mainPanel;
        }

        protected virtual TextBlock CreateMessageTextBlock()
        {
            return new TextBlock
            {
                Text = Message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 14,
                TextAlignment = TextAlignment.Left,
                LineHeight = 20,
                Margin = new Thickness(24, 18, 24, 12)
            };
        }

        protected abstract Panel CreateButtonPanel();
    }
}