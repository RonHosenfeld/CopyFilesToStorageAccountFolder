namespace CopyFilesToStorageAccountFolder.UI;

public class NullConsoleUI : IConsoleUI
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
