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
                return _state;
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

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
