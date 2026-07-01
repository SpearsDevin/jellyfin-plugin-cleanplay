using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.CleanPlay.Data;

/// <summary>
/// Stores filter segments as JSON files ({itemId}.json) under the server data folder.
/// </summary>
public class FilterRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dataDir;
    private readonly object _lock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterRepository"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    public FilterRepository(IApplicationPaths applicationPaths)
    {
        _dataDir = Path.Combine(applicationPaths.DataPath, "cleanplay");
        Directory.CreateDirectory(_dataDir);
    }

    /// <summary>
    /// Gets the stored filters for an item (empty set when none exist).
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The item's filters.</returns>
    public ItemFilters Get(Guid itemId)
    {
        lock (_lock)
        {
            var path = GetPath(itemId);
            if (!File.Exists(path))
            {
                return new ItemFilters { ItemId = itemId };
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ItemFilters>(json, _jsonOptions) ?? new ItemFilters { ItemId = itemId };
            }
            catch (JsonException)
            {
                return new ItemFilters { ItemId = itemId };
            }
        }
    }

    /// <summary>
    /// Saves the filters for an item. Deletes the file when there are no segments.
    /// </summary>
    /// <param name="filters">The filters to save.</param>
    public void Save(ItemFilters filters)
    {
        lock (_lock)
        {
            var path = GetPath(filters.ItemId);
            if (filters.Segments.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            filters.Segments.Sort((a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
            File.WriteAllText(path, JsonSerializer.Serialize(filters, _jsonOptions));
        }
    }

    /// <summary>
    /// Gets ids of all items that have stored filters.
    /// </summary>
    /// <returns>List of item ids.</returns>
    public IReadOnlyList<Guid> GetAllItemIds()
    {
        lock (_lock)
        {
            var result = new List<Guid>();
            foreach (var file in Directory.EnumerateFiles(_dataDir, "*.json"))
            {
                if (Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var id))
                {
                    result.Add(id);
                }
            }

            return result;
        }
    }

    private string GetPath(Guid itemId)
    {
        return Path.Combine(_dataDir, itemId.ToString("N", CultureInfo.InvariantCulture) + ".json");
    }
}
