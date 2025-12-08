using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Views.Base;
using System;
using System.Linq;

namespace FactorioModManager.Views
{
    public partial class InstallModDialog : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        public InstallModDialog()
        {
            InitializeComponent();
        }

        private async void InstallFromFile_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select Factorio Mod (.zip)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Factorio Mod")
                    {
                        Patterns = new[] { "*.zip" },
                        MimeTypes = new[] { "application/zip" }
                    }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);

            if (files.Count > 0)
            {
                var file = files[0];
                CloseWithResult((true, file.Path.LocalPath, false));
            }
        }

        private async void InstallFromUrl_Click(object? sender, RoutedEventArgs e)
        {
            var urlDialog = new UrlInputDialog();
            var url = await urlDialog.ShowDialog(this);

            if (!string.IsNullOrEmpty(url))
            {
                CloseWithResult((true, url, true));
            }
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Cancel();
        }
    }
}
