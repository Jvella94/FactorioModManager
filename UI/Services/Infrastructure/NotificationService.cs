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
        void Show(string title, string message, NotificationType type);

        Task<bool> ShowConfirmationAsync(string title, string message);
    }

    public class NotificationService(IUIService uiService) : INotificationService
    {
        private readonly IUIService _uiService = uiService;

        public void Show(string title, string message, NotificationType type)
        {
            // Show toast notification or banner
            _uiService.Post(() =>
            {
                // Implementation depends on your UI framework
                // Could show a toast, banner, or dialog
            });
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            // Show confirmation dialog
            return await _uiService.ShowConfirmationAsync(title, message, null);
        }
    }
}