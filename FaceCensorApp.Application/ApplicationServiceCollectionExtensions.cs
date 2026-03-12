using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FaceCensorApp.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddFaceCensorApplication(this IServiceCollection services)
    {
        services.AddSingleton<IJobExecutor, BatchJobExecutor>();
        return services;
    }
}