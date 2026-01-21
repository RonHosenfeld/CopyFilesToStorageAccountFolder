using System.Security.Cryptography;
using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Models;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder.Services;

public class FileDiscoveryService(
    IOptions<UploadSettings> settings,
    ILogger<FileDiscoveryService> logger) : IFileDiscoveryService
{
    private readonly UploadSettings _settings = settings.Value;

    public async IAsyncEnumerable<DiscoveredFile> DiscoverFilesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var folder in _settings.SourceFolders)
        {
            if (!Directory.Exists(folder))
            {
                logger.LogWarning("Source folder does not exist: {Folder}", folder);
                continue;
            }

            var searchOption = _settings.ScanRecursively
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(folder, "*", searchOption);

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!ShouldIncludeFile(filePath))
                {
                    logger.LogDebug("Skipping file due to filter: {FilePath}", filePath);
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                var checksum = await ComputeChecksumAsync(filePath, cancellationToken);

                yield return new DiscoveredFile(
                    FullPath: filePath,
                    FileName: fileInfo.Name,
                    Checksum: checksum,
                    FileSize: fileInfo.Length
                );
            }
        }
    }

    private bool ShouldIncludeFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (_settings.FileFilters.ExcludeFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_settings.FileFilters.ExcludeExtensions.Count > 0 &&
            _settings.FileFilters.ExcludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_settings.FileFilters.IncludeExtensions.Count > 0 &&
            !_settings.FileFilters.IncludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static async Task<string> ComputeChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToBase64String(hash);
    }
}
