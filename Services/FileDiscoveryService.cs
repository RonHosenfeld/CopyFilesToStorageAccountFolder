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
            yield return await CreateDiscoveredFileAsync(filePath, computeChecksum: true, cancellationToken);
        }

        foreach (var filePath in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await CreateDiscoveredFileAsync(filePath, computeChecksum: true, cancellationToken);
        }
    }

    private static bool IsJsonFile(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DiscoveredFile> CreateDiscoveredFileAsync(string filePath, bool computeChecksum, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        string? checksum = null;

        if (computeChecksum)
        {
            checksum = await ComputeChecksumInternalAsync(filePath, cancellationToken);
        }

        return new DiscoveredFile(
            FullPath: filePath,
            FileName: fileInfo.Name,
            FileSize: fileInfo.Length,
            Checksum: checksum
        );
    }

    public async Task<DiscoveredFile> WithChecksumAsync(DiscoveredFile file, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(file.Checksum))
            return file;

        var checksum = await ComputeChecksumInternalAsync(file.FullPath, cancellationToken);
        return file with { Checksum = checksum };
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

    private static async Task<string> ComputeChecksumInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToBase64String(hash);
    }

    public async Task<EnumerationResult> PreEnumerateAllFilesAsync(
        Action<string>? onFolderScanned = null,
        CancellationToken cancellationToken = default)
    {
        var folders = new List<FolderEnumeration>();

        foreach (var sourceFolder in _settings.SourceFolders)
        {
            if (!Directory.Exists(sourceFolder))
            {
                logger.LogWarning("Source folder does not exist: {Folder}", sourceFolder);
                continue;
            }

            foreach (var directory in GetDirectoriesToProcess(sourceFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                onFolderScanned?.Invoke(directory);

                var files = await EnumerateDirectoryFilesAsync(directory, cancellationToken);
                if (files.Count > 0)
                {
                    folders.Add(new FolderEnumeration
                    {
                        FolderPath = directory,
                        Files = files
                    });
                }
            }
        }

        return new EnumerationResult
        {
            TotalFolders = folders.Count,
            TotalFiles = folders.Sum(f => f.Files.Count),
            Folders = folders
        };
    }

    private async Task<List<DiscoveredFile>> EnumerateDirectoryFilesAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var result = new List<DiscoveredFile>();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied to directory: {Directory}", directory);
            return result;
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
            result.Add(await CreateDiscoveredFileAsync(filePath, computeChecksum: false, cancellationToken));
        }

        foreach (var filePath in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await CreateDiscoveredFileAsync(filePath, computeChecksum: false, cancellationToken));
        }

        return result;
    }
}
