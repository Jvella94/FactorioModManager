using Avalonia.Media;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FactorioModManager.Models
{
    /// <summary>
    /// Represents a log entry with pre-formatted text segments for display
    /// </summary>
    public partial class FormattedLogEntry(LogEntry log)
    {
        [GeneratedRegex(@"(\b(?:enabled|disabled|installed|removed|updated|downloading|downloaded)\b)", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordsRegex();

        [GeneratedRegex(@"^(enabled|disabled|installed|removed|updated|downloading|downloaded)$", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordMatchRegex();

        [GeneratedRegex(@"(?:for|from)\s+([A-Za-z0-9_-]+)")]
        private static partial Regex ModNameRegex();

        // ✅ Properties for binding
        public string Timestamp { get; } = $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}]";

        public string LevelText { get; } = $"[{log.Level}]";

        public IBrush LevelBrush { get; } = log.Level switch
        {
            LogLevel.Error => Brushes.Red,
            LogLevel.Warning => Brushes.Orange,
            LogLevel.Debug => Brushes.Cyan,
            _ => Brushes.White
        };

        public List<MessageSegment> MessageSegments { get; } = ParseMessage(log.Message);

        private static List<MessageSegment> ParseMessage(string message)
        {
            var segments = new List<MessageSegment>();
            var parts = StateWordsRegex().Split(message);

            foreach (var part in parts)
            {
                if (StateWordMatchRegex().IsMatch(part))
                {
                    // Highlight state words (enabled, disabled, etc.)
                    segments.Add(new MessageSegment(
                        part,
                        Brushes.LightGreen,
                        FontWeight.Bold
                    ));
                }
                else
                {
                    // Check for mod names
                    var matches = ModNameRegex().Matches(part);
                    if (matches.Count > 0)
                    {
                        int lastIndex = 0;
                        foreach (Match match in matches)
                        {
                            // Add text before mod name
                            if (match.Index > lastIndex)
                            {
                                segments.Add(new MessageSegment(
                                    part[lastIndex..match.Index],
                                    Brushes.White
                                ));
                            }

                            // Add prefix (for/from)
                            var prefix = part[match.Index..match.Groups[1].Index];
                            segments.Add(new MessageSegment(prefix, Brushes.White));

                            // Add highlighted mod name
                            segments.Add(new MessageSegment(
                                match.Groups[1].Value,
                                Brushes.Cyan,
                                FontWeight.Bold
                            ));

                            lastIndex = match.Index + match.Length;
                        }

                        // Add remaining text
                        if (lastIndex < part.Length)
                        {
                            segments.Add(new MessageSegment(
                                part[lastIndex..],
                                Brushes.White
                            ));
                        }
                    }
                    else
                    {
                        // Plain text
                        segments.Add(new MessageSegment(part, Brushes.White));
                    }
                }
            }

            return segments;
        }
    }

    /// <summary>
    /// Represents a segment of text with formatting
    /// </summary>
    public record MessageSegment(
        string Text,
        IBrush Foreground,
        FontWeight Weight = default
    )
    {
        public FontWeight Weight { get; init; } = Weight == default ? FontWeight.Normal : Weight;
    }
}