using Microsoft.Extensions.Options;

namespace GrokBotRoute;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "GrokBotRoute";
        });
        builder.Services.Configure<GrokBotRouteOptions>(
            builder.Configuration.GetSection(GrokBotRouteOptions.SectionName));
        builder.Services.AddSingleton<GrokBotEnsure>();
        builder.Services.AddSingleton<GrokBotRouteTicker>();
        builder.Services.AddSingleton<IGrokBotRuntime, WindowsGrokBotRuntime>();
        builder.Services.AddHostedService<GrokBotRouteWorker>();

        builder.Build().Run();
    }
}
