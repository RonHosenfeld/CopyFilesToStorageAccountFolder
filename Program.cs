using CopyFilesToStorageAccountFolder;
using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Azure Blob Storage upload service");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.Configure<UploadSettings>(
        builder.Configuration.GetSection("UploadSettings"));

    builder.Services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
    builder.Services.AddSingleton<IBlobUploadService, BlobUploadService>();
    builder.Services.AddSingleton<IProgressTrackingService, SqliteProgressTrackingService>();

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
