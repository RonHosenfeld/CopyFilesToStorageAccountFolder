# Azure Blob Storage File Upload Utility

A .NET 10.0 Worker Service that uploads files from local folders to Azure Blob Storage with throttling, progress tracking, and resumability.

## Features

- **File Discovery**: Scans configured folders (recursively or flat) with extension and filename filters
- **Resumable Uploads**: Tracks progress in a JSON file; resumes where it left off after interruption
- **Change Detection**: MD5 checksums ensure modified files are re-uploaded
- **Retry Logic**: Exponential backoff for transient Azure errors (429, 503, etc.)
- **Throttling**: Configurable delay between uploads to avoid rate limiting
- **Structured Logging**: Serilog with Console and Seq sink support

## Project Structure

```
├── Configuration/
│   └── UploadSettings.cs       # Strongly-typed configuration classes
├── Models/
│   ├── UploadProgress.cs       # Progress tracking model
│   └── FileUploadResult.cs     # Upload result records
├── Services/
│   ├── IFileDiscoveryService.cs / FileDiscoveryService.cs
│   ├── IBlobUploadService.cs / BlobUploadService.cs
│   └── IProgressTrackingService.cs / ProgressTrackingService.cs
├── Worker.cs                   # Upload orchestration
├── Program.cs                  # DI and Serilog setup
└── appsettings.json            # Configuration
```

## Prerequisites

- .NET 10.0 SDK
- Azure Storage Account with a Blob Container
- SAS token with write permissions to the container

## Configuration

Edit `appsettings.json` with your settings:

```json
{
  "UploadSettings": {
    "AzureBlobStorage": {
      "ContainerUrl": "https://youraccount.blob.core.windows.net/yourcontainer",
      "SasToken": "?sv=2022-11-02&ss=b&srt=o&sp=rwc&se=..."
    },
    "SourceFolders": [
      "/path/to/folder1",
      "/path/to/folder2"
    ],
    "ScanRecursively": true,
    "FileFilters": {
      "IncludeExtensions": [".pdf", ".hl7", ".json"],
      "ExcludeExtensions": [".csv"],
      "ExcludeFileNames": ["results.json"]
    },
    "Throttling": {
      "DelayBetweenFilesMs": 100,
      "MaxRetries": 3
    },
    "ProgressFile": "upload-progress.json"
  }
}
```

### Configuration Options

| Setting | Description |
|---------|-------------|
| `ContainerUrl` | Azure Blob Storage container URL (without SAS token) |
| `SasToken` | SAS token with write permissions (include the leading `?`) |
| `SourceFolders` | List of local folders to scan for files |
| `ScanRecursively` | `true` to include subfolders, `false` for top-level only |
| `IncludeExtensions` | Only upload files with these extensions (empty = all) |
| `ExcludeExtensions` | Skip files with these extensions |
| `ExcludeFileNames` | Skip files with these exact names |
| `DelayBetweenFilesMs` | Milliseconds to wait between uploads |
| `MaxRetries` | Number of retry attempts for transient errors |
| `ProgressFile` | Path to the JSON progress file |

### Using User Secrets (Recommended for SAS Token)

For local development, store sensitive values in user secrets:

```bash
dotnet user-secrets set "UploadSettings:AzureBlobStorage:SasToken" "?sv=2022-11-02&ss=b..."
```

## Building and Running

```bash
# Build the project
dotnet build

# Run the service
dotnet run

# Or run the compiled binary
dotnet bin/Debug/net10.0/CopyFilesToStorageAccountFolder.dll
```

## How It Works

1. **Validation**: Checks that Azure credentials and source folders are configured
2. **Load Progress**: Reads `upload-progress.json` (or creates new if not found)
3. **File Discovery**: Scans source folders, applies filters, computes MD5 checksums
4. **Upload Loop**: For each pending file:
   - Extracts filename only (flattened path structure)
   - Uploads to Azure Blob Storage (overwrites if blob exists)
   - Retries transient errors with exponential backoff
   - Saves progress immediately after each file
   - Applies throttle delay
5. **Completion**: Logs summary and exits

## Blob Naming

Files are uploaded with **flattened names** (filename only, no folder structure):

| Local Path | Blob Name |
|------------|-----------|
| `/data/reports/2024/report.pdf` | `report.pdf` |
| `/data/archive/report.pdf` | `report.pdf` (overwrites previous) |

If multiple files have the same name, later files overwrite earlier ones.

## Progress Tracking

The `upload-progress.json` file tracks:

- Completed files (path, blob name, checksum, timestamp)
- Failed files (with error messages)
- Start and completion timestamps

Progress is saved atomically (write to temp file, then rename) to prevent corruption if interrupted.

### Resuming After Interruption

Simply run the service again. It will:
1. Load existing progress
2. Skip files already completed (matching path + checksum)
3. Re-attempt failed files
4. Continue with remaining files

If a file's content changed (different checksum), it will be re-uploaded.

## Logging

Logs are written to:
- **Console**: Always enabled
- **Seq**: Configure the server URL in `appsettings.json` (default: `http://localhost:5341`)

To run Seq locally with Docker:

```bash
docker run -d --name seq -p 5341:80 datalust/seq:latest
```

Then view logs at http://localhost:5341

## Generating a SAS Token

In Azure Portal:
1. Navigate to your Storage Account
2. Go to **Shared access signature** under Security + networking
3. Configure:
   - Allowed services: Blob
   - Allowed resource types: Object
   - Allowed permissions: Write, Create
   - Set expiry date
4. Click **Generate SAS and connection string**
5. Copy the **SAS token** (starts with `?sv=`)

Or using Azure CLI:

```bash
az storage container generate-sas \
  --account-name youraccount \
  --name yourcontainer \
  --permissions cw \
  --expiry 2025-12-31 \
  --output tsv
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "ContainerUrl is not configured" | Update `appsettings.json` with your Azure container URL |
| "SasToken is not configured" | Add your SAS token to config or user secrets |
| "Source folder does not exist" | Verify paths in `SourceFolders` are correct |
| 403 Forbidden errors | Check SAS token has write permissions and hasn't expired |
| 429 Too Many Requests | Increase `DelayBetweenFilesMs` to reduce upload rate |

## License

MIT
