using FaceCensorApp.AI;
using FaceCensorApp.Application;
using FaceCensorApp.Infrastructure;
using FaceCensorApp.WinForms.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FormsApplication = System.Windows.Forms.Application;

namespace FaceCensorApp.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Services
            .AddFaceCensorApplication()
            .AddFaceCensorInfrastructure()
            .AddFaceCensorAi();
        builder.Services.AddSingleton<MainForm>();

        using var host = builder.Build();
        FormsApplication.Run(host.Services.GetRequiredService<MainForm>());
    }
}