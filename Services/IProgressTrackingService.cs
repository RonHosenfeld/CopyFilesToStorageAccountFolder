using CopyFilesToStorageAccountFolder.Models;

namespace CopyFilesToStorageAccountFolder.Services;

public interface IProgressTrackingService
{
    Task<UploadProgress> LoadProgressAsync(CancellationToken cancellationToken = default);
    Task SaveProgressAsync(UploadProgress progress, CancellationToken cancellationToken = default);
    bool IsFileCompleted(UploadProgress progress, DiscoveredFile file);
    void MarkCompleted(UploadProgress progress, FileUploadResult result);
    void MarkFailed(UploadProgress progress, FileUploadResult result);
}
