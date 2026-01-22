namespace CopyFilesToStorageAccountFolder.UI;

public interface IConsoleUI
{
    Task RunAsync(CancellationToken cancellationToken);
}
