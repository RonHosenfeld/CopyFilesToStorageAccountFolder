using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Services;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder;

public class Worker(
    IFileDiscoveryService fileDiscoveryService,
    IBlobUploadService blobUploadService,
    IProgressTrackingService progressTrackingService,
    IUploadStateService uploadStateService,
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

            // Initialize UI state with configuration
            var destination = GetDestinationDisplayName();
            uploadStateService.Initialize(
                _settings.SourceFolders,
                destination,
                _settings.Throttling.DelayBetweenFilesMs);

            var progress = await progressTrackingService.LoadProgressAsync(stoppingToken);

            // Phase 1: Pre-enumeration
            logger.LogInformation("Starting file enumeration...");
            uploadStateService.SetEnumerating(true, "Scanning folders...");

            var enumerationResult = await fileDiscoveryService.PreEnumerateAllFilesAsync(
                folderPath => uploadStateService.SetEnumerating(true, $"Scanning: {folderPath}"),
                stoppingToken);

            var folderProgressList = enumerationResult.Folders
                .Select(f => new Services.FolderProgress
                {
                    FolderPath = f.FolderPath,
                    DisplayName = GetFolderDisplayName(f.FolderPath),
                    TotalFiles = f.Files.Count
                })
                .ToList();

            uploadStateService.SetEnumerationCounts(
                enumerationResult.TotalFolders,
                enumerationResult.TotalFiles,
                folderProgressList);
            uploadStateService.SetEnumerating(false);

            logger.LogInformation(
                "Enumeration complete: {TotalFolders} folders, {TotalFiles} files",
                enumerationResult.TotalFolders,
                enumerationResult.TotalFiles);

            // Phase 2: Processing
            var stats = new UploadStats { TotalDiscovered = enumerationResult.TotalFiles };
            var fileNumber = 0;

            foreach (var folder in enumerationResult.Folders)
            {
                uploadStateService.SetCurrentFolder(folder.FolderPath);

                foreach (var file in folder.Files)
                {
                    fileNumber++;

                    uploadStateService.SetCurrentFile(file.FullPath, file.FileSize);

                    // Compute checksum lazily (only when needed for processing)
                    var fileWithChecksum = await fileDiscoveryService.WithChecksumAsync(file, stoppingToken);

                    if (progressTrackingService.IsFileCompleted(progress, fileWithChecksum))
                    {
                        logger.LogDebug("Skipping already completed file: {FilePath}", file.FullPath);
                        stats.Skipped++;
                        uploadStateService.RecordSkipped(folder.FolderPath);
                        continue;
                    }

                    logger.LogInformation(
                        "Processing file {Current}/{Total}: {FileName} ({Size} bytes)",
                        fileNumber, enumerationResult.TotalFiles, file.FileName, file.FileSize);

                    var result = await blobUploadService.UploadFileAsync(fileWithChecksum, stoppingToken);

                    if (result.Success)
                    {
                        progressTrackingService.MarkCompleted(progress, result);
                        stats.Succeeded++;
                        uploadStateService.RecordSuccess(folder.FolderPath);
                    }
                    else
                    {
                        progressTrackingService.MarkFailed(progress, result);
                        stats.Failed++;
                        uploadStateService.RecordFailed(folder.FolderPath, result.ErrorMessage);
                    }

                    await progressTrackingService.SaveProgressAsync(progress, stoppingToken);

                    if (_settings.Throttling.DelayBetweenFilesMs > 0)
                    {
                        await Task.Delay(_settings.Throttling.DelayBetweenFilesMs, stoppingToken);
                    }
                }
            }

            progress.CompletedAt = DateTime.UtcNow;
            await progressTrackingService.SaveProgressAsync(progress, stoppingToken);

            uploadStateService.SetCompleted();
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

    private static string GetFolderDisplayName(string folderPath)
    {
        var parts = folderPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length > 0 ? parts[^1] : folderPath;
    }

    private bool ValidateConfiguration()
    {
        var isValid = true;
        var blobSettings = _settings.AzureBlobStorage;

        var hasConnectionString = !string.IsNullOrWhiteSpace(blobSettings.ConnectionString);
        var hasSasConfig = !string.IsNullOrWhiteSpace(blobSettings.ContainerUrl) &&
                           !blobSettings.ContainerUrl.Contains("<account>") &&
                           !string.IsNullOrWhiteSpace(blobSettings.SasToken) &&
                           blobSettings.SasToken != "?sv=...";

        if (hasConnectionString)
        {
            if (string.IsNullOrWhiteSpace(blobSettings.ContainerName))
            {
                logger.LogError("ContainerName must be specified when using ConnectionString");
                isValid = false;
            }
            else
            {
                logger.LogInformation("Using connection string authentication for container: {Container}",
                    blobSettings.ContainerName);
            }
        }
        else if (hasSasConfig)
        {
            logger.LogInformation("Using SAS token authentication for: {ContainerUrl}",
                blobSettings.ContainerUrl);
        }
        else
        {
            logger.LogError("Azure Blob Storage authentication not configured. " +
                           "Provide either ConnectionString+ContainerName or ContainerUrl+SasToken");
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

    private string GetDestinationDisplayName()
    {
        var blobSettings = _settings.AzureBlobStorage;

        if (!string.IsNullOrWhiteSpace(blobSettings.ConnectionString) &&
            !string.IsNullOrWhiteSpace(blobSettings.ContainerName))
        {
            return blobSettings.ContainerName;
        }

        if (!string.IsNullOrWhiteSpace(blobSettings.ContainerUrl))
        {
            try
            {
                var uri = new Uri(blobSettings.ContainerUrl);
                return uri.Host + uri.AbsolutePath.TrimEnd('/');
            }
            catch
            {
                return blobSettings.ContainerUrl;
            }
        }

        return "(unknown)";
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
