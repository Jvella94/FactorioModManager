namespace FactorioModManager.Models
{
    public enum ErrorCode
    {
        // Authentication
        MissingCredentials,
        InvalidCredentials,

        // Network
        NetworkError,
        DownloadFailed,
        ApiRequestFailed,

        // File Operations
        FileNotFound,
        InvalidFile,
        CorruptedFile,
        FileAccessDenied,

        // Validation
        InvalidInput,
        MissingDependencies,
        InvalidModFormat,

        // General
        UnexpectedError,
        OperationCancelled
    }
}
