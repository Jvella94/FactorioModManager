using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace FactorioModManager.Views
{
    public partial class LogWindow : Window
    {
        [GeneratedRegex(@"(\b(?:enabled|disabled|installed|removed|updated|downloading|downloaded)\b)", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordsRegex();

        [GeneratedRegex(@"^(enabled|disabled|installed|removed|updated|downloading|downloaded)$", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordMatchRegex();

        [GeneratedRegex(@"(?:for|from)\s+([A-Za-z0-9_-]+)")]
        private static partial Regex ModNameRegex();

        public LogWindow()
        {
            InitializeComponent();
            LoadLogs();
        }

        private void LoadLogs()
        {
            var logs = LogService.Instance.GetLogs().ToList();
            var inlineCollection = new InlineCollection();

            foreach (var log in logs)
            {
                var timestamp = $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] ";
                var level = $"[{log.Level}] ";

                // Timestamp in gray
                inlineCollection.Add(new Run(timestamp) { Foreground = Brushes.Gray });

                // Level with color coding
                var levelBrush = log.Level switch
                {
                    LogLevel.Error => Brushes.Red,
                    LogLevel.Warning => Brushes.Orange,
                    LogLevel.Debug => Brushes.Cyan,
                    _ => Brushes.White
                };
                inlineCollection.Add(new Run(level) { Foreground = levelBrush, FontWeight = FontWeight.Bold });

                // Highlight mod names and states
                var message = log.Message;
                var parts = StateWordsRegex().Split(message);

                foreach (var part in parts)
                {
                    if (StateWordMatchRegex().IsMatch(part))
                    {
                        inlineCollection.Add(new Run(part) { Foreground = Brushes.LightGreen, FontWeight = FontWeight.Bold });
                    }
                    else
                    {
                        var matches = ModNameRegex().Matches(part);
                        if (matches.Count > 0)
                        {
                            int lastIndex = 0;
                            foreach (Match match in matches)
                            {
                                if (match.Index > lastIndex)
                                {
                                    inlineCollection.Add(new Run(part[lastIndex..match.Index]) { Foreground = Brushes.White });
                                }

                                var prefix = part[match.Index..match.Groups[1].Index];
                                inlineCollection.Add(new Run(prefix) { Foreground = Brushes.White });
                                inlineCollection.Add(new Run(match.Groups[1].Value) { Foreground = Brushes.Cyan, FontWeight = FontWeight.Bold });

                                lastIndex = match.Index + match.Length;
                            }

                            if (lastIndex < part.Length)
                            {
                                inlineCollection.Add(new Run(part[lastIndex..]) { Foreground = Brushes.White });
                            }
                        }
                        else
                        {
                            inlineCollection.Add(new Run(part) { Foreground = Brushes.White });
                        }
                    }
                }

                inlineCollection.Add(new Run(Environment.NewLine));
            }

            LogTextBlock.Inlines = inlineCollection;

            if (this.FindControl<TextBlock>("LogCountText") is TextBlock countText)
            {
                countText.Text = $"{logs.Count} log entries";
            }
        }

        private void Refresh_Click(object? sender, RoutedEventArgs e)
        {
            LoadLogs();
        }

        private void OpenLogFile_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var logFilePath = LogService.Instance.GetLogFilePath();
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError($"Error opening log file: {ex.Message}");
            }
        }

        private async void ClearLogs_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ConfirmationDialog(
                "Clear Logs",
                "Are you sure you want to clear all logs?\n\nThis action cannot be undone."
            );

            var result = await dialog.ShowDialog(this);

            if (result)
            {
                // Clear the logs
                LogService.Instance.ClearLogs(); // You'll need to add this method to LogService
                LoadLogs();
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void ArchiveLogs_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.ConfirmationDialog(
                "Archive Logs",
                "Archive current logs to a timestamped file and start fresh?"
            );

            var result = await dialog.ShowDialog(this);

            if (result)
            {
                LogService.Instance.ArchiveLogs();
                LoadLogs();

                var successDialog = new Dialogs.MessageBoxDialog(
                    "Success",
                    "Logs have been archived successfully."
                );
                await successDialog.ShowDialog(this);
            }
        }

    }
}
