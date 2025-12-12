using FactorioModManager.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

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

    public class ErrorMessageService : IErrorMessageService
    {
        public string GetUserFriendlyMessage(Exception exception, string? context = null)
        {
            var contextPrefix = !string.IsNullOrEmpty(context) ? $"[{context}] " : "";

            return exception switch
            {
                // File System Errors
                UnauthorizedAccessException =>
                    $"{contextPrefix}Access denied. Please check file permissions or run as administrator.",

                FileNotFoundException fnf =>
                    $"{contextPrefix}File not found: {Path.GetFileName(fnf.FileName)}. It may have been moved or deleted.",

                DirectoryNotFoundException =>
                    $"{contextPrefix}Directory not found. Please check your mods folder path in Settings.",

                IOException io when io.Message.Contains("used by another process") =>
                    $"{contextPrefix}File is in use by another program. Please close Factorio and any file managers, then try again.",

                IOException io when io.Message.Contains("disk full") =>
                    $"{contextPrefix}Not enough disk space. Please free up some space and try again.",

                IOException =>
                    $"{contextPrefix}File operation failed: {exception.Message}",

                // Network Errors
                HttpRequestException http when http.StatusCode == System.Net.HttpStatusCode.Unauthorized =>
                    $"{contextPrefix}Invalid Factorio credentials. Please check your username and token in Settings.",

                HttpRequestException http when http.StatusCode == System.Net.HttpStatusCode.NotFound =>
                    $"{contextPrefix}Mod not found on the portal. It may have been removed.",

                HttpRequestException http when http.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                    $"{contextPrefix}Too many requests. Please wait a moment and try again.",

                HttpRequestException =>
                    $"{contextPrefix}Network error. Please check your internet connection and try again.",

                TaskCanceledException =>
                    $"{contextPrefix}Request timed out. Please check your internet connection.",

                // JSON/Data Errors
                JsonException =>
                    $"{contextPrefix}Invalid data format. The mod file or response may be corrupted.",

                // Argument Errors
                ArgumentNullException ane =>
                    $"{contextPrefix}Missing required information: {ane.ParamName}",

                ArgumentException =>
                    $"{contextPrefix}Invalid input: {exception.Message}",

                // Default
                _ => $"{contextPrefix}An unexpected error occurred: {exception.Message}"
            };
        }

        public string GetUserFriendlyMessage(ErrorCode errorCode, string? context = null)
        {
            var contextSuffix = !string.IsNullOrEmpty(context) ? $" ({context})" : "";

            return errorCode switch
            {
                ErrorCode.MissingCredentials =>
                    "Factorio credentials not configured. Go to Settings → Add your username and token from factorio.com/profile",

                ErrorCode.InvalidCredentials =>
                    "Invalid Factorio credentials. Please verify your username and token in Settings.",

                ErrorCode.ApiRequestFailed =>
                    $"Failed to fetch data from mod portal{contextSuffix}. The server may be down. Try again later.",

                ErrorCode.DownloadFailed =>
                    $"Download failed{contextSuffix}. Check your internet connection and try again.",

                ErrorCode.FileNotFound =>
                    $"File not found{contextSuffix}. It may have been moved or deleted.",

                ErrorCode.CorruptedFile =>
                    $"Downloaded file is corrupted{contextSuffix}. Please try downloading again.",

                ErrorCode.InvalidModFormat =>
                    $"Invalid mod format{contextSuffix}. The file may not be a valid Factorio mod.",

                ErrorCode.InvalidFile =>
                    $"Invalid file{contextSuffix}. The file appears to be empty or corrupted.",

                ErrorCode.NetworkError =>
                    "Network error. Please check your internet connection.",

                ErrorCode.FileAccessDenied =>
                    "Access denied. Please check file permissions or close any programs using the file.",

                ErrorCode.InvalidInput =>
                    "Invalid input provided. Please check your input and try again.",

                ErrorCode.UnexpectedError =>
                    "An unexpected error occurred. Please check the logs for details.",

                _ => "An unknown error occurred."
            };
        }

        public string GetTechnicalMessage(Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}\n" +
                   $"Stack Trace: {exception.StackTrace}\n" +
                   (exception.InnerException != null
                       ? $"Inner Exception: {exception.InnerException.Message}\n"
                       : "");
        }
    }
}