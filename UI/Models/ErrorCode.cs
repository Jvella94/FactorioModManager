namespace FactorioModManager.Models
{
    public enum ErrorCode
    {
        // Authentication
        MissingCredentials = 100,
        InvalidCredentials = 101,

        // Network
        NetworkError = 200,
        DownloadFailed = 201,
        ApiRequestFailed = 202,

        // File Operations
        FileNotFound = 300,
        InvalidFile = 301,
        CorruptedFile = 302,
        FileAccessDenied = 303,

        // Validation
        InvalidInput = 400,
        MissingDependencies = 401,
        InvalidModFormat = 402,

        // General
        UnexpectedError = 500,
        OperationCancelled = 501
    }
}