using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.CleanPlay.Data;

namespace Jellyfin.Plugin.CleanPlay.Scanning;

/// <summary>
/// Parses MPlayer-style EDL text: one entry per line, "start end action",
/// where start/end are seconds and action is 0 (cut/skip), 1 (mute) or 3 (commercial).
/// Lines starting with # are ignored.
/// </summary>
public static class EdlParser
{
    /// <summary>
    /// Parses EDL text into filter segments.
    /// </summary>
    /// <param name="text">Raw EDL content.</param>
    /// <param name="category">Category to assign to imported segments.</param>
    /// <returns>The parsed segments.</returns>
    public static IReadOnlyList<FilterSegment> Parse(string text, string category)
    {
        var segments = new List<FilterSegment>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
                && end > start)
            {
                var action = parts.Length > 2 ? parts[2] : "0";
                segments.Add(new FilterSegment
                {
                    StartSeconds = start,
                    EndSeconds = end,
                    Category = category,
                    Source = "EDL",
                    Note = "EDL action " + action
                });
            }
        }

        return segments;
    }
}
