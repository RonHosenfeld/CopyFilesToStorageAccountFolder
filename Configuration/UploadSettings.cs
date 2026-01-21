namespace CopyFilesToStorageAccountFolder.Configuration;

public class UploadSettings
{
    public AzureBlobStorageSettings AzureBlobStorage { get; set; } = new();
    public List<string> SourceFolders { get; set; } = [];
    public bool ScanRecursively { get; set; } = true;
    public FileFilterSettings FileFilters { get; set; } = new();
    public ThrottlingSettings Throttling { get; set; } = new();
    public string ProgressFile { get; set; } = "upload-progress.json";
}

public class AzureBlobStorageSettings
{
    /// <summary>
    /// Connection string for Azure Storage (use "UseDevelopmentStorage=true" for Azurite).
    /// If set, ContainerName must also be provided. Takes precedence over ContainerUrl/SasToken.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Container name when using ConnectionString authentication.
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Full container URL (without SAS token) for SAS authentication.
    /// </summary>
    public string ContainerUrl { get; set; } = string.Empty;

    /// <summary>
    /// SAS token for authentication (include the leading '?').
    /// </summary>
    public string SasToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional blob name prefix (virtual folder path). Leave empty for container root.
    /// </summary>
    public string? BlobPrefix { get; set; }
}

public class FileFilterSettings
{
    public List<string> IncludeExtensions { get; set; } = [];
    public List<string> ExcludeExtensions { get; set; } = [];
    public List<string> ExcludeFileNames { get; set; } = [];
}

public class ThrottlingSettings
{
    public int DelayBetweenFilesMs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
}
