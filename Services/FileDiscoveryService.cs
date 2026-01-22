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

            foreach (var directory in GetDirectoriesToProcess(folder))
            {
                await foreach (var file in ProcessDirectoryAsync(directory, cancellationToken))
                {
                    yield return file;
                }
            }
        }
    }

    private IEnumerable<string> GetDirectoriesToProcess(string sourceFolder)
    {
        yield return sourceFolder;

        if (!_settings.ScanRecursively)
        {
            yield break;
        }

        IEnumerable<string> subdirectories;
        try
        {
            subdirectories = Directory.EnumerateDirectories(sourceFolder, "*", SearchOption.AllDirectories)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied to subdirectories in: {Folder}", sourceFolder);
            yield break;
        }

        foreach (var subdir in subdirectories)
        {
            yield return subdir;
        }
    }

    private async IAsyncEnumerable<DiscoveredFile> ProcessDirectoryAsync(
        string directory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied to directory: {Directory}", directory);
            yield break;
        }

        var filteredFiles = files
            .Where(ShouldIncludeFile)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nonJsonFiles = filteredFiles.Where(f => !IsJsonFile(f));
        var jsonFiles = filteredFiles.Where(IsJsonFile);

        foreach (var filePath in nonJsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await CreateDiscoveredFileAsync(filePath, cancellationToken);
        }

        foreach (var filePath in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await CreateDiscoveredFileAsync(filePath, cancellationToken);
        }
    }

    private static bool IsJsonFile(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DiscoveredFile> CreateDiscoveredFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var checksum = await ComputeChecksumAsync(filePath, cancellationToken);

        return new DiscoveredFile(
            FullPath: filePath,
            FileName: fileInfo.Name,
            Checksum: checksum,
            FileSize: fileInfo.Length
        );
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
