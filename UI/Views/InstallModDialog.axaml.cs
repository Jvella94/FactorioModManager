using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Views.Base;
using System;
using Avalonia.Media;
using Avalonia;

namespace FactorioModManager.Views
{
    public partial class InstallModDialog : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        private IBrush? _originalBorderBrush;
        private Thickness _originalBorderThickness;

        public InstallModDialog()
        {
            InitializeComponent();
        }

        protected override void OnDialogOpened(object? sender, EventArgs e)
        {
            base.OnDialogOpened(sender, e);
            try
            {
                InputTextBox.Focus();
            }
            catch { }

            // capture original border values so we can restore them
            _originalBorderBrush = InputTextBox.BorderBrush;
            _originalBorderThickness = InputTextBox.BorderThickness;
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = GetTopLevel(this);
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
                InputTextBox.Text = file.Path.LocalPath;
            }
        }

        private static bool IsValidHttpUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (!(uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                return false;
            if (string.IsNullOrEmpty(uri.Host)) return false;

            // If this is the Factorio mods portal, enforce the https://mods.factorio.com/mod/{modName} prefix
            if (uri.Host.Equals("mods.factorio.com", StringComparison.OrdinalIgnoreCase))
            {
                // require HTTPS for portal URLs
                if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    return false;

                var path = uri.AbsolutePath ?? string.Empty;
                // must start with /mod/
                const string requiredPrefix = "/mod/";
                if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;

                // ensure there is a mod name after the prefix
                var after = path.Length > requiredPrefix.Length ? path.Substring(requiredPrefix.Length) : string.Empty;
                if (string.IsNullOrWhiteSpace(after))
                    return false;

                // first segment after /mod/ must be non-empty
                var segments = after.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0) return false;

                return true;
            }

            return false;
        }

        private async void Install_Click(object? sender, RoutedEventArgs e)
        {
            var input = InputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                SetErrorVisible("Please enter a file path or a URL.");
                return;
            }

            // Prefer local file if it exists
            try
            {
                if (System.IO.File.Exists(input))
                {
                    SetErrorVisible(null);
                    CloseWithResult((true, input, false));
                    return;
                }
            }
            catch
            {
                // ignore file check errors
            }

            // Use robust URL validation
            if (IsValidHttpUrl(input))
            {
                SetErrorVisible(null);
                CloseWithResult((true, input, true));
                return;
            }

            // Invalid input: show inline error
            SetErrorVisible("Invalid path or URL. Enter a valid .zip path or https:// URL.");
        }

        private void InputTextBox_TextChanged(object? sender, RoutedEventArgs e)
        {
            // Clear error when user changes input
            SetErrorVisible(null);
        }

        private void SetErrorVisible(string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                ErrorTextBlock.Text = string.Empty;
                ErrorTextBlock.IsVisible = false;
                // restore original border
                try
                {
                    InputTextBox.BorderBrush = _originalBorderBrush;
                    InputTextBox.BorderThickness = _originalBorderThickness;
                }
                catch { }
            }
            else
            {
                ErrorTextBlock.Text = message;
                ErrorTextBlock.IsVisible = true;
                // set red border to indicate error
                try
                {
                    InputTextBox.BorderBrush = Brushes.Red;
                    Thickness thickness = new(1);
                    InputTextBox.BorderThickness = thickness;
                }
                catch { }
            }
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Cancel();
        }
    }
}