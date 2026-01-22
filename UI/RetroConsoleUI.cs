using CopyFilesToStorageAccountFolder.Configuration;
using CopyFilesToStorageAccountFolder.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CopyFilesToStorageAccountFolder.UI;

public class RetroConsoleUI(
    IUploadStateService stateService,
    IOptions<UISettings> uiSettings) : IConsoleUI
{
    private readonly int _refreshRateMs = uiSettings.Value.RefreshRateMs;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.Cursor.Hide();

        try
        {
            await AnsiConsole.Live(BuildDisplay())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ctx.UpdateTarget(BuildDisplay());

                        var state = stateService.CurrentState;
                        if (state.IsCompleted)
                        {
                            ctx.UpdateTarget(BuildDisplay());
                            await Task.Delay(1000, cancellationToken);
                            break;
                        }

                        await Task.Delay(_refreshRateMs, cancellationToken);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            AnsiConsole.Cursor.Show();
        }
    }

    private IRenderable BuildDisplay()
    {
        var state = stateService.CurrentState;
        var table = new Table()
            .Border(TableBorder.Double)
            .BorderColor(RetroTheme.Primary)
            .Expand();

        table.AddColumn(new TableColumn(string.Empty).NoWrap());

        // Header with ASCII logo
        table.AddRow(BuildHeader());
        table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));

        // Source/Destination panel
        table.AddRow(BuildSourceDestPanel(state));
        table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));

        // Progress panel
        table.AddRow(BuildProgressPanel(state));
        table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));

        // Current file
        table.AddRow(BuildCurrentFilePanel(state));
        table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));

        // Stats footer
        table.AddRow(BuildStatsFooter(state));

        return table;
    }

    private static IRenderable BuildHeader()
    {
        return new Markup($"[rgb(0,255,128)]{RetroTheme.AsciiLogo.EscapeMarkup()}[/]");
    }

    private static IRenderable BuildSourceDestPanel(UploadState state)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(36));
        grid.AddColumn(new GridColumn());

        var sourceText = state.SourceFolders.Count > 0
            ? RetroTheme.TruncatePath(state.SourceFolders[0], 32)
            : "(none)";

        if (state.SourceFolders.Count > 1)
            sourceText += $" (+{state.SourceFolders.Count - 1} more)";

        var destText = !string.IsNullOrEmpty(state.Destination)
            ? RetroTheme.TruncatePath(state.Destination, 28)
            : "(none)";

        grid.AddRow(
            new Markup($"[rgb(0,255,128) bold]SOURCE FOLDERS[/]"),
            new Markup($"[rgb(0,255,128) bold]DESTINATION[/]"));
        grid.AddRow(
            new Markup($"[rgb(0,200,255)]> {sourceText.EscapeMarkup()}[/]"),
            new Markup($"[rgb(0,200,255)]{destText.EscapeMarkup()}[/]"));

        return grid;
    }

    private static IRenderable BuildProgressPanel(UploadState state)
    {
        var panel = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand();

        panel.AddColumn(new TableColumn(string.Empty).Width(12));
        panel.AddColumn(new TableColumn(string.Empty));
        panel.AddColumn(new TableColumn(string.Empty).Width(20).RightAligned());

        var total = state.TotalDiscovered;
        var processed = state.Succeeded + state.Skipped + state.Failed;

        // TOTAL row
        panel.AddRow(
            new Markup("[rgb(0,255,128)]TOTAL[/]"),
            BuildProgressBar(total, Math.Max(total, 1), RetroTheme.Primary),
            new Markup($"[rgb(0,255,128)]{total:N0}[/]"));

        // UPLOADED row
        var uploadedPct = total > 0 ? (state.Succeeded * 100.0 / total) : 0;
        panel.AddRow(
            new Markup("[rgb(0,255,128)]UPLOADED[/]"),
            BuildProgressBar(state.Succeeded, Math.Max(total, 1), RetroTheme.Success),
            new Markup($"[rgb(0,255,128)]{state.Succeeded:N0}[/] [dim]({uploadedPct:F1}%)[/]"));

        // SKIPPED row
        var skippedPct = total > 0 ? (state.Skipped * 100.0 / total) : 0;
        panel.AddRow(
            new Markup("[rgb(255,200,0)]SKIPPED[/]"),
            BuildProgressBar(state.Skipped, Math.Max(total, 1), RetroTheme.Warning),
            new Markup($"[rgb(255,200,0)]{state.Skipped:N0}[/] [dim]({skippedPct:F1}%)[/]"));

        // FAILED row
        var failedPct = total > 0 ? (state.Failed * 100.0 / total) : 0;
        panel.AddRow(
            new Markup("[rgb(255,80,80)]FAILED[/]"),
            BuildProgressBar(state.Failed, Math.Max(total, 1), RetroTheme.Error),
            new Markup($"[rgb(255,80,80)]{state.Failed:N0}[/] [dim]({failedPct:F1}%)[/]"));

        var progressHeader = new Markup("[rgb(0,255,128) bold]PROGRESS[/]");
        var content = new Rows(progressHeader, panel);
        return content;
    }

    private static IRenderable BuildProgressBar(int value, int max, Color color)
    {
        var ratio = Math.Min((double)value / max, 1.0);
        var width = 40;
        var filled = (int)(ratio * width);
        var empty = width - filled;

        var filledStr = new string('#', filled);
        var emptyStr = new string(' ', empty);

        return new Markup($"[[[rgb({color.R},{color.G},{color.B})]{filledStr}[/][rgb(40,40,40)]{emptyStr}[/]]]");
    }

    private static IRenderable BuildCurrentFilePanel(UploadState state)
    {
        string text;
        if (state.IsCompleted)
        {
            text = "[rgb(0,255,128) bold]COMPLETED[/]";
        }
        else if (string.IsNullOrEmpty(state.CurrentFile))
        {
            text = "[dim]Waiting for files...[/]";
        }
        else
        {
            var fileName = Path.GetFileName(state.CurrentFile);
            var truncatedName = fileName.Length > 50 ? fileName[..47] + "..." : fileName;
            var sizeStr = RetroTheme.FormatFileSize(state.CurrentFileSize);
            text = $"[rgb(0,200,255)]CURRENT:[/] [rgb(0,255,128)]{truncatedName.EscapeMarkup()}[/] [dim]({sizeStr})[/]";
        }

        if (!string.IsNullOrEmpty(state.LastError))
        {
            text += $"\n[rgb(255,80,80)]LAST ERROR: {state.LastError.EscapeMarkup()}[/]";
        }

        return new Markup(text);
    }

    private static IRenderable BuildStatsFooter(UploadState state)
    {
        var elapsed = DateTime.UtcNow - state.SessionStartedAt;
        var elapsedStr = elapsed.ToString(@"hh\:mm\:ss");

        var processed = state.Succeeded + state.Skipped + state.Failed;
        var rate = elapsed.TotalSeconds > 0 ? processed / elapsed.TotalSeconds : 0;

        var statusText = state.IsCompleted ? "[rgb(0,255,128) bold]DONE[/]" : "[rgb(0,200,255)]RUNNING[/]";

        return new Markup(
            $"[dim]Elapsed:[/] [rgb(0,255,128)]{elapsedStr}[/]   " +
            $"[dim]Throttle:[/] [rgb(0,255,128)]{state.ThrottleDelayMs}ms[/]   " +
            $"[dim]Rate:[/] [rgb(0,255,128)]~{rate:F1} files/sec[/]   " +
            $"[dim]Status:[/] {statusText}");
    }
}
