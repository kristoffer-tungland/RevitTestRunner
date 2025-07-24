using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitTestFramework.Common;

/// <summary>
/// Record to hold all RevitFactAttribute configuration values
/// </summary>
public record RevitTestConfiguration(
    string? ProjectGuid = null,
    string? ModelGuid = null,
    string? LocalPath = null,
    Autodesk.Revit.DB.DetachFromCentralOption DetachFromCentral = Autodesk.Revit.DB.DetachFromCentralOption.DoNotDetach,
    int[]? WorksetsToOpen = null,
    string? CloudRegion = null);

public static class RevitTestModelHelper
{
    private static ILogger _logger = FileLogger.ForContext(typeof(RevitTestModelHelper));
    private static readonly object _loggerLock = new object();

    /// <summary>
    /// Sets a pipe-aware logger for test execution
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to use for forwarding logs</param>
    public static void SetPipeAwareLogger(StreamWriter? pipeWriter)
    {
        lock (_loggerLock)
        {
            if (pipeWriter != null)
            {
                _logger = PipeAwareLogger.ForContext(typeof(RevitTestModelHelper), pipeWriter);
                // Add debug log to confirm pipe-aware logger is set
                _logger.LogDebug("RevitTestModelHelper: Pipe-aware logger has been configured");
                _logger.LogInformation("RevitTestModelHelper: Switched to pipe-aware logging for test execution");
            }
            else
            {
                _logger = FileLogger.ForContext(typeof(RevitTestModelHelper));
                _logger.LogDebug("RevitTestModelHelper: Reset to file-only logging");
            }
        }
    }

    /// <summary>
    /// Gets the current logger (thread-safe)
    /// </summary>
    private static ILogger Logger
    {
        get
        {
            lock (_loggerLock)
            {
                return _logger;
            }
        }
    }

