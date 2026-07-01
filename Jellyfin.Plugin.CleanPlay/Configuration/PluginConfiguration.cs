using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CleanPlay.Configuration;

/// <summary>
/// Plugin configuration for CleanPlay.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private const string DefaultWords = "fuck*\nmotherfuck*\nshit*\nbullshit*\nbitch*\nasshole*\nbastard*\ndamn*\ngoddamn*\ndick\ndickhead*\ncock\ncunt*\npussy\nwhore*\nslut*\npiss*\nprick\ndouche*\njackass*\ntwat*\nwank*\nbollocks";

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ProfanityWords = DefaultWords;
        EnableSubtitleScan = true;
        PadBeforeMs = 250;
        PadAfterMs = 250;
        MergeGapMs = 750;
        PreferredSubtitleLanguage = "eng";
        ProfanitySegmentType = "Commercial";
        NuditySegmentType = "Commercial";
        ViolenceSegmentType = "Commercial";
        OtherSegmentType = "Commercial";
    }

    /// <summary>
    /// Gets or sets the profanity word list, one word per line. A trailing * matches any suffix
    /// (e.g. "damn*" matches "damnit").
    /// </summary>
    public string ProfanityWords { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scheduled subtitle profanity scan is enabled.
    /// </summary>
    public bool EnableSubtitleScan { get; set; }

    /// <summary>
    /// Gets or sets padding (ms) added before each profanity subtitle cue.
    /// </summary>
    public int PadBeforeMs { get; set; }

    /// <summary>
    /// Gets or sets padding (ms) added after each profanity subtitle cue.
    /// </summary>
    public int PadAfterMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum gap (ms) between adjacent filter segments that will be merged into one.
    /// </summary>
    public int MergeGapMs { get; set; }

    /// <summary>
    /// Gets or sets the preferred subtitle language (ISO 639-2, e.g. "eng") used for profanity scanning.
    /// </summary>
    public string PreferredSubtitleLanguage { get; set; }

    /// <summary>
    /// Gets or sets the media segment type emitted for Profanity filters.
    /// </summary>
    public string ProfanitySegmentType { get; set; }

    /// <summary>
    /// Gets or sets the media segment type emitted for Nudity filters.
    /// </summary>
    public string NuditySegmentType { get; set; }

    /// <summary>
    /// Gets or sets the media segment type emitted for Violence filters.
    /// </summary>
    public string ViolenceSegmentType { get; set; }

    /// <summary>
    /// Gets or sets the media segment type emitted for Other filters.
    /// </summary>
    public string OtherSegmentType { get; set; }
}
