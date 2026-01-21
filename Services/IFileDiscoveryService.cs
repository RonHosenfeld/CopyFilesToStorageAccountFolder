using CopyFilesToStorageAccountFolder.Models;

namespace CopyFilesToStorageAccountFolder.Services;

public interface IFileDiscoveryService
{
    IAsyncEnumerable<DiscoveredFile> DiscoverFilesAsync(CancellationToken cancellationToken = default);
}