    public static Document? OpenModel(UIApplication uiApp, RevitTestConfiguration configuration)
    {
        Logger.LogInformation("=== RevitTestModelHelper.OpenModel - START ===");
        Logger.LogInformation($"Configuration: ProjectGuid={configuration.ProjectGuid}, ModelGuid={configuration.ModelGuid}, LocalPath={configuration.LocalPath}");
        Logger.LogInformation($"DetachFromCentral: {configuration.DetachFromCentral}");
        Logger.LogInformation($"WorksetsToOpen: {(configuration.WorksetsToOpen != null ? $"[{string.Join(", ", configuration.WorksetsToOpen)}]" : "null")}");

        // If no parameters are provided, return the currently active document
        if (string.IsNullOrEmpty(configuration.LocalPath) && 
            string.IsNullOrEmpty(configuration.ProjectGuid) && 
            string.IsNullOrEmpty(configuration.ModelGuid))
        {
            Logger.LogDebug("No model parameters provided - attempting to use active document");
            var activeDoc = uiApp.ActiveUIDocument?.Document;
            if (activeDoc == null)
            {
                Logger.LogWarning("No active document is currently open in Revit.");
                Logger.LogInformation("=== RevitTestModelHelper.OpenModel - END (No Active Document) ===");
                return null;
            }
            
            Logger.LogInformation($"Using currently active model: {activeDoc.Title}");
            LogModelInfo(activeDoc);
            Logger.LogInformation("=== RevitTestModelHelper.OpenModel - END (Active Document) ===");
            return activeDoc;
        }
        
        if (string.IsNullOrEmpty(configuration.LocalPath) && 
            (string.IsNullOrEmpty(configuration.ProjectGuid) || string.IsNullOrEmpty(configuration.ModelGuid)))
        {
            Logger.LogError("Either localPath or both projectGuid and modelGuid must be provided.");
            throw new ArgumentException("Either localPath or both projectGuid and modelGuid must be provided.");
        }
        
        try
        {
            Document? doc = null;
            if (!string.IsNullOrEmpty(configuration.LocalPath))
            {
                Logger.LogInformation("Opening local model...");
                doc = OpenLocalModel(uiApp, configuration);
            }
            else
            {
                Logger.LogInformation("Opening cloud model...");
                doc = OpenCloudModel(uiApp, configuration);
            }

            if (doc != null)
            {
                LogModelInfo(doc);
            }

            Logger.LogInformation("=== RevitTestModelHelper.OpenModel - END (Success) ===");
            return doc;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            // Wrap with more specific context about which operation failed
            var operation = !string.IsNullOrEmpty(configuration.LocalPath) ? "local model" : "cloud model";
            var identifier = !string.IsNullOrEmpty(configuration.LocalPath) ? 
                configuration.LocalPath : $"{configuration.ProjectGuid}:{configuration.ModelGuid}";
            Logger.LogError(ex, $"Failed to open {operation} '{identifier}'");
            Logger.LogInformation("=== RevitTestModelHelper.OpenModel - END (Error) ===");
            throw new InvalidOperationException($"Failed to open {operation} '{identifier}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Logs detailed information about the opened model, including workset information if workshared
    /// </summary>
    private static void LogModelInfo(Document doc)
    {
        Logger.LogInformation("--- Model Information ---");
        Logger.LogInformation($"Document Title: {doc.Title}");
        Logger.LogInformation($"Document Path: {doc.PathName ?? "Not saved"}");
        Logger.LogInformation($"Is Workshared: {doc.IsWorkshared}");
        Logger.LogInformation($"Is Family Document: {doc.IsFamilyDocument}");
        Logger.LogInformation($"Application Version: {doc.Application.VersionName} ({doc.Application.VersionNumber})");

        if (doc.IsWorkshared)
        {
            LogWorksetInformation(doc);
        }
        else
        {
            Logger.LogInformation("Model is not workshared - no workset information available");
        }

        Logger.LogInformation("--- End Model Information ---");
    }

    /// <summary>
    /// Logs detailed workset information for workshared models
    /// </summary>
    private static void LogWorksetInformation(Document doc)
    {
        try
        {
            Logger.LogInformation("--- Workset Information ---");
            
            var worksetTable = doc.GetWorksetTable();
            
            // Try to get central model information
            try
            {
                var centralModelPath = doc.GetCloudModelPath();
                if (centralModelPath != null)
                {
                    Logger.LogInformation($"Central Model Path: {centralModelPath}");
                }
                else
                {
                    var modelPath = doc.GetWorksharingCentralModelPath();
                    if (modelPath != null)
                    {
                        Logger.LogInformation($"Central Model Path: {ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath)}");
                    }
                    else
                    {
                        Logger.LogInformation("Central Model Path: Not available");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Central Model Path: Could not retrieve ({ex.Message})");
            }
            
            var worksets = new FilteredWorksetCollector(doc).WherePasses(new WorksetKindFilter(WorksetKind.UserWorkset)).ToWorksets();
            Logger.LogInformation($"Total Worksets: {worksets.Count}");

            if (worksets.Count > 0)
            {
                Logger.LogInformation("Workset Details:");
                foreach (var workset in worksets.OrderBy(w => w.Id.IntegerValue))
                {
                    var status = workset.IsOpen ? "OPEN" : "CLOSED";
                    var isEditable = workset.IsEditable ? "EDITABLE" : "READ-ONLY";
                    var owner = string.IsNullOrEmpty(workset.Owner) ? "No Owner" : workset.Owner;
                    
                    Logger.LogInformation($"  ID: {workset.Id.IntegerValue}, Name: '{workset.Name}', Status: {status}, {isEditable}, Owner: {owner}");
                    
                    if (workset.Kind != WorksetKind.UserWorkset)
                    {
                        Logger.LogDebug($"    Kind: {workset.Kind}");
                    }
                }

                var openWorksets = worksets.Where(w => w.IsOpen).ToList();
                var closedWorksets = worksets.Where(w => !w.IsOpen).ToList();
                
                Logger.LogInformation($"Open Worksets: {openWorksets.Count}");
                if (openWorksets.Count > 0)
                {
                    Logger.LogInformation($"  Open IDs: [{string.Join(", ", openWorksets.Select(w => w.Id.IntegerValue))}]");
                    Logger.LogInformation($"  Open Names: [{string.Join(", ", openWorksets.Select(w => $"'{w.Name}'"))}]");
                }

                Logger.LogInformation($"Closed Worksets: {closedWorksets.Count}");
                if (closedWorksets.Count > 0)
                {
                    Logger.LogInformation($"  Closed IDs: [{string.Join(", ", closedWorksets.Select(w => w.Id.IntegerValue))}]");
                    Logger.LogInformation($"  Closed Names: [{string.Join(", ", closedWorksets.Select(w => $"'{w.Name}'"))}]");
                }
            }
            
            Logger.LogInformation("--- End Workset Information ---");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve workset information");
        }
    }

    private static Document OpenLocalModel(UIApplication uiApp, RevitTestConfiguration configuration)
    {
        try
        {
            var primaryVersionNumber = uiApp.Application.VersionNumber;
            
            // Replace [RevitVersion] placeholder with actual version number
            var resolvedPath = configuration.LocalPath!.Replace("[RevitVersion]", primaryVersionNumber);
            
            Logger.LogDebug($"Original path: {configuration.LocalPath}");
            if (resolvedPath != configuration.LocalPath)
            {
                Logger.LogDebug($"Resolved path: {resolvedPath}");
            }

            if (!File.Exists(resolvedPath))
            {
                Logger.LogError($"Revit model file not found at path: {resolvedPath}");
                throw new FileNotFoundException($"Revit model file not found at path: {resolvedPath}");
            }

            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(resolvedPath);
            var app = uiApp.Application;

            var opts = new OpenOptions();
            
            // Use the Revit API enum directly (no longer nullable)
            opts.DetachFromCentralOption = configuration.DetachFromCentral;
            Logger.LogInformation($"Opening with DetachFromCentralOption: {configuration.DetachFromCentral}");

            // Configure worksets
            if (configuration.WorksetsToOpen != null && configuration.WorksetsToOpen.Length > 0)
            {
                // Create WorksetConfiguration to close all worksets by default, then open specified ones
                var worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                
                // Create list of WorksetId objects from the integer array
                var worksetIds = configuration.WorksetsToOpen.Select(id => new WorksetId(id)).ToList();
                
                // Open the specified worksets
                worksetConfig.Open(worksetIds);
                opts.SetOpenWorksetsConfiguration(worksetConfig);
                
                Logger.LogInformation($"Opening local model with {worksetIds.Count} specified worksets: [{string.Join(", ", configuration.WorksetsToOpen)}]");
            }
            else
            {
                // Default: close all worksets
                opts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets));
                Logger.LogDebug("Opening local model with all worksets closed (default configuration)");
            }
            
            opts.Audit = false;

            Logger.LogInformation($"Opening local model: {resolvedPath}");
            var doc = app.OpenDocumentFile(modelPath, opts);

            if (doc == null)
            {
                Logger.LogError($"Revit returned null document when opening local model at: {resolvedPath}");
                throw new InvalidOperationException($"Revit returned null document when opening local model at: {resolvedPath}");
            }

            Logger.LogInformation($"Successfully opened local model: {doc.Title}");
            return doc;
        }
        catch (Exception ex) when (!(ex is FileNotFoundException))
        {
            Logger.LogError(ex, $"Failed to open local model '{configuration.LocalPath}'");
            throw new InvalidOperationException($"Failed to open local model '{configuration.LocalPath}': {ex.Message}", ex);
        }
    }

    private static Document OpenCloudModel(UIApplication uiApp, RevitTestConfiguration configuration)
    {
        try
        {
            if (!Guid.TryParse(configuration.ProjectGuid, out var projGuid))
            {
                Logger.LogError($"Invalid project GUID format: '{configuration.ProjectGuid}'");
                throw new ArgumentException($"Invalid project GUID format: '{configuration.ProjectGuid}'");
            }
            
            if (!Guid.TryParse(configuration.ModelGuid, out var modGuid))
            {
                Logger.LogError($"Invalid model GUID format: '{configuration.ModelGuid}'");
                throw new ArgumentException($"Invalid model GUID format: '{configuration.ModelGuid}'");
            }

            // Convert CloudRegion string to ModelPathUtils cloud region
            var revitCloudRegion = configuration.CloudRegion switch
            {
                "US" => ModelPathUtils.CloudRegionUS,
                "EMEA" => ModelPathUtils.CloudRegionEMEA,
                _ => throw new ArgumentOutOfRangeException(nameof(configuration.CloudRegion), 
                    $"Unsupported cloud region: {configuration.CloudRegion}. Supported values are 'US' and 'EMEA'.")
            };

            var cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(revitCloudRegion, projGuid, modGuid);
            var app = uiApp.Application;

            var openOpts = new OpenOptions();
            
            // Use the Revit API enum directly (no longer nullable)
            openOpts.DetachFromCentralOption = configuration.DetachFromCentral;
            Logger.LogInformation($"Opening cloud model with DetachFromCentralOption: {configuration.DetachFromCentral}");
            Logger.LogInformation($"Using cloud region: {configuration.CloudRegion} (Revit: {revitCloudRegion})");

            // Configure worksets
            if (configuration.WorksetsToOpen != null && configuration.WorksetsToOpen.Length > 0)
            {
                // Create WorksetConfiguration to close all worksets by default, then open specified ones
                var worksetConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                
                // Create list of WorksetId objects from the integer array
                var worksetIds = configuration.WorksetsToOpen.Select(id => new WorksetId(id)).ToList();
                
                // Open the specified worksets
                worksetConfig.Open(worksetIds);
                openOpts.SetOpenWorksetsConfiguration(worksetConfig);
                
                Logger.LogInformation($"Opening cloud model with {worksetIds.Count} specified worksets: [{string.Join(", ", configuration.WorksetsToOpen)}]");
            }
            else
            {
                // Default: close all worksets
                openOpts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets));
                Logger.LogDebug("Opening cloud model with all worksets closed (default configuration)");
            }

            Logger.LogInformation($"Opening cloud model: {configuration.ProjectGuid}:{configuration.ModelGuid}");
            var doc = app.OpenDocumentFile(cloudPath, openOpts);

            if (doc == null)
            {
                Logger.LogError($"Revit returned null document when opening cloud model: {configuration.ProjectGuid}:{configuration.ModelGuid}");
                throw new InvalidOperationException($"Revit returned null document when opening cloud model: {configuration.ProjectGuid}:{configuration.ModelGuid}");
            }

            Logger.LogInformation($"Successfully opened cloud model: {doc.Title}");
            return doc;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            Logger.LogError(ex, $"Failed to open cloud model '{configuration.ProjectGuid}:{configuration.ModelGuid}'");
            throw new InvalidOperationException($"Failed to open cloud model '{configuration.ProjectGuid}:{configuration.ModelGuid}': {ex.Message}", ex);
        }
    }
}