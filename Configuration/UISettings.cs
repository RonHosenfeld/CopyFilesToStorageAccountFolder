namespace CopyFilesToStorageAccountFolder.Configuration;

public class UISettings
{
    public bool Enabled { get; set; } = true;
    public int RefreshRateMs { get; set; } = 100;
    public int MaxVisibleFolders { get; set; } = 5;
}
