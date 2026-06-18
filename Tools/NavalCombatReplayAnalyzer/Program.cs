using NavalCombatReplayAnalyzer.Services;
using YYZ;

var builder = WebApplication.CreateBuilder(args);

ServiceLocator.Register<ILoggerService>(new ReplayAnalyzerLogger(), logOverride: false);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<ReplayAnalyzerService>();
builder.Services.AddSingleton<AcmiExporter>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

sealed class ReplayAnalyzerLogger : ILoggerService
{
    public void Log(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void LogError(string message)
    {
        Console.Error.WriteLine("[Error] " + message);
    }
}
