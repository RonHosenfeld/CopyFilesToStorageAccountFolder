namespace CopyFilesToStorageAccountFolder.Services;

public record FolderProgress
{
    public string FolderPath { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int TotalFiles { get; init; }
    public int Succeeded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool IsCurrentFolder { get; set; }
    public int Processed => Succeeded + Skipped + Failed;
    public bool IsCompleted => Processed >= TotalFiles;
}

public record UploadState
{
    public List<string> SourceFolders { get; init; } = [];
    public string Destination { get; init; } = string.Empty;
    public int TotalDiscovered { get; set; }
    public int TotalFolders { get; set; }
    public int Succeeded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public long CurrentFileSize { get; set; }
    public DateTime SessionStartedAt { get; init; } = DateTime.UtcNow;
    public int ThrottleDelayMs { get; init; }
    public bool IsCompleted { get; set; }
    public string? LastError { get; set; }
    public bool IsEnumerating { get; set; }
    public string EnumerationStatus { get; set; } = string.Empty;
    public List<FolderProgress> FolderProgressList { get; init; } = [];
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

    // Folder progress methods
    void SetEnumerating(bool isEnumerating, string? status = null);
    void SetEnumerationCounts(int totalFolders, int totalFiles, List<FolderProgress> folderProgressList);
    void SetCurrentFolder(string folderPath);
    void RecordSuccess(string folderPath);
    void RecordSkipped(string folderPath);
    void RecordFailed(string folderPath, string? errorMessage = null);
}
