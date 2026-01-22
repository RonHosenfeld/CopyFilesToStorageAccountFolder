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
    private readonly int _maxVisibleFolders = uiSettings.Value.MaxVisibleFolders;

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
        try
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

            // Show enumeration panel during scanning, otherwise show folder progress
            if (state.IsEnumerating)
            {
                table.AddRow(BuildEnumerationPanel(state));
                table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));
            }
            else if (state.FolderProgressList.Count > 0)
            {
                table.AddRow(BuildFolderProgressPanel(state));
                table.AddRow(new Rule().RuleStyle(RetroTheme.PrimaryStyle));
            }

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
        catch (Exception ex)
        {
            // If rendering fails, show error message instead of crashing
            return new Markup($"[red]UI Error: {ex.Message.EscapeMarkup()}[/]");
        }
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

        // Show folder/file counts after enumeration
        if (state.TotalFolders > 0)
        {
            grid.AddRow(
                new Markup($"[dim]Folders:[/] [rgb(0,255,128)]{state.TotalFolders:N0}[/]  [dim]|[/]  [dim]Files:[/] [rgb(0,255,128)]{state.TotalDiscovered:N0}[/]"),
                new Text(string.Empty));
        }

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

        var statusText = state.IsCompleted
            ? "[rgb(0,255,128) bold]DONE[/]"
            : state.IsEnumerating
                ? "[rgb(255,200,0)]SCANNING[/]"
                : "[rgb(0,200,255)]RUNNING[/]";

        return new Markup(
            $"[dim]Elapsed:[/] [rgb(0,255,128)]{elapsedStr}[/]   " +
            $"[dim]Throttle:[/] [rgb(0,255,128)]{state.ThrottleDelayMs}ms[/]   " +
            $"[dim]Rate:[/] [rgb(0,255,128)]~{rate:F1} files/sec[/]   " +
            $"[dim]Status:[/] {statusText}");
    }

    private static IRenderable BuildEnumerationPanel(UploadState state)
    {
        var rows = new List<IRenderable>
        {
            new Markup("[rgb(255,200,0) bold]SCANNING...[/]")
        };

        if (!string.IsNullOrEmpty(state.EnumerationStatus))
        {
            var truncatedStatus = state.EnumerationStatus.Length > 60
                ? "..." + state.EnumerationStatus[^57..]
                : state.EnumerationStatus;
            rows.Add(new Markup($"[dim]{truncatedStatus.EscapeMarkup()}[/]"));
        }

        return new Rows(rows);
    }

    private IRenderable BuildFolderProgressPanel(UploadState state)
    {
        var folderList = state.FolderProgressList;
        if (folderList.Count == 0)
            return new Text(string.Empty);

        var rows = new List<IRenderable>
        {
            new Markup("[rgb(0,255,128) bold]FOLDER PROGRESS[/]")
        };

        // Find current folder index for scrolling
        var currentIndex = folderList.FindIndex(f => f.IsCurrentFolder);
        if (currentIndex < 0) currentIndex = 0;

        // Calculate visible window
        var startIndex = 0;
        var endIndex = Math.Min(_maxVisibleFolders, folderList.Count);

        if (folderList.Count > _maxVisibleFolders)
        {
            // Center current folder in view, with bias toward showing more below
            var halfVisible = _maxVisibleFolders / 2;
            startIndex = Math.Max(0, currentIndex - halfVisible);
            endIndex = Math.Min(folderList.Count, startIndex + _maxVisibleFolders);

            // Adjust if we hit the end
            if (endIndex == folderList.Count)
            {
                startIndex = Math.Max(0, endIndex - _maxVisibleFolders);
            }
        }

        // Add "more above" indicator
        if (startIndex > 0)
        {
            rows.Add(new Markup($"[dim]    ^ {startIndex} more above[/]"));
        }

        // Build folder progress rows
        for (var i = startIndex; i < endIndex; i++)
        {
            var folder = folderList[i];
            rows.Add(BuildFolderProgressRow(folder));
        }

        // Add "more below" indicator
        var remaining = folderList.Count - endIndex;
        if (remaining > 0)
        {
            rows.Add(new Markup($"[dim]    v {remaining} more below[/]"));
        }

        return new Rows(rows);
    }

    private static IRenderable BuildFolderProgressRow(FolderProgress folder)
    {
        var prefix = folder.IsCurrentFolder ? "> " : "  ";
        var color = folder.IsCurrentFolder
            ? "rgb(0,200,255)"
            : folder.IsCompleted
                ? "dim"
                : "rgb(0,255,128)";

        // Truncate folder name if too long and escape for markup
        var displayName = folder.DisplayName ?? "(unknown)";
        if (displayName.Length > 25)
            displayName = "..." + displayName[^22..];
        var escapedName = displayName.EscapeMarkup().PadRight(25);

        var progressRatio = folder.TotalFiles > 0
            ? (double)folder.Processed / folder.TotalFiles
            : 0;
        var barWidth = 20;
        var filledWidth = (int)(progressRatio * barWidth);
        var emptyWidth = barWidth - filledWidth;

        var filledBar = new string('#', filledWidth);
        var emptyBar = new string(' ', emptyWidth);

        var barColor = folder.IsCompleted
            ? "rgb(0,255,128)"
            : folder.IsCurrentFolder
                ? "rgb(0,200,255)"
                : "rgb(100,100,100)";

        var stats = $"{folder.Processed}/{folder.TotalFiles}";
        if (folder.Failed > 0)
        {
            stats += $" [rgb(255,80,80)]({folder.Failed} failed)[/]";
        }

        return new Markup(
            $"[{color}]{prefix}{escapedName}[/] [[[{barColor}]{filledBar}[/][rgb(40,40,40)]{emptyBar}[/]]] [{color}]{stats}[/]");
    }
}
