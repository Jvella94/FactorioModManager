using FactorioModManager.Models;
using FactorioModManager.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private async Task<Result<bool>> DownloadModFromPortalAsync(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            ModViewModel? modForProgress = null)
        {
            try
            {
                var username = _settingsService.GetUsername();
                var token = _settingsService.GetToken();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                {
                    _logService.LogWarning("Download requires username and token from Factorio");
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Cannot download {modTitle}: Missing Factorio credentials. Please check Settings.";
                    });
                    return Result<bool>.Fail("Missing Factorio credentials", ErrorCode.MissingCredentials);
                }

                var authenticatedUrl = Constants.Urls.GetModDownloadUrl(downloadUrl, username, token);
                var modsDirectory = ModPathHelper.GetModsDirectory();
                var fileName = $"{modName}_{version}.zip";
                var filePath = Path.Combine(modsDirectory, fileName);

                var downloadResult = await DownloadFileWithProgressAsync(
                    authenticatedUrl, filePath, modTitle, token, modForProgress);

                if (!downloadResult.Success)
                    return downloadResult;

                var verifyResult = await VerifyDownloadedModFileAsync(filePath, modTitle);
                if (!verifyResult.Success)
                    return verifyResult;

                _logService.Log($"Successfully downloaded {modTitle} version {version}");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading {modTitle}: {ex.Message}", ex);
                await _uiService.InvokeAsync(() =>
                {
                    StatusText = $"Error downloading {modTitle}: {ex.Message}";
                });
                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }

        private async Task<Result<bool>> DownloadFileWithProgressAsync(
            string url,
            string destinationPath,
            string displayName,
            string tokenForLogging,
            ModViewModel? modForProgress = null)
        {
            try
            {
                using var httpClient = new HttpClient();
                _logService.Log($"Downloading from {url.Replace(tokenForLogging, "***")}");

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    _logService.LogWarning($"Download failed: {response.StatusCode}");
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Download failed for {displayName}: {response.StatusCode}";
                        if (modForProgress != null)
                        {
                            modForProgress.IsDownloading = false;
                            modForProgress.DownloadStatusText = $"Failed: {response.StatusCode}";
                        }
                    });
                    return Result<bool>.Fail($"HTTP {response.StatusCode}", ErrorCode.DownloadFailed);
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, Constants.UI.BufferSize, true);

                var buffer = new byte[Constants.UI.BufferSize];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (modForProgress != null)
                    {
                        if (totalBytes > 0)
                        {
                            var progressPercent = (double)totalRead / totalBytes * 100;
                            _uiService.Post(() =>
                            {
                                modForProgress.HasDownloadProgress = true;
                                modForProgress.DownloadProgress = progressPercent;
                                modForProgress.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                            });
                        }
                        else
                        {
                            var mbDownloaded = totalRead / 1024.0 / 1024.0;
                            _uiService.Post(() =>
                            {
                                modForProgress.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                            });
                        }
                    }
                }

                _logService.Log($"Downloaded {totalRead} bytes to {Path.GetFileName(destinationPath)}");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error during file download: {ex.Message}", ex);

                if (modForProgress != null)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        modForProgress.IsDownloading = false;
                        modForProgress.DownloadStatusText = $"Error: {ex.Message}";
                    });
                }

                return Result<bool>.Fail(ex.Message, ErrorCode.NetworkError);
            }
        }

        private async Task<Result<bool>> VerifyDownloadedModFileAsync(string filePath, string displayName)
        {
            await Task.Run(() =>
            {
                _uiService.Post(() =>
                {
                    StatusText = $"Verifying download for {displayName}...";
                });
            });

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
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Downloaded file is empty for {displayName}";
                    });
                    File.Delete(filePath);
                    return Result<bool>.Fail("File is empty", ErrorCode.InvalidFile);
                }

                var buffer = new byte[Math.Min(100, (int)fileInfo.Length)];
                using (var fs = File.OpenRead(filePath))
                {
                    fs.ReadExactly(buffer);
                }

                var contentPreview = System.Text.Encoding.UTF8.GetString(buffer);
                if (contentPreview.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                    contentPreview.Contains("<html", StringComparison.OrdinalIgnoreCase))
                {
                    _logService.LogWarning($"Downloaded file is HTML (invalid credentials?)");
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Download failed for {displayName} - Invalid credentials. Please check Settings.";
                    });
                    File.Delete(filePath);
                    return Result<bool>.Fail("Invalid credentials", ErrorCode.InvalidCredentials);
                }

                using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);

                if (archive.Entries.Count == 0)
                {
                    _logService.LogWarning($"Downloaded ZIP file is empty: {filePath}");
                    File.Delete(filePath);
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Downloaded file is corrupted for {displayName}";
                    });
                    return Result<bool>.Fail("ZIP file is empty", ErrorCode.CorruptedFile);
                }

                var infoEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));

                if (infoEntry == null)
                {
                    _logService.LogWarning($"Downloaded ZIP is missing info.json: {filePath}");
                    File.Delete(filePath);
                    await _uiService.InvokeAsync(() =>
                    {
                        StatusText = $"Downloaded file is invalid for {displayName}";
                    });
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
                await _uiService.InvokeAsync(() =>
                {
                    StatusText = $"Downloaded file is corrupted for {displayName}";
                });
                return Result<bool>.Fail("Corrupted ZIP file", ErrorCode.CorruptedFile);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error verifying downloaded file: {ex.Message}", ex);
                return Result<bool>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        private async Task<Result<bool>> InstallModFromLocalFileAsync(string sourceFilePath)
        {
            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var modsDirectory = ModPathHelper.GetModsDirectory();
                var destinationPath = Path.Combine(modsDirectory, fileName);

                await _uiService.InvokeAsync(() =>
                {
                    StatusText = $"Installing {fileName}...";
                });

                var verifyResult = await VerifyDownloadedModFileAsync(sourceFilePath, fileName);
                if (!verifyResult.Success)
                    return verifyResult;

                File.Copy(sourceFilePath, destinationPath, overwrite: true);

                _logService.Log($"Installed mod from file: {fileName}");

                await _uiService.InvokeAsync(() =>
                {
                    StatusText = $"Successfully installed {fileName}";
                });

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing mod from file: {ex.Message}", ex);
                await _uiService.InvokeAsync(() =>
                {
                    StatusText = $"Error installing mod: {ex.Message}";
                });
                return Result<bool>.Fail(ex.Message, ErrorCode.FileAccessDenied);
            }
        }

        private void DeleteOldModVersions(string modName, string currentVersionFile)
        {
            try
            {
                var keepOldFiles = _settingsService.GetKeepOldModFiles();
                if (keepOldFiles)
                    return;

                _uiService.Post(() =>
                {
                    StatusText = $"Removing old versions of {modName}...";
                });

                var modsDirectory = ModPathHelper.GetModsDirectory();
                var oldFiles = Directory.GetFiles(modsDirectory, $"{modName}_*.zip")
                    .Where(f => !f.Equals(currentVersionFile, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var oldFile in oldFiles)
                {
                    File.Delete(oldFile);
                    _logService.Log($"Deleted old version: {Path.GetFileName(oldFile)}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error deleting old versions: {ex.Message}");
            }
        }
    }
}