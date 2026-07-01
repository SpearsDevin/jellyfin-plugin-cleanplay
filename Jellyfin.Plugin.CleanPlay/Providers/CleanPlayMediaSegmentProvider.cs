using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.CleanPlay.Data;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Plugin.CleanPlay.Providers;

/// <summary>
/// Supplies CleanPlay filter segments to Jellyfin's media segment system.
/// </summary>
public class CleanPlayMediaSegmentProvider : IMediaSegmentProvider
{
    private readonly FilterRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanPlayMediaSegmentProvider"/> class.
    /// </summary>
    /// <param name="repository">The filter repository.</param>
    public CleanPlayMediaSegmentProvider(FilterRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public string Name => "CleanPlay";

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => new ValueTask<bool>(item is IHasMediaSources);

    /// <inheritdoc />
    public Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(MediaSegmentGenerationRequest request, CancellationToken cancellationToken)
    {
        var filters = _repository.Get(request.ItemId);
        if (filters.Segments.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<MediaSegmentDto>>(Array.Empty<MediaSegmentDto>());
        }

        var segments = new List<MediaSegmentDto>(filters.Segments.Count);
        foreach (var filter in filters.Segments)
        {
            if (filter.EndSeconds <= filter.StartSeconds)
            {
                continue;
            }

            segments.Add(new MediaSegmentDto
            {
                Id = Guid.NewGuid(),
                ItemId = request.ItemId,
                Type = GetSegmentType(filter.Category),
                StartTicks = (long)(filter.StartSeconds * TimeSpan.TicksPerSecond),
                EndTicks = (long)(filter.EndSeconds * TimeSpan.TicksPerSecond)
            });
        }

        return Task.FromResult<IReadOnlyList<MediaSegmentDto>>(segments);
    }

    private static MediaSegmentType GetSegmentType(string category)
    {
        var config = Plugin.Instance?.Configuration;
        var typeName = category switch
        {
            "Profanity" => config?.ProfanitySegmentType,
            "Nudity" => config?.NuditySegmentType,
            "Violence" => config?.ViolenceSegmentType,
            _ => config?.OtherSegmentType
        };

        return Enum.TryParse<MediaSegmentType>(typeName, true, out var type) ? type : MediaSegmentType.Commercial;
    }
}
