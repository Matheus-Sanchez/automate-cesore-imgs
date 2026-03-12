using FaceCensorApp.AI.Detection;
using FaceCensorApp.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FaceCensorApp.AI;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddFaceCensorAi(this IServiceCollection services)
    {
        services.AddSingleton<IFaceDetector, YuNetOnnxFaceDetector>();
        return services;
    }
}