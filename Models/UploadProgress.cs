namespace CopyFilesToStorageAccountFolder.Models;

public class UploadProgress
{
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<FileUploadEntry> CompletedFiles { get; set; } = [];
    public List<FileUploadEntry> FailedFiles { get; set; } = [];
}

public class FileUploadEntry
{
    public string SourcePath { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
