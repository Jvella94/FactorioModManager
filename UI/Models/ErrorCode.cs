using System.ComponentModel;
using System.Reflection;

namespace FactorioModManager.Models
{
    public enum ErrorCode
    {
        [Description("No error occurred")]
        None = 0,

        // Authentication
        [Description("Missing authentication credentials")]
        MissingCredentials = 100,

        [Description("Provided credentials are invalid")]
        InvalidCredentials = 101,

        // Network
        [Description("Network connectivity issue")]
        NetworkError = 200,

        [Description("Download operation failed")]
        DownloadFailed = 201,

        [Description("API request failed")]
        ApiRequestFailed = 202,

        // File Operations
        [Description("File not found")]
        FileNotFound = 300,

        [Description("Invalid file format")]
        InvalidFile = 301,

        [Description("File is corrupted")]
        CorruptedFile = 302,

        [Description("File access denied")]
        FileAccessDenied = 303,

        // Validation
        [Description("Invalid input provided")]
        InvalidInput = 400,

        [Description("Missing required dependencies")]
        MissingDependencies = 401,

        [Description("Invalid mod format")]
        InvalidModFormat = 402,

        // General
        [Description("Unexpected error occurred")]
        UnexpectedError = 500,

        [Description("Operation was cancelled")]
        OperationCancelled = 501
    }

    public static class ErrorCodeExtensions
    {
        /// <summary>
        /// Returns the DescriptionAttribute text for this error code, or the enum name if no description is present.
        /// </summary>
        public static string GetDescription(this ErrorCode code)
        {
            var member = typeof(ErrorCode).GetMember(code.ToString());
            if (member.Length == 0)
                return code.ToString();

            var attr = member[0].GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? code.ToString();
        }

        /// <summary>
        /// Indicates whether this error is likely fatal (non-retryable).
        /// Tweak the set as your needs evolve.
        /// </summary>
        public static bool IsFatal(this ErrorCode code)
        {
            return code switch
            {
                ErrorCode.InvalidCredentials => true,
                ErrorCode.InvalidFile => true,
                ErrorCode.CorruptedFile => true,
                ErrorCode.InvalidModFormat => true,
                ErrorCode.UnexpectedError => true,
                _ => false
            };
        }

        /// <summary>
        /// Indicates whether this error is likely transient and can be retried.
        /// </summary>
        public static bool IsTransient(this ErrorCode code)
        {
            return code switch
            {
                ErrorCode.NetworkError => true,
                ErrorCode.DownloadFailed => true,
                ErrorCode.ApiRequestFailed => true,
                ErrorCode.OperationCancelled => false, // usually user choice
                _ => false
            };
        }
    }
}