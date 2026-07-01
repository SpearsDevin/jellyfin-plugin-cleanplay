using Jellyfin.Plugin.CleanPlay.Data;
using Jellyfin.Plugin.CleanPlay.Providers;
using Jellyfin.Plugin.CleanPlay.Scanning;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CleanPlay;

/// <summary>
/// Registers plugin services with the Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<FilterRepository>();
        serviceCollection.AddSingleton<SubtitleProfanityScanner>();
        serviceCollection.AddSingleton<IMediaSegmentProvider, CleanPlayMediaSegmentProvider>();
    }
}
