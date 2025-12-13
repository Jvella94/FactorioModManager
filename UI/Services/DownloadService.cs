using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class DownloadService(
        ISettingsService settingsService,
        ILogService logService,
        HttpClient httpClient) : IDownloadService
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ILogService _logService = logService;
        private readonly HttpClient _httpClient = httpClient;

        public async Task<Result<bool>> DownloadModAsync(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var username = _settingsService.GetUsername();
                var token = _settingsService.GetToken();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                {
                    _logService.LogWarning("Download requires username and token from Factorio");
                    return Result<bool>.Fail("Missing Factorio credentials", ErrorCode.MissingCredentials);
                }

                var authenticatedUrl = Constants.Urls.GetModDownloadUrl(downloadUrl, username, token);
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var fileName = $"{modName}_{version}.zip";
                var filePath = Path.Combine(modsDirectory, fileName);

                var downloadResult = await DownloadFileAsync(authenticatedUrl, filePath, progress, cancellationToken);

                if (!downloadResult.Success)
                    return downloadResult;

                var verifyResult = await VerifyModFileAsync(filePath, modTitle);
                if (!verifyResult.Success)
                    return verifyResult;

                _logService.Log($"Successfully downloaded {modTitle} version {version}");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading {modTitle}: {ex.Message}", ex);
                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }

        public async Task<Result<bool>> VerifyModFileAsync(string filePath, string displayName)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logService.LogWarning($"Downloaded file does not exist: {filePath}");
                    return Result<bool>.Fail("File not found", ErrorCode.FileNotFound);
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    _logService.LogWarning($"Downloaded file is empty: {filePath}");
                    File.Delete(filePath);
                    return Result<bool>.Fail("File is empty", ErrorCode.InvalidFile);
                }

                // Check for HTML error responses (invalid credentials, etc.)
                var buffer = new byte[Math.Min(100, (int)fileInfo.Length)];
                using (var fs = File.OpenRead(filePath))
                {
                    await fs.ReadExactlyAsync(buffer);
                }

                var contentPreview = System.Text.Encoding.UTF8.GetString(buffer);
                if (contentPreview.Contains("<!DOCTYPE") || contentPreview.Contains("<html"))
                {
                    _logService.LogWarning($"Downloaded file is HTML (likely auth error): {filePath}");
                    File.Delete(filePath);
                    return Result<bool>.Fail("Invalid credentials", ErrorCode.InvalidCredentials);
                }

                // Verify ZIP structure
                using var archive = ZipFile.OpenRead(filePath);
                if (archive.Entries.Count == 0)
                {
                    _logService.LogWarning($"Downloaded ZIP file is empty: {filePath}");
                    File.Delete(filePath);
                    return Result<bool>.Fail("ZIP file is empty", ErrorCode.CorruptedFile);
                }

                var infoEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));

                if (infoEntry == null)
                {
                    _logService.LogWarning($"Downloaded ZIP is missing info.json: {filePath}");
                    File.Delete(filePath);
                    return Result<bool>.Fail("Missing info.json", ErrorCode.InvalidModFormat);
                }

                _logService.Log($"ZIP file verified: {archive.Entries.Count} entries found");
                return Result<bool>.Ok(true);
            }
            catch (InvalidDataException ex)
            {
                _logService.LogError($"Downloaded file is not a valid ZIP: {ex.Message}", ex);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return Result<bool>.Fail("Corrupted ZIP file", ErrorCode.CorruptedFile);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error verifying downloaded file: {ex.Message}", ex);
                return Result<bool>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public async Task<Result<bool>> InstallFromLocalFileAsync(string sourceFilePath)
        {
            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var destinationPath = Path.Combine(modsDirectory, fileName);

                var verifyResult = await VerifyModFileAsync(sourceFilePath, fileName);
                if (!verifyResult.Success)
                    return verifyResult;

                File.Copy(sourceFilePath, destinationPath, overwrite: true);
                _logService.Log($"Installed mod from file: {fileName}");

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing mod from file: {ex.Message}", ex);
                return Result<bool>.Fail(ex.Message, ErrorCode.FileAccessDenied);
            }
        }

        public void DeleteOldVersions(string modName, string currentVersionFile)
        {
            try
            {
                var keepOldFiles = _settingsService.GetKeepOldModFiles();
                if (keepOldFiles)
                    return;

                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var allZipFiles = Directory.GetFiles(modsDirectory, "*.zip");

                foreach (var file in allZipFiles)
                {
                    try
                    {
                        // Skip the current version file
                        if (file.Equals(currentVersionFile, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileName = Path.GetFileNameWithoutExtension(file);
                        // Expect file format: <modName>_<version>
                        var lastUnderscore = fileName.LastIndexOf('_');
                        if (lastUnderscore <= 0)
                            continue;

                        var namePart = fileName[..lastUnderscore];
                        if (!namePart.Equals(modName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Extra safety: verify the ZIP's internal info.json declares the same mod name
                        bool safeToDelete = false;
                        try
                        {
                            using var archive = ZipFile.OpenRead(file);
                            var infoEntry = archive.Entries.FirstOrDefault(e =>
                                e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));
                            if (infoEntry != null)
                            {
                                using var stream = infoEntry.Open();
                                using var sr = new StreamReader(stream);
                                var json = sr.ReadToEnd();
                                try
                                {
                                    var info = System.Text.Json.JsonSerializer.Deserialize<Models.ModInfo>(json, Constants.JsonHelper.CaseInsensitive);
                                    if (info != null && info.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
                                        safeToDelete = true;
                                }
                                catch
                                {
                                    safeToDelete = false;
                                }
                            }
                        }
                        catch (Exception exZip)
                        {
                            _logService.LogWarning($"Skipping deletion of {file}: could not read ZIP ({exZip.Message})");
                            safeToDelete = false;
                        }

                        if (!safeToDelete)
                        {
                            _logService.LogDebug($"Skipping deletion of {Path.GetFileName(file)} because it does not match mod '{modName}' or could not be verified");
                            continue;
                        }

                        File.Delete(file);
                        _logService.Log($"Deleted old version: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Error deleting old version file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error deleting old versions: {ex.Message}");
            }
        }

        public async Task<Result<bool>> DownloadFileAsync(
      string url,
      string destinationPath,
      IProgress<(long, long?)>? progress = null,
      CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return Result<bool>.Fail($"HTTP {response.StatusCode}", ErrorCode.DownloadFailed);

                var totalBytes = response.Content.Headers.ContentLength;
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(destinationPath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;
                    progress?.Report((totalRead, totalBytes));
                }

                return Result<bool>.Ok(true);
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                throw;
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message, ErrorCode.NetworkError);
            }
        }
    }
}