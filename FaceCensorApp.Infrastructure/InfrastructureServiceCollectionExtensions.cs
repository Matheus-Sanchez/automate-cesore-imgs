using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Infrastructure.Imaging;
using FaceCensorApp.Infrastructure.Logging;
using FaceCensorApp.Infrastructure.Output;
using FaceCensorApp.Infrastructure.Scanning;
using FaceCensorApp.Infrastructure.Settings;
using FaceCensorApp.Infrastructure.Video;
using Microsoft.Extensions.DependencyInjection;

namespace FaceCensorApp.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddFaceCensorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMediaScanner, RecursiveMediaScanner>();
        services.AddSingleton<IImageCensorService, GdiImageCensorService>();
        services.AddSingleton<IOutputOrganizer, OutputOrganizer>();
        services.AddSingleton<ILogService, FileLogService>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<IVideoCensorService, UnsupportedVideoCensorService>();
        return services;
    }
}