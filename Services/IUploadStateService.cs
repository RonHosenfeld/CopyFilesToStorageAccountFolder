namespace CopyFilesToStorageAccountFolder.Services;

public record UploadState
{
    public List<string> SourceFolders { get; init; } = [];
    public string Destination { get; init; } = string.Empty;
    public int TotalDiscovered { get; set; }
    public int Succeeded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public long CurrentFileSize { get; set; }
    public DateTime SessionStartedAt { get; init; } = DateTime.UtcNow;
    public int ThrottleDelayMs { get; init; }
    public bool IsCompleted { get; set; }
    public string? LastError { get; set; }
}

public interface IUploadStateService
{
    UploadState CurrentState { get; }
    event EventHandler? StateChanged;

    void Initialize(IEnumerable<string> sourceFolders, string destination, int throttleDelayMs);
    void UpdateDiscovered(int count);
    void SetCurrentFile(string filePath, long fileSize);
    void RecordSuccess();
    void RecordSkipped();
    void RecordFailed(string? errorMessage = null);
    void SetCompleted();
}
