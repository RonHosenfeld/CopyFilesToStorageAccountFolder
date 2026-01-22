using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CopyFilesToStorageAccountFolder.Services;

public class SqliteProgressTrackingService : IProgressTrackingService, IDisposable
{
    private readonly string _databasePath;
    private readonly ILogger<SqliteProgressTrackingService> _logger;
    private SqliteConnection? _connection;
    private HashSet<string> _completedFilesCache = new();
    private bool _disposed;

    public SqliteProgressTrackingService(
        IOptions<UploadSettings> settings,
        ILogger<SqliteProgressTrackingService> logger)
    {
        _databasePath = settings.Value.ProgressDatabase;
        _logger = logger;
    }

    private SqliteConnection GetConnection()
    {
        if (_connection is not null)
            return _connection;

        _connection = new SqliteConnection($"Data Source={_databasePath}");
        _connection.Open();
        InitializeSchema();
        return _connection;
    }

    private void InitializeSchema()
    {
        const string schema = """
            CREATE TABLE IF NOT EXISTS upload_sessions (
                id INTEGER PRIMARY KEY,
                started_at TEXT NOT NULL,
                completed_at TEXT
            );

            CREATE TABLE IF NOT EXISTS completed_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_path TEXT NOT NULL,
                blob_name TEXT NOT NULL,
                checksum TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                UNIQUE(source_path, checksum)
            );

            CREATE TABLE IF NOT EXISTS failed_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_path TEXT NOT NULL,
                blob_name TEXT NOT NULL,
                checksum TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                error_message TEXT,
                UNIQUE(source_path, checksum)
            );

            CREATE INDEX IF NOT EXISTS idx_completed_lookup ON completed_files(source_path, checksum);
            CREATE INDEX IF NOT EXISTS idx_failed_lookup ON failed_files(source_path, checksum);
            """;

        using var command = _connection!.CreateCommand();
        command.CommandText = schema;
        command.ExecuteNonQuery();

        _logger.LogDebug("SQLite schema initialized at {DatabasePath}", _databasePath);
    }

    public async Task<UploadProgress> LoadProgressAsync(CancellationToken cancellationToken = default)
    {
        var connection = GetConnection();
        var progress = new UploadProgress();

        // Load session info
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT started_at, completed_at FROM upload_sessions WHERE id = 1";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                progress.StartedAt = DateTime.Parse(reader.GetString(0));
                progress.CompletedAt = reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1));
            }
        }

        // Load completed files and build cache
        _completedFilesCache.Clear();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT source_path, blob_name, checksum, timestamp FROM completed_files";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entry = new FileUploadEntry
                {
                    SourcePath = reader.GetString(0),
                    BlobName = reader.GetString(1),
                    Checksum = reader.GetString(2),
                    Timestamp = DateTime.Parse(reader.GetString(3))
                };
                progress.CompletedFiles.Add(entry);
                _completedFilesCache.Add(GetCacheKey(entry.SourcePath, entry.Checksum));
            }
        }

        // Load failed files
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT source_path, blob_name, checksum, timestamp, error_message FROM failed_files";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                progress.FailedFiles.Add(new FileUploadEntry
                {
                    SourcePath = reader.GetString(0),
                    BlobName = reader.GetString(1),
                    Checksum = reader.GetString(2),
                    Timestamp = DateTime.Parse(reader.GetString(3)),
                    ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }

        _logger.LogInformation(
            "Loaded progress from SQLite: {CompletedCount} completed, {FailedCount} failed",
            progress.CompletedFiles.Count, progress.FailedFiles.Count);

        return progress;
    }

    public async Task SaveProgressAsync(UploadProgress progress, CancellationToken cancellationToken = default)
    {
        var connection = GetConnection();

        // Save/update session info
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO upload_sessions (id, started_at, completed_at)
            VALUES (1, @started_at, @completed_at)
            """;
        cmd.Parameters.AddWithValue("@started_at", progress.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@completed_at",
            progress.CompletedAt.HasValue ? progress.CompletedAt.Value.ToString("O") : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public bool IsFileCompleted(UploadProgress progress, DiscoveredFile file)
    {
        // O(1) lookup using HashSet cache
        return _completedFilesCache.Contains(GetCacheKey(file.FullPath, file.Checksum));
    }

    public void MarkCompleted(UploadProgress progress, FileUploadResult result)
    {
        var connection = GetConnection();

        // Remove from failed_files if exists
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM failed_files WHERE source_path = @source_path";
            cmd.Parameters.AddWithValue("@source_path", result.SourcePath);
            cmd.ExecuteNonQuery();
        }

        // Insert into completed_files
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR REPLACE INTO completed_files (source_path, blob_name, checksum, timestamp)
                VALUES (@source_path, @blob_name, @checksum, @timestamp)
                """;
            cmd.Parameters.AddWithValue("@source_path", result.SourcePath);
            cmd.Parameters.AddWithValue("@blob_name", result.BlobName);
            cmd.Parameters.AddWithValue("@checksum", result.Checksum);
            cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        // Update cache
        _completedFilesCache.Add(GetCacheKey(result.SourcePath, result.Checksum));

        // Update in-memory progress for consistency
        progress.FailedFiles.RemoveAll(f => f.SourcePath == result.SourcePath);
        progress.CompletedFiles.Add(new FileUploadEntry
        {
            SourcePath = result.SourcePath,
            BlobName = result.BlobName,
            Checksum = result.Checksum,
            Timestamp = DateTime.UtcNow
        });
    }

    public void MarkFailed(UploadProgress progress, FileUploadResult result)
    {
        var connection = GetConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO failed_files (source_path, blob_name, checksum, timestamp, error_message)
            VALUES (@source_path, @blob_name, @checksum, @timestamp, @error_message)
            """;
        cmd.Parameters.AddWithValue("@source_path", result.SourcePath);
        cmd.Parameters.AddWithValue("@blob_name", result.BlobName);
        cmd.Parameters.AddWithValue("@checksum", result.Checksum);
        cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@error_message",
            result.ErrorMessage is not null ? result.ErrorMessage : DBNull.Value);
        cmd.ExecuteNonQuery();

        // Update in-memory progress for consistency
        progress.FailedFiles.Add(new FileUploadEntry
        {
            SourcePath = result.SourcePath,
            BlobName = result.BlobName,
            Checksum = result.Checksum,
            Timestamp = DateTime.UtcNow,
            ErrorMessage = result.ErrorMessage
        });
    }

    private static string GetCacheKey(string sourcePath, string checksum)
    {
        return $"{sourcePath}|{checksum}";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Close();
        _connection?.Dispose();
        _disposed = true;
    }
}
