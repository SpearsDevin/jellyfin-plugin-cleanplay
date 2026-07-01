using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CleanPlay.Data;

/// <summary>
/// A single filtered (skipped) time range within an item.
/// </summary>
public class FilterSegment
{
    /// <summary>
    /// Gets or sets the unique id of this filter segment.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the start position in seconds.
    /// </summary>
    public double StartSeconds { get; set; }

    /// <summary>
    /// Gets or sets the end position in seconds.
    /// </summary>
    public double EndSeconds { get; set; }

    /// <summary>
    /// Gets or sets the category: Profanity, Nudity, Violence or Other.
    /// </summary>
    public string Category { get; set; } = "Other";

    /// <summary>
    /// Gets or sets the source: Manual, Subtitle or EDL.
    /// </summary>
    public string Source { get; set; } = "Manual";

    /// <summary>
    /// Gets or sets an optional note (e.g. the matched word).
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// All filter segments stored for a single library item.
/// </summary>
public class ItemFilters
{
    /// <summary>
    /// Gets or sets the Jellyfin item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the filter segments.
    /// </summary>
    public List<FilterSegment> Segments { get; set; } = new List<FilterSegment>();
}
