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

                _logService.LogDebug($"Downloading mod '{modName}' to '{filePath}' from '{authenticatedUrl}' (using separate downloads/incomplete folder)");

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
                                    var info = System.Text.Json.JsonSerializer.Deserialize<Models.ModInfo>(json, Constants.JsonOptions.CaseInsensitive);
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
            // Use a separate downloads/incomplete folder outside of the mods directory
            var systemTemp = Path.GetTempPath();
            var downloadsDir = Path.Combine(systemTemp, "FactorioModManager", "downloads");
            Directory.CreateDirectory(downloadsDir);

            var destFileName = Path.GetFileName(destinationPath) ?? ("downloaded_mod.zip");
            var uniqueId = Guid.NewGuid().ToString("N");
            var tempPath = Path.Combine(downloadsDir, destFileName + "." + uniqueId + ".zip.part");

            try
            {
                _logService.LogDebug($"Starting file download: url='{url}' -> temp='{tempPath}'");

                using var response = await _httpClient.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logService.LogWarning($"Download failed HTTP {response.StatusCode}: {url}");
                    return Result<bool>.Fail($"HTTP {response.StatusCode}", ErrorCode.DownloadFailed);
                }

                var totalBytes = response.Content.Headers.ContentLength;
                var totalRead = await DownloadTempFile(progress, tempPath, response, totalBytes, cancellationToken);

                // Move temp file into final mods directory atomically (with retries)
                try
                {
                    const int moveMaxAttempts = 8;
                    var moved = false;
                    for (int moveAttempt = 1; moveAttempt <= moveMaxAttempts; moveAttempt++)
                    {
                        try
                        {
                            // Ensure destination directory exists
                            var destDir = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            // If destination exists, try delete (best effort)
                            try
                            {
                                if (File.Exists(destinationPath))
                                    File.Delete(destinationPath);
                            }
                            catch (Exception exDel)
                            {
                                _logService.LogDebug($"Attempt {moveAttempt}: unable to delete existing destination '{destinationPath}': {exDel.Message}");
                            }

                            _logService.LogDebug($"Attempt {moveAttempt}: moving temp file '{tempPath}' to final '{destinationPath}'");
                            File.Move(tempPath, destinationPath);
                            _logService.Log($"Download completed: {Path.GetFileName(destinationPath)} ({totalBytes ?? totalRead:N0} bytes)");
                            moved = true;
                            break;
                        }
                        catch (IOException ioEx)
                        {
                            _logService.LogWarning($"Attempt {moveAttempt} failed moving '{tempPath}' to '{destinationPath}': {ioEx.Message}");
                            if (moveAttempt == moveMaxAttempts)
                            {
                                _logService.LogError($"IO error moving temp file after {moveMaxAttempts} attempts: {ioEx.Message}", ioEx);
                                throw;
                            }

                            // Backoff before retrying
                            try { Thread.Sleep(300 * moveAttempt); } catch { }
                            continue;
                        }
                        catch (Exception exMove)
                        {
                            _logService.LogError($"Failed to move temp file '{tempPath}' to '{destinationPath}': {exMove.Message}", exMove);
                            throw;
                        }
                    }

                    if (!moved)
                    {
                        // Ensure temp file removed and return failure
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                        return Result<bool>.Fail("Failed to finalize download: destination file locked", ErrorCode.FileAccessDenied);
                    }
                }
                catch (Exception exMove)
                {
                    // cleanup temp and propagate failure
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                    return Result<bool>.Fail($"Failed to finalize download: {exMove.Message}", ErrorCode.FileAccessDenied);
                }

                return Result<bool>.Ok(true);
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning($"Download cancelled: {Path.GetFileName(destinationPath)}");

                // Clean up temp file
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Download failed for '{destinationPath}': {ex.Message}", ex);

                // Clean up temp file
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                return Result<bool>.Fail(ex.Message, ErrorCode.NetworkError);
            }
        }

        private static async Task<long> DownloadTempFile(IProgress<(long, long?)>? progress, string tempPath, HttpResponseMessage response, long? totalBytes, CancellationToken cancellationToken)
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.Create,
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

            // ensure data flushed
            await fileStream.FlushAsync(cancellationToken);
            return totalRead;
        }
    }
}