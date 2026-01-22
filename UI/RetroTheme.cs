using Spectre.Console;

namespace CopyFilesToStorageAccountFolder.UI;

public static class RetroTheme
{
    // Phosphor green CRT colors
    public static readonly Color Primary = new(0, 255, 128);
    public static readonly Color Success = new(0, 255, 128);
    public static readonly Color Warning = new(255, 200, 0);
    public static readonly Color Error = new(255, 80, 80);
    public static readonly Color Info = new(0, 200, 255);
    public static readonly Color Dimmed = new(80, 80, 80);
    public static readonly Color ProgressBarEmpty = new(40, 40, 40);

    public static readonly Style PrimaryStyle = new(Primary);
    public static readonly Style SuccessStyle = new(Success);
    public static readonly Style WarningStyle = new(Warning);
    public static readonly Style ErrorStyle = new(Error);
    public static readonly Style InfoStyle = new(Info);
    public static readonly Style DimmedStyle = new(Dimmed);

    public const string AsciiLogo = @"
  _____ _ _        _   _       _                 _
 |  ___(_) | ___  | | | |_ __ | | ___   __ _  __| | ___ _ __
 | |_  | | |/ _ \ | | | | '_ \| |/ _ \ / _` |/ _` |/ _ \ '__|
 |  _| | | |  __/ | |_| | |_) | | (_) | (_| | (_| |  __/ |
 |_|   |_|_|\___|  \___/| .__/|_|\___/ \__,_|\__,_|\___|_|
                        |_|            AZURE BLOB UPLOADER   ";

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }

    public static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        var fileName = Path.GetFileName(path);
        if (fileName.Length >= maxLength - 3)
            return "..." + fileName[^(maxLength - 3)..];

        var remaining = maxLength - fileName.Length - 4;
        if (remaining <= 0)
            return "..." + fileName;

        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        if (dir.Length <= remaining)
            return path;

        return dir[..remaining] + ".../" + fileName;
    }
}
