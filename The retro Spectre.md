The retro Spectre.Console live UI has been implemented. Here's a summary of the changes:                                             
                                                                                                                                       
  Files Created:                                                                                                                       
  - Services/IUploadStateService.cs - Interface and UploadState record for thread-safe state management                                
  - Services/UploadStateService.cs - Thread-safe implementation with state change events                                               
  - UI/IConsoleUI.cs - UI abstraction interface                                                                                        
  - UI/RetroTheme.cs - Phosphor green CRT colors and helper methods                                                                    
  - UI/RetroConsoleUI.cs - Spectre.Console Live implementation with ASCII art header, progress bars, and stats                         
  - UI/NullConsoleUI.cs - No-op implementation for CI/CD mode                                                                          
  - UI/ConsoleUIHostedService.cs - Background service to run the UI                                                                    
  - Configuration/UISettings.cs - Configuration class (Enabled, RefreshRateMs)                                                         
                                                                                                                                       
  Files Modified:                                                                                                                      
  - CopyFilesToStorageAccountFolder.csproj - Added Spectre.Console v0.49.1                                                             
  - Worker.cs - Injected IUploadStateService, added state update calls at key points                                                   
  - Program.cs - Added UI service registration, conditional Serilog configuration                                                      
  - appsettings.json - Added UISettings section                                                                                        
                                                                                                                                       
  Features:                                                                                                                            
  - Live-updating retro terminal UI with green phosphor CRT aesthetic                                                                  
  - Progress bars for total, uploaded, skipped, and failed files                                                                       
  - Current file display with size                                                                                                     
  - Elapsed time, throttle delay, and upload rate stats                                                                                
  - Automatic fallback to console logging when:                                                                                        
    - --no-ui flag is passed                                                                                                           
    - UISettings.Enabled = false                                                                                                       
    - CI environment detected (CI, GITHUB_ACTIONS, TF_BUILD)                                                                           
    - Console redirected or no ANSI support                                                                                            
                                                                                                                                       
  To test:                                                                                                                             
  - dotnet run - UI displays and updates live                                                                                          
  - dotnet run -- --no-ui - Falls back to console logging                                                                              
  - Logs still flow to Seq at http://localhost:5341 when UI is enabled    