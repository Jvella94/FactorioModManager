using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FactorioModManager.Services.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Platform
{
    public class AvaloniaPlatformService(IUIService uiService, Window? overrideWindow = null) : IPlatformService
    {
        private readonly Window? _overrideWindow = overrideWindow;
        private readonly IUIService _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));

        private IStorageProvider? GetProvider()
        {
            // prefer override window provider when available
            var window = _overrideWindow ?? _uiService.GetMainWindow();
            return window?.StorageProvider;
        }

        public async Task<string?> PickFileAsync(string title, string[] patterns)
        {
            try
            {
                var provider = GetProvider();
                if (provider == null)
                {
                    return null;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = [.. patterns.Select(p => new FilePickerFileType("Custom") { Patterns = [p] })]
                };

                var result = await provider.OpenFilePickerAsync(options);
                if (result != null && result.Count > 0)
                    return result[0].Path.LocalPath;
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> PickFolderAsync(string title)
        {
            try
            {
                var provider = GetProvider();
                if (provider == null)
                    return null;

                var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
                var result = await provider.OpenFolderPickerAsync(options);
                if (result != null && result.Count > 0)
                    return result[0].Path.LocalPath;
                return null;
            }
            catch
            {
                return null;
            }
        }

        public Task<bool> LaunchProcessAsync(string path)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                };
                System.Diagnostics.Process.Start(psi);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}