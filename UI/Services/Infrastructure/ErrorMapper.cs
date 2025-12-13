using FactorioModManager.Models;
using System;

namespace FactorioModManager.Services.Infrastructure
{
    public interface IErrorMapper
    {
        string MapToUserMessage(ErrorCode code, Exception? ex = null);
        LogLevel GetLogLevel(ErrorCode code);
    }

    public class ErrorMapper : IErrorMapper
    {
        public string MapToUserMessage(ErrorCode code, Exception? ex = null)
        {
            var baseMessage = code switch
            {
                ErrorCode.FileNotFound => "The requested file could not be found.",
                ErrorCode.InvalidInput => "The input provided was invalid.",
                ErrorCode.NetworkError => "A network error occurred. Please check your connection.",
                ErrorCode.ApiRequestFailed => "Failed to communicate with the mod portal.",
                ErrorCode.InvalidCredentials => "Authentication failed. Please check your credentials.",
                ErrorCode.FileAccessDenied => "Permission denied. Try running as administrator.",
                ErrorCode.CorruptedFile => "The file appears to be corrupted.",
                ErrorCode.OperationCancelled => "The operation was cancelled.",
                ErrorCode.MissingDependencies => "Required dependencies are missing.",
                ErrorCode.UnexpectedError => "An unexpected error occurred.",
                ErrorCode.DownloadFailed => "Download failed.",
                ErrorCode.InvalidFile => "Invalid file format.",
                ErrorCode.InvalidModFormat => "Invalid mod format.",
                ErrorCode.MissingCredentials => "Missing authentication credentials.",
                _ => "An unknown error occurred."
            };

            if (ex != null && code == ErrorCode.UnexpectedError)
            {
                baseMessage += $" ({ex.Message})";
            }

            return baseMessage;
        }

        public LogLevel GetLogLevel(ErrorCode code)
        {
            return code switch
            {
                ErrorCode.OperationCancelled => LogLevel.Info,
                ErrorCode.InvalidInput => LogLevel.Warning,
                ErrorCode.FileNotFound => LogLevel.Warning,
                ErrorCode.NetworkError => LogLevel.Error,
                ErrorCode.ApiRequestFailed => LogLevel.Error,
                ErrorCode.InvalidCredentials => LogLevel.Error,
                ErrorCode.FileAccessDenied => LogLevel.Error,
                ErrorCode.CorruptedFile => LogLevel.Error,
                ErrorCode.MissingDependencies => LogLevel.Warning,
                ErrorCode.UnexpectedError => LogLevel.Error,
                _ => LogLevel.Error
            };
        }
    }
}