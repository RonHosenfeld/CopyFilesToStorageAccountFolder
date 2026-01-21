using CopyFilesToStorageAccountFolder.Models;

namespace CopyFilesToStorageAccountFolder.Services;

public interface IBlobUploadService
{
    Task<FileUploadResult> UploadFileAsync(DiscoveredFile file, CancellationToken cancellationToken = default);
}
