using FactorioModManager.Services.Infrastructure;
using System;

namespace FactorioModManager.Models
{
    public interface IErrorHandler
    {
        void Handle(Exception ex, string context, ILogService logService, Action<string>? setStatus = null);
    }

    public class ErrorHandler : IErrorHandler
    {
        public void Handle(Exception ex, string context, ILogService logService, Action<string>? setStatus = null)
        {
            var userMessage = $"Error in {context}: {ex.Message}";
            logService.LogError(context, ex);
            setStatus?.Invoke(userMessage);
        }
    }
}