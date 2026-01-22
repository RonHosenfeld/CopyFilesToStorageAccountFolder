namespace CopyFilesToStorageAccountFolder.UI;

public class ConsoleUIHostedService(IConsoleUI consoleUI) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await consoleUI.RunAsync(stoppingToken);
    }
}
