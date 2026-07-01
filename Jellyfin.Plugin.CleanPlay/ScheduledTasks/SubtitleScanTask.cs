using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CleanPlay.Data;
using Jellyfin.Plugin.CleanPlay.Scanning;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CleanPlay.ScheduledTasks;

/// <summary>
/// Scheduled task that scans library subtitles for profanity and refreshes media segments.
/// </summary>
public class SubtitleScanTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSegmentManager _mediaSegmentManager;
    private readonly FilterRepository _repository;
    private readonly SubtitleProfanityScanner _scanner;
    private readonly ILogger<SubtitleScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleScanTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="mediaSegmentManager">Instance of the <see cref="IMediaSegmentManager"/> interface.</param>
    /// <param name="repository">The filter repository.</param>
    /// <param name="scanner">The subtitle profanity scanner.</param>
    /// <param name="logger">The logger.</param>
    public SubtitleScanTask(
        ILibraryManager libraryManager,
        IMediaSegmentManager mediaSegmentManager,
        FilterRepository repository,
        SubtitleProfanityScanner scanner,
        ILogger<SubtitleScanTask> logger)
    {
        _libraryManager = libraryManager;
        _mediaSegmentManager = mediaSegmentManager;
        _repository = repository;
        _scanner = scanner;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "CleanPlay subtitle profanity scan";

    /// <inheritdoc />
    public string Description => "Scans subtitles for profanity and creates skip segments.";

    /// <inheritdoc />
    public string Category => "CleanPlay";

    /// <inheritdoc />
    public string Key => "CleanPlaySubtitleScan";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnableSubtitleScan != true)
        {
            progress.Report(100);
            return;
        }

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            IsVirtualItem = false,
            Recursive = true,
            SourceTypes = new[] { SourceType.Library }
        };

        var items = _libraryManager.GetItemList(query);
        var total = items.Count;
        var done = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.IsFileProtocol && File.Exists(item.Path))
            {
                try
                {
                    await ScanItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CleanPlay: failed to scan {Name}", item.Name);
                }
            }

            done++;
            progress.Report(100.0 * done / Math.Max(1, total));
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }

    private async Task ScanItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var generated = _scanner.Scan(item);
        var filters = _repository.Get(item.Id);
        var existingAuto = filters.Segments.Where(s => s.Source == "Subtitle").ToList();

        // Nothing found and nothing stored: skip.
        if (generated.Count == 0 && existingAuto.Count == 0)
        {
            return;
        }

        filters.Segments.RemoveAll(s => s.Source == "Subtitle");
        filters.Segments.AddRange(generated);
        _repository.Save(filters);

        var libraryOptions = _libraryManager.GetLibraryOptions(item);
        await _mediaSegmentManager.RunSegmentPluginProviders(item, libraryOptions, true, cancellationToken).ConfigureAwait(false);
    }
}
