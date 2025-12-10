using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;

namespace FactorioModManager.Models
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public record MessageSegment(string Text, IBrush? Foreground, FontWeight Weight = FontWeight.Normal);

    public partial class LogEntry()
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;

        // Computed UI properties
        public string TimestampFormatted => Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        public string LevelText => Level.ToString();

        public IBrush LevelBrush => Level switch
        {
            LogLevel.Error => Brushes.Red!,
            LogLevel.Warning => Brushes.Orange!,
            LogLevel.Debug => Brushes.Cyan!,
            _ => Brushes.White!
        };

        public List<MessageSegment> MessageSegments => ParseMessage(Message);

        private static readonly Regex _stateWordsRegex = StateWordsRegexGenerator();
        private static readonly Regex _stateWordMatchRegex = StateWordMatchRegexGenerator();
        private static readonly Regex _modNameRegex = ModNameRegexGenerator();

        [GeneratedRegex("(?:enabled|disabled|installed|removed|updated|downloading|downloaded)", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordsRegexGenerator();

        [GeneratedRegex("(?:enabled|disabled|installed|removed|updated|downloading|downloaded)", RegexOptions.IgnoreCase)]
        private static partial Regex StateWordMatchRegexGenerator();

        [GeneratedRegex(@"(?:(?:for|from)\s+)?([A-Za-z0-9-]+)", RegexOptions.IgnoreCase)]
        private static partial Regex ModNameRegexGenerator();

        private static List<MessageSegment> ParseMessage(string message)
        {
            var segments = new List<MessageSegment>();
            var parts = _stateWordsRegex.Split(message);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (_stateWordMatchRegex.IsMatch(part))
                {
                    AddStateWordSegment(segments, part);
                }
                else
                {
                    AddModNameSegmentsOrPlainText(segments, part);
                }
            }
            return segments;
        }

        private static void AddStateWordSegment(List<MessageSegment> segments, string word)
        {
            segments.Add(new MessageSegment(word, Brushes.LightGreen, FontWeight.Bold));
        }

        private static void AddModNameSegmentsOrPlainText(List<MessageSegment> segments, string part)
        {
            var matches = _modNameRegex.Matches(part);
            if (matches.Count == 0)
            {
                // Plain text
                segments.Add(new MessageSegment(part, Brushes.White));
                return;
            }

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                // Text before mod name
                if (match.Index > lastIndex)
                {
                    segments.Add(new MessageSegment(part[lastIndex..match.Index], Brushes.White));
                }

                // Prefix "for"/"from"
                var prefix = part[match.Index..match.Groups[1].Index];
                if (!string.IsNullOrEmpty(prefix))
                {
                    segments.Add(new MessageSegment(prefix, Brushes.White));
                }

                // Highlighted mod name
                segments.Add(new MessageSegment(match.Groups[1].Value, Brushes.Cyan, FontWeight.Bold));

                lastIndex = match.Index + match.Length;
            }

            // Remaining text
            if (lastIndex < part.Length)
            {
                segments.Add(new MessageSegment(part[lastIndex..], Brushes.White));
            }
        }
    }
}