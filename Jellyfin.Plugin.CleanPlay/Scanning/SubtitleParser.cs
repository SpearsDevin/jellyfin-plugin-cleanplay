using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CleanPlay.Scanning;

/// <summary>
/// A single subtitle cue.
/// </summary>
/// <param name="Start">Start time in seconds.</param>
/// <param name="End">End time in seconds.</param>
/// <param name="Text">Cue text with markup stripped.</param>
public record SubtitleCue(double Start, double End, string Text);

/// <summary>
/// Minimal parser for SRT, WebVTT and ASS/SSA subtitle files.
/// </summary>
public static class SubtitleParser
{
    private static readonly Regex _srtTime = new Regex(
        @"(\d{1,2}):(\d{2}):(\d{2})[,\.](\d{1,3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,\.](\d{1,3})",
        RegexOptions.Compiled);

    private static readonly Regex _vttShortTime = new Regex(
        @"(\d{1,2}):(\d{2})\.(\d{1,3})\s*-->\s*(\d{1,2}):(\d{2})\.(\d{1,3})",
        RegexOptions.Compiled);

    private static readonly Regex _markup = new Regex(@"<[^>]+>|\{[^}]*\}", RegexOptions.Compiled);

    /// <summary>
    /// Parses a subtitle file into cues. Returns an empty list for unsupported formats.
    /// </summary>
    /// <param name="path">Path to the subtitle file.</param>
    /// <returns>The parsed cues.</returns>
    public static IReadOnlyList<SubtitleCue> ParseFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var lines = File.ReadAllLines(path);

        return ext switch
        {
            ".srt" or ".vtt" => ParseSrtVtt(lines),
            ".ass" or ".ssa" => ParseAss(lines),
            _ => Array.Empty<SubtitleCue>()
        };
    }

    private static IReadOnlyList<SubtitleCue> ParseSrtVtt(string[] lines)
    {
        var cues = new List<SubtitleCue>();
        double start = -1, end = -1;
        var text = new List<string>();

        void Flush()
        {
            if (start >= 0 && text.Count > 0)
            {
                cues.Add(new SubtitleCue(start, end, CleanText(string.Join(" ", text))));
            }

            start = end = -1;
            text.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimStart('﻿');
            var m = _srtTime.Match(line);
            if (m.Success)
            {
                Flush();
                start = ToSeconds(m, 1);
                end = ToSeconds(m, 5);
                continue;
            }

            var mv = _vttShortTime.Match(line);
            if (mv.Success)
            {
                Flush();
                start = ToSecondsShort(mv, 1);
                end = ToSecondsShort(mv, 4);
                continue;
            }

            if (line.Length == 0)
            {
                Flush();
            }
            else if (start >= 0 && !IsCueIndexOrHeader(line))
            {
                text.Add(line);
            }
        }

        Flush();
        return cues;
    }

    private static IReadOnlyList<SubtitleCue> ParseAss(string[] lines)
    {
        var cues = new List<SubtitleCue>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Substring("Dialogue:".Length).Split(',', 10);
            if (parts.Length < 10)
            {
                continue;
            }

            if (TryParseAssTime(parts[1].Trim(), out var start) && TryParseAssTime(parts[2].Trim(), out var end))
            {
                var text = parts[9].Replace("\\N", " ", StringComparison.OrdinalIgnoreCase);
                cues.Add(new SubtitleCue(start, end, CleanText(text)));
            }
        }

        return cues;
    }

    private static bool TryParseAssTime(string value, out double seconds)
    {
        seconds = 0;
        var parts = value.Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
        {
            seconds = (h * 3600) + (m * 60) + s;
            return true;
        }

        return false;
    }

    private static double ToSeconds(Match m, int offset)
    {
        var h = int.Parse(m.Groups[offset].Value, CultureInfo.InvariantCulture);
        var min = int.Parse(m.Groups[offset + 1].Value, CultureInfo.InvariantCulture);
        var s = int.Parse(m.Groups[offset + 2].Value, CultureInfo.InvariantCulture);
        var ms = int.Parse(m.Groups[offset + 3].Value.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return (h * 3600) + (min * 60) + s + (ms / 1000.0);
    }

    private static double ToSecondsShort(Match m, int offset)
    {
        var min = int.Parse(m.Groups[offset].Value, CultureInfo.InvariantCulture);
        var s = int.Parse(m.Groups[offset + 1].Value, CultureInfo.InvariantCulture);
        var ms = int.Parse(m.Groups[offset + 2].Value.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return (min * 60) + s + (ms / 1000.0);
    }

    private static bool IsCueIndexOrHeader(string line)
    {
        if (line.Equals("WEBVTT", StringComparison.OrdinalIgnoreCase) || line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static string CleanText(string text)
    {
        return _markup.Replace(text, " ").Trim();
    }
}
