using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Services;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder;

public class Worker(
    IFileDiscoveryService fileDiscoveryService,
    IBlobUploadService blobUploadService,
    IProgressTrackingService progressTrackingService,
    IOptions<UploadSettings> settings,
    ILogger<Worker> logger,
    IHostApplicationLifetime appLifetime) : BackgroundService
{
    private readonly UploadSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!ValidateConfiguration())
            {
                logger.LogError("Configuration validation failed. Exiting.");
                appLifetime.StopApplication();
                return;
            }

            var progress = await progressTrackingService.LoadProgressAsync(stoppingToken);

            var stats = new UploadStats();

            logger.LogInformation("Starting file discovery...");

            await foreach (var file in fileDiscoveryService.DiscoverFilesAsync(stoppingToken))
            {
                stats.TotalDiscovered++;

                if (progressTrackingService.IsFileCompleted(progress, file))
                {
                    logger.LogDebug("Skipping already completed file: {FilePath}", file.FullPath);
                    stats.Skipped++;
                    continue;
                }

                logger.LogInformation(
                    "Processing file {Current}: {FileName} ({Size} bytes)",
                    stats.TotalDiscovered, file.FileName, file.FileSize);

                var result = await blobUploadService.UploadFileAsync(file, stoppingToken);

                if (result.Success)
                {
                    progressTrackingService.MarkCompleted(progress, result);
                    stats.Succeeded++;
                }
                else
                {
                    progressTrackingService.MarkFailed(progress, result);
                    stats.Failed++;
                }

                await progressTrackingService.SaveProgressAsync(progress, stoppingToken);

                if (_settings.Throttling.DelayBetweenFilesMs > 0)
                {
                    await Task.Delay(_settings.Throttling.DelayBetweenFilesMs, stoppingToken);
                }
            }

            progress.CompletedAt = DateTime.UtcNow;
            await progressTrackingService.SaveProgressAsync(progress, stoppingToken);

            LogSummary(stats);

            logger.LogInformation("Upload process completed. Shutting down.");
            appLifetime.StopApplication();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Upload operation was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during upload process");
            throw;
        }
    }

    private bool ValidateConfiguration()
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(_settings.AzureBlobStorage.ContainerUrl) ||
            _settings.AzureBlobStorage.ContainerUrl.Contains("<account>"))
        {
            logger.LogError("Azure Blob Storage ContainerUrl is not configured");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_settings.AzureBlobStorage.SasToken) ||
            _settings.AzureBlobStorage.SasToken == "?sv=...")
        {
            logger.LogError("Azure Blob Storage SasToken is not configured");
            isValid = false;
        }

        if (_settings.SourceFolders.Count == 0)
        {
            logger.LogError("No source folders configured");
            isValid = false;
        }

        foreach (var folder in _settings.SourceFolders)
        {
            if (!Directory.Exists(folder))
            {
                logger.LogWarning("Source folder does not exist: {Folder}", folder);
            }
        }

        return isValid;
    }

    private void LogSummary(UploadStats stats)
    {
        logger.LogInformation(
            "Upload Summary: {TotalDiscovered} files discovered, {Succeeded} uploaded, {Skipped} skipped (already done), {Failed} failed",
            stats.TotalDiscovered, stats.Succeeded, stats.Skipped, stats.Failed);
    }

    private class UploadStats
    {
        public int TotalDiscovered { get; set; }
        public int Succeeded { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }
}
