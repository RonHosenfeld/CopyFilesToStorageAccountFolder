namespace CopyFilesToStorageAccountFolder.Services;

public class UploadStateService : IUploadStateService
{
    private readonly object _lock = new();
    private UploadState _state = new();

    public UploadState CurrentState
    {
        get
        {
            lock (_lock)
            {
                // Return a deep snapshot to avoid thread-safety issues with UI reading while worker modifies
                return _state with
                {
                    FolderProgressList = _state.FolderProgressList
                        .Select(f => f with { })  // Create copies of each FolderProgress
                        .ToList()
                };
            }
        }
    }

    public event EventHandler? StateChanged;

    public void Initialize(IEnumerable<string> sourceFolders, string destination, int throttleDelayMs)
    {
        lock (_lock)
        {
            _state = new UploadState
            {
                SourceFolders = sourceFolders.ToList(),
                Destination = destination,
                ThrottleDelayMs = throttleDelayMs,
                SessionStartedAt = DateTime.UtcNow
            };
        }
        OnStateChanged();
    }

    public void UpdateDiscovered(int count)
    {
        lock (_lock)
        {
            _state.TotalDiscovered = count;
        }
        OnStateChanged();
    }

    public void SetCurrentFile(string filePath, long fileSize)
    {
        lock (_lock)
        {
            _state.CurrentFile = filePath;
            _state.CurrentFileSize = fileSize;
        }
        OnStateChanged();
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _state.Succeeded++;
            _state.LastError = null;
        }
        OnStateChanged();
    }

    public void RecordSkipped()
    {
        lock (_lock)
        {
            _state.Skipped++;
        }
        OnStateChanged();
    }

    public void RecordFailed(string? errorMessage = null)
    {
        lock (_lock)
        {
            _state.Failed++;
            _state.LastError = errorMessage;
        }
        OnStateChanged();
    }

    public void SetCompleted()
    {
        lock (_lock)
        {
            _state.IsCompleted = true;
            _state.CurrentFile = string.Empty;
        }
        OnStateChanged();
    }

    public void SetEnumerating(bool isEnumerating, string? status = null)
    {
        lock (_lock)
        {
            _state.IsEnumerating = isEnumerating;
            _state.EnumerationStatus = status ?? string.Empty;
        }
        OnStateChanged();
    }

    public void SetEnumerationCounts(int totalFolders, int totalFiles, List<FolderProgress> folderProgressList)
    {
        lock (_lock)
        {
            _state.TotalFolders = totalFolders;
            _state.TotalDiscovered = totalFiles;
            _state.FolderProgressList.Clear();
            _state.FolderProgressList.AddRange(folderProgressList);
        }
        OnStateChanged();
    }

    public void SetCurrentFolder(string folderPath)
    {
        lock (_lock)
        {
            foreach (var folder in _state.FolderProgressList)
            {
                folder.IsCurrentFolder = folder.FolderPath == folderPath;
            }
        }
        OnStateChanged();
    }

    public void RecordSuccess(string folderPath)
    {
        lock (_lock)
        {
            _state.Succeeded++;
            _state.LastError = null;
            var folder = _state.FolderProgressList.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folder != null)
            {
                folder.Succeeded++;
            }
        }
        OnStateChanged();
    }

    public void RecordSkipped(string folderPath)
    {
        lock (_lock)
        {
            _state.Skipped++;
            var folder = _state.FolderProgressList.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folder != null)
            {
                folder.Skipped++;
            }
        }
        OnStateChanged();
    }

    public void RecordFailed(string folderPath, string? errorMessage = null)
    {
        lock (_lock)
        {
            _state.Failed++;
            _state.LastError = errorMessage;
            var folder = _state.FolderProgressList.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folder != null)
            {
                folder.Failed++;
            }
        }
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
