namespace CopyFilesToStorageAccountFolder.Models;

public record FileUploadResult(
    bool Success,
    string SourcePath,
    string BlobName,
    string? Checksum,
    string? ErrorMessage = null
);

public record DiscoveredFile(
    string FullPath,
    string FileName,
    long FileSize,
    string? Checksum = null
);
