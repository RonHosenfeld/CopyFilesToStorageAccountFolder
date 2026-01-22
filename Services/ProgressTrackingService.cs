using System.Text.Json;
using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Models;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder.Services;

public class ProgressTrackingService(
    IOptions<UploadSettings> settings,
    ILogger<ProgressTrackingService> logger) : IProgressTrackingService
{
    private readonly string _progressFilePath = settings.Value.ProgressFile;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<UploadProgress> LoadProgressAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_progressFilePath))
        {
            logger.LogInformation("No existing progress file found, starting fresh");
            return new UploadProgress();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_progressFilePath, cancellationToken);
            var progress = JsonSerializer.Deserialize<UploadProgress>(json, JsonOptions);

            if (progress is not null)
            {
                logger.LogInformation(
                    "Loaded progress: {CompletedCount} completed, {FailedCount} failed",
                    progress.CompletedFiles.Count, progress.FailedFiles.Count);
                return progress;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load progress file, starting fresh");
        }

        return new UploadProgress();
    }

    public async Task SaveProgressAsync(UploadProgress progress, CancellationToken cancellationToken = default)
    {
        var tempPath = _progressFilePath + ".tmp";

        try
        {
            var json = JsonSerializer.Serialize(progress, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, _progressFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save progress file");
            throw;
        }
    }

    public bool IsFileCompleted(UploadProgress progress, DiscoveredFile file)
    {
        return progress.CompletedFiles.Any(c =>
            c.SourcePath == file.FullPath && c.Checksum == file.Checksum);
    }

    public void MarkCompleted(UploadProgress progress, FileUploadResult result)
    {
        progress.FailedFiles.RemoveAll(f => f.SourcePath == result.SourcePath);

        progress.CompletedFiles.Add(new FileUploadEntry
        {
            SourcePath = result.SourcePath,
            BlobName = result.BlobName,
            Checksum = result.Checksum ?? string.Empty,
            Timestamp = DateTime.UtcNow
        });
    }

    public void MarkFailed(UploadProgress progress, FileUploadResult result)
    {
        progress.FailedFiles.Add(new FileUploadEntry
        {
            SourcePath = result.SourcePath,
            BlobName = result.BlobName,
            Checksum = result.Checksum ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            ErrorMessage = result.ErrorMessage
        });
    }
}
