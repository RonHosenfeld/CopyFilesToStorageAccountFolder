using CopyFilesToStorageAccountFolder.Models;

namespace CopyFilesToStorageAccountFolder.Services;

public record FolderEnumeration
{
    public string FolderPath { get; init; } = string.Empty;
    public List<DiscoveredFile> Files { get; init; } = [];
}

public record EnumerationResult
{
    public int TotalFolders { get; init; }
    public int TotalFiles { get; init; }
    public List<FolderEnumeration> Folders { get; init; } = [];
}

public interface IFileDiscoveryService
{
    IAsyncEnumerable<DiscoveredFile> DiscoverFilesAsync(CancellationToken cancellationToken = default);

    Task<EnumerationResult> PreEnumerateAllFilesAsync(
        Action<string>? onFolderScanned = null,
        CancellationToken cancellationToken = default);

    Task<DiscoveredFile> WithChecksumAsync(DiscoveredFile file, CancellationToken cancellationToken = default);
}
