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
    public string ContainerUrl { get; set; } = string.Empty;
    public string SasToken { get; set; } = string.Empty;
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
