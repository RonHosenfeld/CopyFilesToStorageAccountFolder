namespace CopyFilesToStorageAccountFolder.Models;

public record FileUploadResult(
    bool Success,
    string SourcePath,
    string BlobName,
    string Checksum,
    string? ErrorMessage = null
);

public record DiscoveredFile(
    string FullPath,
    string FileName,
    string Checksum,
    long FileSize
);
