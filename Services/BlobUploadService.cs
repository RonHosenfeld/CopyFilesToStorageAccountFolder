using Azure;
using Azure.Storage.Blobs;
using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Models;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder.Services;

public class BlobUploadService : IBlobUploadService
{
    private readonly BlobContainerClient _containerClient;
    private readonly UploadSettings _settings;
    private readonly ILogger<BlobUploadService> _logger;

    public BlobUploadService(
        IOptions<UploadSettings> settings,
        ILogger<BlobUploadService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var containerUri = new Uri(_settings.AzureBlobStorage.ContainerUrl + _settings.AzureBlobStorage.SasToken);
        _containerClient = new BlobContainerClient(containerUri);
    }

    public async Task<FileUploadResult> UploadFileAsync(DiscoveredFile file, CancellationToken cancellationToken = default)
    {
        var blobName = file.FileName;
        var retryCount = 0;
        var maxRetries = _settings.Throttling.MaxRetries;

        while (retryCount <= maxRetries)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);

                _logger.LogInformation(
                    "Uploading {SourcePath} to blob {BlobName} (attempt {Attempt}/{MaxAttempts})",
                    file.FullPath, blobName, retryCount + 1, maxRetries + 1);

                await using var stream = File.OpenRead(file.FullPath);
                await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

                _logger.LogInformation(
                    "Successfully uploaded {BlobName} ({Size} bytes)",
                    blobName, file.FileSize);

                return new FileUploadResult(
                    Success: true,
                    SourcePath: file.FullPath,
                    BlobName: blobName,
                    Checksum: file.Checksum
                );
            }
            catch (RequestFailedException ex) when (IsTransientError(ex) && retryCount < maxRetries)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));

                _logger.LogWarning(
                    ex,
                    "Transient error uploading {BlobName}, retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                    blobName, delay.TotalSeconds, retryCount + 1, maxRetries + 1);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload {SourcePath} to {BlobName}", file.FullPath, blobName);

                return new FileUploadResult(
                    Success: false,
                    SourcePath: file.FullPath,
                    BlobName: blobName,
                    Checksum: file.Checksum,
                    ErrorMessage: ex.Message
                );
            }
        }

        return new FileUploadResult(
            Success: false,
            SourcePath: file.FullPath,
            BlobName: blobName,
            Checksum: file.Checksum,
            ErrorMessage: $"Max retries ({maxRetries}) exceeded"
        );
    }

    private static bool IsTransientError(RequestFailedException ex)
    {
        return ex.Status is 429 or 503 or 500 or 502 or 504;
    }
}
