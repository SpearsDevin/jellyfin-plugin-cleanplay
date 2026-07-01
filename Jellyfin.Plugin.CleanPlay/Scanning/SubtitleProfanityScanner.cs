using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CleanPlay.Data;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CleanPlay.Scanning;

/// <summary>
/// Scans external text subtitles of an item for profanity and produces filter segments.
/// </summary>
public class SubtitleProfanityScanner
{
    private static readonly string[] _supportedExtensions = { ".srt", ".vtt", ".ass", ".ssa" };

    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<SubtitleProfanityScanner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleProfanityScanner"/> class.
    /// </summary>
    /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> interface.</param>
    /// <param name="logger">The logger.</param>
    public SubtitleProfanityScanner(IMediaSourceManager mediaSourceManager, ILogger<SubtitleProfanityScanner> logger)
    {
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    /// <summary>
    /// Scans the item's subtitles and returns profanity filter segments.
    /// Returns an empty list when no usable subtitle file is found.
    /// </summary>
    /// <param name="item">The item to scan.</param>
    /// <returns>Generated segments (source = "Subtitle").</returns>
    public IReadOnlyList<FilterSegment> Scan(BaseItem item)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Array.Empty<FilterSegment>();
        }

        var regex = BuildRegex(config.ProfanityWords);
        if (regex is null)
        {
            return Array.Empty<FilterSegment>();
        }

        var subtitlePath = FindSubtitlePath(item, config.PreferredSubtitleLanguage);
        if (subtitlePath is null)
        {
            return Array.Empty<FilterSegment>();
        }

        IReadOnlyList<SubtitleCue> cues;
        try
        {
            cues = SubtitleParser.ParseFile(subtitlePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "CleanPlay: failed to read subtitle file {Path}", subtitlePath);
            return Array.Empty<FilterSegment>();
        }

        var padBefore = config.PadBeforeMs / 1000.0;
        var padAfter = config.PadAfterMs / 1000.0;
        var raw = new List<FilterSegment>();

        foreach (var cue in cues)
        {
            var matches = regex.Matches(cue.Text);
            if (matches.Count == 0)
            {
                continue;
            }

            var words = string.Join(", ", matches.Select(m => m.Value.ToLowerInvariant()).Distinct());
            raw.Add(new FilterSegment
            {
                StartSeconds = Math.Max(0, cue.Start - padBefore),
                EndSeconds = cue.End + padAfter,
                Category = "Profanity",
                Source = "Subtitle",
                Note = words
            });
        }

        return Merge(raw, config.MergeGapMs / 1000.0);
    }

    /// <summary>
    /// Builds a word-boundary regex from a newline-separated word list. A trailing * matches suffixes.
    /// </summary>
    /// <param name="wordList">The configured word list.</param>
    /// <returns>The compiled regex, or null when the list is empty.</returns>
    public static Regex? BuildRegex(string wordList)
    {
        var patterns = new List<string>();
        foreach (var raw in (wordList ?? string.Empty).Split('\n'))
        {
            var word = raw.Trim();
            if (word.Length == 0 || word.StartsWith('#'))
            {
                continue;
            }

            var suffixWildcard = word.EndsWith('*');
            if (suffixWildcard)
            {
                word = word.TrimEnd('*');
            }

            if (word.Length == 0)
            {
                continue;
            }

            patterns.Add(Regex.Escape(word) + (suffixWildcard ? @"\w*" : string.Empty));
        }

        if (patterns.Count == 0)
        {
            return null;
        }

        return new Regex(@"\b(?:" + string.Join("|", patterns) + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private string? FindSubtitlePath(BaseItem item, string preferredLanguage)
    {
        IReadOnlyList<MediaStream> streams;
        try
        {
            streams = _mediaSourceManager.GetMediaStreams(item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CleanPlay: could not get media streams for {Name}", item.Name);
            return null;
        }

        var candidates = streams
            .Where(s => s.Type == MediaStreamType.Subtitle
                && s.IsExternal
                && !string.IsNullOrEmpty(s.Path)
                && _supportedExtensions.Contains(Path.GetExtension(s.Path).ToLowerInvariant())
                && File.Exists(s.Path))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var preferred = candidates.FirstOrDefault(s =>
            string.Equals(s.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));

        return (preferred ?? candidates[0]).Path;
    }

    private static IReadOnlyList<FilterSegment> Merge(List<FilterSegment> segments, double maxGapSeconds)
    {
        if (segments.Count == 0)
        {
            return segments;
        }

        segments.Sort((a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
        var merged = new List<FilterSegment> { segments[0] };

        for (var i = 1; i < segments.Count; i++)
        {
            var current = merged[^1];
            var next = segments[i];

            if (next.StartSeconds - current.EndSeconds <= maxGapSeconds)
            {
                current.EndSeconds = Math.Max(current.EndSeconds, next.EndSeconds);
                if (!string.IsNullOrEmpty(next.Note) && current.Note?.Contains(next.Note, StringComparison.OrdinalIgnoreCase) != true)
                {
                    current.Note = string.IsNullOrEmpty(current.Note) ? next.Note : current.Note + ", " + next.Note;
                }
            }
            else
            {
                merged.Add(next);
            }
        }

        return merged;
    }
}
