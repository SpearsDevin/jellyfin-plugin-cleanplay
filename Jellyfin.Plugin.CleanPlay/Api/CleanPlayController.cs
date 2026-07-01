using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CleanPlay.Data;
using Jellyfin.Plugin.CleanPlay.Scanning;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CleanPlay.Api;

/// <summary>
/// Request body for EDL import.
/// </summary>
public class EdlImportRequest
{
    /// <summary>
    /// Gets or sets the raw EDL text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category assigned to imported segments.
    /// </summary>
    public string Category { get; set; } = "Other";
}

/// <summary>
/// Search result entry for the filter editor.
/// </summary>
public class SearchResultDto
{
    /// <summary>Gets or sets the item id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the item name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the production year.</summary>
    public int? Year { get; set; }

    /// <summary>Gets or sets the item type.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets the series name for episodes.</summary>
    public string? Series { get; set; }

    /// <summary>Gets or sets the season number for episodes.</summary>
    public int? Season { get; set; }

    /// <summary>Gets or sets the episode number for episodes.</summary>
    public int? Episode { get; set; }

    /// <summary>Gets or sets the runtime in seconds.</summary>
    public double? RuntimeSeconds { get; set; }

    /// <summary>Gets or sets the number of stored filter segments.</summary>
    public int FilterCount { get; set; }
}

/// <summary>
/// Admin API for managing CleanPlay filters.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("CleanPlay")]
[Produces(MediaTypeNames.Application.Json)]
public class CleanPlayController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSegmentManager _mediaSegmentManager;
    private readonly FilterRepository _repository;
    private readonly SubtitleProfanityScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanPlayController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="mediaSegmentManager">Instance of the <see cref="IMediaSegmentManager"/> interface.</param>
    /// <param name="repository">The filter repository.</param>
    /// <param name="scanner">The subtitle profanity scanner.</param>
    public CleanPlayController(
        ILibraryManager libraryManager,
        IMediaSegmentManager mediaSegmentManager,
        FilterRepository repository,
        SubtitleProfanityScanner scanner)
    {
        _libraryManager = libraryManager;
        _mediaSegmentManager = mediaSegmentManager;
        _repository = repository;
        _scanner = scanner;
    }

    /// <summary>
    /// Searches movies and episodes by name.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <returns>Matching items.</returns>
    [HttpGet("Search")]
    public ActionResult<IEnumerable<SearchResultDto>> Search([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Ok(Array.Empty<SearchResultDto>());
        }

        var query = new InternalItemsQuery
        {
            SearchTerm = term,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            IsVirtualItem = false,
            Recursive = true,
            Limit = 25
        };

        var results = _libraryManager.GetItemList(query).Select(item =>
        {
            var episode = item as Episode;
            return new SearchResultDto
            {
                Id = item.Id,
                Name = item.Name,
                Year = item.ProductionYear,
                Type = item.GetBaseItemKind().ToString(),
                Series = episode?.SeriesName,
                Season = episode?.ParentIndexNumber,
                Episode = episode?.IndexNumber,
                RuntimeSeconds = item.RunTimeTicks.HasValue ? item.RunTimeTicks.Value / (double)TimeSpan.TicksPerSecond : null,
                FilterCount = _repository.Get(item.Id).Segments.Count
            };
        });

        return Ok(results);
    }

    /// <summary>
    /// Gets stored filters for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The item's filters.</returns>
    [HttpGet("Items/{itemId}/Filters")]
    public ActionResult<ItemFilters> GetFilters([FromRoute] Guid itemId)
    {
        return Ok(_repository.Get(itemId));
    }

    /// <summary>
    /// Adds a filter segment to an item and refreshes its media segments.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="segment">The segment to add.</param>
    /// <returns>The updated filters.</returns>
    [HttpPost("Items/{itemId}/Filters")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ItemFilters>> AddFilter([FromRoute] Guid itemId, [FromBody] FilterSegment segment)
    {
        if (segment.EndSeconds <= segment.StartSeconds || segment.StartSeconds < 0)
        {
            return BadRequest("End must be greater than start.");
        }

        segment.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(segment.Source))
        {
            segment.Source = "Manual";
        }

        var filters = _repository.Get(itemId);
        filters.Segments.Add(segment);
        _repository.Save(filters);

        await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return Ok(_repository.Get(itemId));
    }

    /// <summary>
    /// Deletes a single filter segment.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="segmentId">The filter segment id.</param>
    /// <returns>The updated filters.</returns>
    [HttpDelete("Items/{itemId}/Filters/{segmentId}")]
    public async Task<ActionResult<ItemFilters>> DeleteFilter([FromRoute] Guid itemId, [FromRoute] Guid segmentId)
    {
        var filters = _repository.Get(itemId);
        filters.Segments.RemoveAll(s => s.Id == segmentId);
        _repository.Save(filters);

        await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return Ok(_repository.Get(itemId));
    }

    /// <summary>
    /// Deletes all filter segments for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Items/{itemId}/Filters")]
    public async Task<ActionResult> ClearFilters([FromRoute] Guid itemId)
    {
        _repository.Save(new ItemFilters { ItemId = itemId });
        await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Imports EDL text as filter segments for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="request">The EDL import request.</param>
    /// <returns>The updated filters.</returns>
    [HttpPost("Items/{itemId}/ImportEdl")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ItemFilters>> ImportEdl([FromRoute] Guid itemId, [FromBody] EdlImportRequest request)
    {
        var imported = EdlParser.Parse(request.Text ?? string.Empty, string.IsNullOrWhiteSpace(request.Category) ? "Other" : request.Category);
        if (imported.Count == 0)
        {
            return BadRequest("No valid EDL entries found.");
        }

        var filters = _repository.Get(itemId);
        filters.Segments.AddRange(imported);
        _repository.Save(filters);

        await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return Ok(_repository.Get(itemId));
    }

    /// <summary>
    /// Scans the item's subtitles for profanity, replacing previous subtitle-derived segments.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The updated filters.</returns>
    [HttpPost("Items/{itemId}/ScanSubtitles")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemFilters>> ScanSubtitles([FromRoute] Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        var generated = _scanner.Scan(item);
        var filters = _repository.Get(itemId);
        filters.Segments.RemoveAll(s => s.Source == "Subtitle");
        filters.Segments.AddRange(generated);
        _repository.Save(filters);

        await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return Ok(_repository.Get(itemId));
    }

    /// <summary>
    /// Re-runs segment providers for an item (applies stored filters to playback).
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>No content.</returns>
    [HttpPost("Items/{itemId}/Apply")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Apply([FromRoute] Guid itemId)
    {
        var refreshed = await RefreshSegmentsAsync(itemId).ConfigureAwait(false);
        return refreshed ? NoContent() : NotFound();
    }

    private async Task<bool> RefreshSegmentsAsync(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return false;
        }

        var libraryOptions = _libraryManager.GetLibraryOptions(item);
        await _mediaSegmentManager.RunSegmentPluginProviders(item, libraryOptions, true, CancellationToken.None).ConfigureAwait(false);
        return true;
    }
}
