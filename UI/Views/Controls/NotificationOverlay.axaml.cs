using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FactorioModManager.Views.Controls
{
    public partial class NotificationOverlay : UserControl
    {
        private StackPanel? _toastPanel;

        public NotificationOverlay()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _toastPanel = this.FindControl<StackPanel>("ToastPanel");
        }

        public void AddToast(Control toast)
        {
            if (_toastPanel == null) return;
            // allow toasts to be interactive (click to dismiss)
            toast.IsHitTestVisible = true;
            _toastPanel.Children.Add(toast);
        }

        public void RemoveToast(Control toast)
        {
            if (_toastPanel == null) return;
            _toastPanel.Children.Remove(toast);
        }
    }
}