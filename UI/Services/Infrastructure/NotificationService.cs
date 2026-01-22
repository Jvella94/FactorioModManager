using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Infrastructure
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public interface INotificationService
    {
        void Show(string title, string message, NotificationType type = NotificationType.Info);

        Task<bool> ShowConfirmationAsync(string title, string message);
    }

    public class NotificationService(IUIService uiService) : INotificationService
    {
        private readonly IUIService _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        private readonly List<Control> _activeToasts = [];
        private readonly Lock _lock = new();

        // Toast sizing / spacing
        private const int _toastMaxWidth = 420; // reduced max width to avoid overly wide toasts

        private const int _toastHeight = 72;
        private const int _toastSpacing = 8;

        public void Show(string title, string message, NotificationType type = NotificationType.Info)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var overlay = FindOverlay();
                    var toast = CreateToastControl(title, message, type);

                    if (overlay == null)
                    {
                        // fallback to old window-based toast: size to content but cap width
                        var popup = new Window
                        {
                            CanResize = false,
                            SystemDecorations = SystemDecorations.None,
                            Topmost = true,
                            ShowInTaskbar = false,
                            Content = toast,
                            SizeToContent = SizeToContent.WidthAndHeight,
                            MaxWidth = _toastMaxWidth
                        };

                        PositionWindowBottomRight(popup);
                        lock (_lock) { _activeToasts.Add(popup); }
                        popup.Show();
                        await Task.Delay(3500);
                        lock (_lock) { try { popup.Close(); } catch { } _activeToasts.Remove(popup); }
                        return;
                    }

                    overlay.AddToast(toast);
                    lock (_lock) _activeToasts.Add(toast);

                    // Auto remove after delay
                    await Task.Delay(3500);
                    Dispatcher.UIThread.Post(() =>
                    {
                        overlay.RemoveToast(toast);
                        lock (_lock) _activeToasts.Remove(toast);
                    });
                }
                catch { }
            });
        }

        private Views.Controls.NotificationOverlay? FindOverlay()
        {
            var owner = _uiService.GetMainWindow();
            if (owner == null) return null;

            // Try find by name first
            var byName = owner.FindControl<Views.Controls.NotificationOverlay>("NotificationOverlay");
            if (byName != null) return byName;

            // Search visual tree for the overlay type
            foreach (var v in owner.GetVisualDescendants())
            {
                if (v is Views.Controls.NotificationOverlay no) return no;
            }

            return null;
        }

        private Border CreateToastControl(string title, string message, NotificationType type)
        {
            // Use icon bitmaps from Assets when available (green check, warning, error, info)
            IImage? iconImg = null;
            string? asset = type switch
            {
                NotificationType.Success => "avares://FactorioModManager/Assets/check.png",
                NotificationType.Warning => "avares://FactorioModManager/Assets/warning.png",
                NotificationType.Error => "avares://FactorioModManager/Assets/error.png",
                _ => "avares://FactorioModManager/Assets/info.png"
            };
            try
            {
                var uri = new Uri(asset);
                var stream = AssetLoader.Open(uri);
                iconImg = new Bitmap(stream);
            }
            catch { iconImg = null; }

            var icon = new Image
            {
                Source = iconImg,
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                FontSize = 14,
                MaxWidth = _toastMaxWidth - 100
            };

            var msgBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                MaxWidth = _toastMaxWidth - 120 // leave room for icon and padding
            };

            var bg = type switch
            {
                NotificationType.Success => Color.Parse("#2E7D32"),
                NotificationType.Warning => Color.Parse("#F9A825"),
                NotificationType.Error => Color.Parse("#C62828"),
                _ => Color.Parse("#263238")
            };

            var contentStack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
            };
            contentStack.Children.Add(icon);

            var textStack = new StackPanel { Spacing = 2 };
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(msgBlock);
            contentStack.Children.Add(textStack);

            var content = new Border
            {
                Background = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Child = contentStack
            };

            // Host aligned right and allow sizing to content up to a maximum
            var host = new Border
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                MaxWidth = _toastMaxWidth,
                Height = Double.NaN,
                Child = content,
                Margin = new Thickness(0)
            };

            // Allow click to dismiss
            host.PointerPressed += (_, __) =>
            {
                var overlay = FindOverlay();
                overlay?.RemoveToast(host);
                lock (_lock) _activeToasts.Remove(host);
            };

            return host;
        }

        private static void PositionWindowBottomRight(Window w)
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var owner = desktop.MainWindow;
                    var x = (int)(owner.Position.X + owner.ClientSize.Width - (w.Width > 0 ? w.Width : _toastMaxWidth) - 16);
                    var y = (int)(owner.Position.Y + owner.ClientSize.Height - (w.Height > 0 ? w.Height : _toastHeight) - 16);
                    w.Position = new PixelPoint(x, y);
                }
            }
            catch { }
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return _uiService.ShowConfirmationAsync(title, message, _uiService.GetMainWindow());
        }
    }
}