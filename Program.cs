using CopyFilesToStorageAccountFolder;
using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Services;
using CopyFilesToStorageAccountFolder.UI;
using Serilog;
using Spectre.Console;

var noUiFlag = args.Contains("--no-ui");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Azure Blob Storage upload service");

    var builder = Host.CreateApplicationBuilder(args);

    // Load UI settings to determine if UI is enabled
    var uiSettings = builder.Configuration
        .GetSection("UISettings")
        .Get<UISettings>() ?? new UISettings();

    var uiEnabled = ShouldEnableUI(uiSettings, noUiFlag);

    // Configure Serilog - suppress console when UI is enabled
    builder.Services.AddSerilog((services, lc) =>
    {
        lc.ReadFrom.Services(services)
          .Enrich.FromLogContext();

        if (uiEnabled)
        {
            // Only write to Seq when UI is active (no console output)
            lc.WriteTo.Seq("http://localhost:5341");
        }
        else
        {
            // Use full configuration including console when UI is disabled
            lc.ReadFrom.Configuration(builder.Configuration);
        }
    });

    builder.Services.Configure<UploadSettings>(
        builder.Configuration.GetSection("UploadSettings"));
    builder.Services.Configure<UISettings>(
        builder.Configuration.GetSection("UISettings"));

    builder.Services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
    builder.Services.AddSingleton<IBlobUploadService, BlobUploadService>();
    builder.Services.AddSingleton<IProgressTrackingService, SqliteProgressTrackingService>();
    builder.Services.AddSingleton<IUploadStateService, UploadStateService>();

    // Register UI services
    if (uiEnabled)
    {
        builder.Services.AddSingleton<IConsoleUI, RetroConsoleUI>();
        builder.Services.AddHostedService<ConsoleUIHostedService>();
    }
    else
    {
        builder.Services.AddSingleton<IConsoleUI, NullConsoleUI>();
    }

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static bool ShouldEnableUI(UISettings settings, bool noUiFlag)
{
    // Explicitly disabled via flag or settings
    if (noUiFlag || !settings.Enabled)
        return false;

    // CI environment detection
    if (Environment.GetEnvironmentVariable("CI") == "true" ||
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
        Environment.GetEnvironmentVariable("TF_BUILD") == "true")
        return false;

    // Check for ANSI support and interactive console
    if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        return false;

    // Check if Spectre.Console supports ANSI
    if (!AnsiConsole.Profile.Capabilities.Ansi)
        return false;

    return true;
}
