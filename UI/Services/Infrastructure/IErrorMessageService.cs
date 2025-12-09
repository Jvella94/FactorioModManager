using FactorioModManager.Models;
using System;

namespace FactorioModManager.Services.Infrastructure
{
    public interface IErrorMessageService
    {
        /// <summary>
        /// Converts an exception to a user-friendly message
        /// </summary>
        string GetUserFriendlyMessage(Exception exception, string? context = null);

        /// <summary>
        /// Converts an error code to a user-friendly message
        /// </summary>
        string GetUserFriendlyMessage(ErrorCode errorCode, string? context = null);

        /// <summary>
        /// Gets a detailed technical message for logging
        /// </summary>
        string GetTechnicalMessage(Exception exception);
    }
}