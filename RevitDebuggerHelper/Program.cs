using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace RevitDebuggerHelper
{
    /// <summary>
    /// .NET Framework 4.8 console application that attaches Visual Studio debugger to a specific Revit process.
    /// This helper is needed because .NET Core/5+ doesn't support COM interop for Visual Studio DTE.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Parse command line arguments
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: RevitDebuggerHelper.exe <ProcessId> [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --find-revit [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach <ProcessId> [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach-all [--vs-process <VSProcessId>]");
                    Console.WriteLine("");
                    Console.WriteLine("  --vs-process <VSProcessId>    Prefer the Visual Studio instance with this process ID");
                    return 1;
                }

                // Parse Visual Studio process ID if provided
                int? vsProcessId = null;
                var filteredArgs = new List<string>();
                
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--vs-process" && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[i + 1], out int vsProcId))
                        {
                            vsProcessId = vsProcId;
                        }
                        i++; // Skip the next argument as it's the VS process ID
                    }
                    else
                    {
                        filteredArgs.Add(args[i]);
                    }
                }

                if (filteredArgs.Count == 0)
                {
                    Console.WriteLine("Usage: RevitDebuggerHelper.exe <ProcessId> [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --find-revit [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach <ProcessId> [--vs-process <VSProcessId>]");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach-all [--vs-process <VSProcessId>]");
                    Console.WriteLine("");
                    Console.WriteLine("  --vs-process <VSProcessId>    Prefer the Visual Studio instance with this process ID");
                    return 1;
                }

                if (filteredArgs[0] == "--find-revit")
                {
                    return FindAndAttachToRevit(vsProcessId);
                }

                if (filteredArgs[0] == "--detach")
                {
                    if (filteredArgs.Count > 1 && int.TryParse(filteredArgs[1], out int detachProcessId))
                    {
                        return DetachFromProcess(detachProcessId, vsProcessId);
                    }
                    else
                    {
                        Console.Error.WriteLine("--detach requires a process ID");
                        return 1;
                    }
                }

                if (filteredArgs[0] == "--detach-all")
                {
                    return DetachFromAllRevitProcesses(vsProcessId);
                }

                if (int.TryParse(filteredArgs[0], out int processId))
                {
                    return AttachToProcess(processId, vsProcessId);
                }

                Console.Error.WriteLine("Invalid process ID: " + filteredArgs[0]);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
                }
                return 3;
            }
        }

        /// <summary>
        /// Finds the first Revit process and attaches the debugger to it
        /// </summary>
        /// <param name="vsProcessId">Optional Visual Studio process ID to prefer when selecting VS instance</param>
        /// <returns>Exit code (0 = success)</returns>
        static int FindAndAttachToRevit(int? vsProcessId = null)
        {
            var revitProcesses = Process.GetProcessesByName("Revit");
            if (revitProcesses.Length == 0)
            {
                Console.Error.WriteLine("No Revit process found.");
                return 2;
            }

            var proc = revitProcesses[0];
            Console.WriteLine($"Found Revit process ID: {proc.Id}");
            return AttachToProcess(proc.Id, vsProcessId);
        }

        /// <summary>
        /// Detaches debugger from a specific process ID
        /// </summary>
        /// <param name="processId">The process ID to detach from</param>
        /// <param name="vsProcessId">Optional Visual Studio process ID to prefer when selecting VS instance</param>
        /// <returns>Exit code (0 = success)</returns>
        static int DetachFromProcess(int processId, int? vsProcessId = null)
        {
            try
            {
                Console.WriteLine($"Attempting to detach debugger from process {processId}...");

                dynamic dte = GetVisualStudioDTE(vsProcessId);
                if (dte == null)
                {
                    Console.Error.WriteLine("Could not find running Visual Studio instance.");
                    return 1;
                }

                var version = dte.Version;
                Console.WriteLine($"Found Visual Studio DTE: {version}");

                // Get debugger using dynamic property access
                dynamic debugger = dte.Debugger;
                if (debugger == null)
                {
                    Console.Error.WriteLine("Could not access Visual Studio debugger.");
                    return 1;
                }

                // Check debugger state
                try
                {
                    var currentMode = debugger.CurrentMode;
                    Console.WriteLine($"Visual Studio debugger mode: {currentMode}");
                    
                    // If not debugging, nothing to detach
                    if (currentMode.ToString() == "dbgDesignMode")
                    {
                        Console.WriteLine("Debugger is not currently attached to any process.");
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not determine debugger mode: {ex.Message}");
                }

                // Get debugged processes
                dynamic debuggedProcesses = debugger.DebuggedProcesses;
                if (debuggedProcesses == null)
                {
                    Console.WriteLine("No processes are currently being debugged.");
                    return 0;
                }

                // Find and detach from the specific process
                bool foundProcess = false;
                foreach (dynamic process in debuggedProcesses)
                {
                    try
                    {
                        var debuggedProcessId = process.ProcessID;
                        var processName = process.Name;
                        
                        Console.WriteLine($"Found debugged process: {processName} (ID: {debuggedProcessId})");
                        
                        if (debuggedProcessId == processId)
                        {
                            Console.WriteLine($"Detaching from process: {processName} (ID: {processId})");
                            process.Detach();
                            foundProcess = true;
                            Console.WriteLine($"Successfully detached from process {processId}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error accessing debugged process info: {ex.Message}");
                        continue;
                    }
                }

                if (!foundProcess)
                {
                    Console.WriteLine($"Process {processId} is not currently being debugged.");
                    return 2;
                }

                return 0;
            }
            catch (COMException comEx)
            {
                Console.Error.WriteLine($"COM Error: {comEx.Message} (HRESULT: 0x{comEx.HResult:X8})");
                return 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error detaching from process {processId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return 3;
            }
        }

        /// <summary>
        /// Detaches debugger from all Revit processes
        /// </summary>
        /// <param name="vsProcessId">Optional Visual Studio process ID to prefer when selecting VS instance</param>
        /// <returns>Exit code (0 = success)</returns>
        static int DetachFromAllRevitProcesses(int? vsProcessId = null)
        {
            try
            {
                Console.WriteLine("Attempting to detach debugger from all Revit processes...");

                dynamic dte = GetVisualStudioDTE(vsProcessId);
                if (dte == null)
                {
                    Console.Error.WriteLine("Could not find running Visual Studio instance.");
                    return 1;
                }

                var version = dte.Version;
                Console.WriteLine($"Found Visual Studio DTE: {version}");

                dynamic debugger = dte.Debugger;
                if (debugger == null)
                {
                    Console.Error.WriteLine("Could not access Visual Studio debugger.");
                    return 1;
                }

                try
                {
                    var currentMode = debugger.CurrentMode;
                    Console.WriteLine($"Visual Studio debugger mode: {currentMode}");
                    
                    if (currentMode.ToString() == "dbgDesignMode")
                    {
                        Console.WriteLine("Debugger is not currently attached to any process.");
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not determine debugger mode: {ex.Message}");
                }

                // Get debugged processes
                dynamic debuggedProcesses = debugger.DebuggedProcesses;
                if (debuggedProcesses == null)
                {
                    Console.WriteLine("No processes are currently being debugged.");
                    return 0;
                }

                // Find and detach from all Revit processes
                var revitProcessesToDetach = new List<dynamic>();
                foreach (dynamic process in debuggedProcesses)
                {
                    try
                    {
                        var processName = process.Name;
                        if (processName.IndexOf("Revit", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            revitProcessesToDetach.Add(process);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking process name: {ex.Message}");
                        continue;
                    }
                }

                if (revitProcessesToDetach.Count == 0)
                {
                    Console.WriteLine("No Revit processes are currently being debugged.");
                    return 0;
                }

                foreach (var process in revitProcessesToDetach)
                {
                    try
                    {
                        var processId = process.ProcessID;
                        var processName = process.Name;
                        Console.WriteLine($"Detaching from Revit process: {processName} (ID: {processId})");
                        process.Detach();
                        Console.WriteLine($"Successfully detached from process {processId}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error detaching from process: {ex.Message}");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error detaching from Revit processes: {ex.Message}");
                return 3;
            }
        }

        /// <summary>
        /// Attaches Visual Studio debugger to a specific process ID
        /// </summary>
        /// <param name="processId">The process ID to attach to</param>
        /// <param name="vsProcessId">Optional Visual Studio process ID to prefer when selecting VS instance</param>
        /// <returns>Exit code (0 = success)</returns>
        static int AttachToProcess(int processId, int? vsProcessId = null)
        {
            try
            {
                Console.WriteLine($"Attempting to attach debugger to process {processId}...");

                dynamic dte = GetVisualStudioDTE(vsProcessId);
                if (dte == null)
                {
                    Console.Error.WriteLine("Could not find running Visual Studio instance.");
                    return 1;
                }

                // Get version using dynamic property access
                var version = dte.Version;
                Console.WriteLine($"Found Visual Studio DTE: {version}");

                // Verify the target process exists
                try
                {
                    var targetProcess = Process.GetProcessById(processId);
                    Console.WriteLine($"Target process: {targetProcess.ProcessName} (ID: {processId})");
                }
                catch (ArgumentException)
                {
                    Console.Error.WriteLine($"Process {processId} not found.");
                    return 2;
                }

                // Get debugger using dynamic property access
                Console.WriteLine("Accessing Visual Studio debugger...");
                dynamic debugger = dte.Debugger;
                if (debugger == null)
                {
                    Console.Error.WriteLine("Could not access Visual Studio debugger.");
                    return 1;
                }

                // Check debugger state
                try
                {
                    var currentMode = debugger.CurrentMode;
                    Console.WriteLine($"Visual Studio debugger mode: {currentMode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not determine debugger mode: {ex.Message}");
                }

                // Get local processes using dynamic property access
                Console.WriteLine("Accessing local processes...");
                dynamic localProcesses = debugger.LocalProcesses;
                if (localProcesses == null)
                {
                    Console.Error.WriteLine("Could not access local processes.");
                    return 1;
                }

                // Find the target process in the local processes collection
                Console.WriteLine($"Searching for process {processId} in local processes...");
                dynamic dteProcess = null;
                int processCount = 0;

                foreach (dynamic process in localProcesses)
                {
                    processCount++;
                    try
                    {
                        // Use dynamic property access instead of GetProperty
                        var processID = process.ProcessID;
                        var processName = process.Name;
                        
                        // Only log when we find the target process, not every process
                        if (processID == processId)
                        {
                            dteProcess = process;
                            Console.WriteLine($"- Match found!");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error accessing process info: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine($"Searched {processCount} local processes.");

                if (dteProcess == null)
                {
                    Console.Error.WriteLine($"Process {processId} not found in Visual Studio debugger's local processes.");
                    Console.Error.WriteLine("This can happen if:");
                    Console.Error.WriteLine("  - The process is not visible to Visual Studio");
                    Console.Error.WriteLine("  - The process is running as a different user");
                    Console.Error.WriteLine("  - Visual Studio doesn't have permission to attach");
                    return 2;
                }

                // Get process name using dynamic property access
                var targetProcessName = dteProcess.Name;
                Console.WriteLine($"Attaching to process: {targetProcessName}");

                // Call Attach method using dynamic method invocation
                Console.WriteLine("Attempting to attach...");
                dteProcess.Attach();

                Console.WriteLine($"Successfully attached debugger to process {processId}");
                return 0;
            }
            catch (COMException comEx)
            {
                Console.Error.WriteLine($"COM Error: {comEx.Message} (HRESULT: 0x{comEx.HResult:X8})");
                
                // Provide specific guidance for common COM errors
                switch ((uint)comEx.HResult)
                {
                    case 0x80010001: // RPC_E_CALL_REJECTED
                        Console.Error.WriteLine("The call was rejected by the callee. Visual Studio may be busy.");
                        break;
                    case 0x80004005: // E_FAIL
                        Console.Error.WriteLine("General failure. Visual Studio may not be in a debuggable state.");
                        break;
                    case 0x80070005: // E_ACCESSDENIED
                        Console.Error.WriteLine("Access denied. Try running as administrator.");
                        break;
                }
                return 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error attaching to process {processId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return 3;
            }
        }

        /// <summary>
        /// Gets the Visual Studio DTE object for the running instance using dynamic/reflection approach.
        /// When multiple Visual Studio instances are running, tries to find the one matching the specified process ID first,
        /// then falls back to finding one debugging the test process, then uses the first available.
        /// </summary>
        /// <param name="vsProcessId">Optional Visual Studio process ID to prefer when selecting VS instance</param>
        /// <returns>DTE object or null if not found</returns>
        static dynamic GetVisualStudioDTE(int? vsProcessId = null)
        {
            // Collect all available Visual Studio instances
            var availableInstances = new List<(dynamic dte, int? processId)>();
            
            // Try different Visual Studio versions (2022, 2019, 2017)
            string[] versions = { "17.0", "16.0", "15.0" };

            foreach (var version in versions)
            {
                Console.WriteLine($"Searching for Visual Studio {version} instances...");
                
                // Get all instances of this version from ROT
                var instances = GetAllVisualStudioInstancesFromROT(version);
                foreach (var instance in instances)
                {
                    try
                    {
                        // Try to get the process ID for this Visual Studio instance
                        int? dteProcessId = null;
                        try
                        {
                            // Get the main window handle and find the process ID using dynamic cast
                            dynamic dteObj = instance;
                            dynamic mainWindow = dteObj.MainWindow;
                            if (mainWindow != null)
                            {
                                var handle = (IntPtr)mainWindow.HWnd;
                                if (handle != IntPtr.Zero)
                                {
                                    GetWindowThreadProcessId(handle, out uint procId);
                                    dteProcessId = (int)procId;
                                    Console.WriteLine($"  Found Visual Studio {version} instance with process ID: {dteProcessId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Could not determine process ID for VS {version} instance: {ex.Message}");
                        }
                        
                        availableInstances.Add((instance, dteProcessId));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error accessing VS {version} instance: {ex.Message}");
                        continue;
                    }
                }
            }

            // Fallback: Try to get any running Visual Studio instance without version specificity
            if (availableInstances.Count == 0)
            {
                try
                {
                    Console.WriteLine("Trying generic Visual Studio DTE as fallback...");
                    var obj = Marshal.GetActiveObject("VisualStudio.DTE");
                    if (obj != null)
                    {
                        Console.WriteLine("Found generic Visual Studio instance");
                        
                        // Try to get the process ID for this generic instance
                        int? dteProcessId = null;
                        try
                        {
                            dynamic dteObj = obj;
                            dynamic mainWindow = dteObj.MainWindow;
                            if (mainWindow != null)
                            {
                                var handle = (IntPtr)mainWindow.HWnd;
                                if (handle != IntPtr.Zero)
                                {
                                    GetWindowThreadProcessId(handle, out uint procId);
                                    dteProcessId = (int)procId;
                                    Console.WriteLine($"  Generic Visual Studio process ID: {dteProcessId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Could not determine process ID for generic VS: {ex.Message}");
                        }
                        
                        availableInstances.Add((obj, dteProcessId));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accessing generic DTE: {ex.Message}");
                }
            }

            if (availableInstances.Count == 0)
            {
                Console.WriteLine("No Visual Studio instances found.");
                return null;
            }

            Console.WriteLine($"Found {availableInstances.Count} Visual Studio instance(s)");

            // If we have a specific Visual Studio process ID, try to find that instance first
            if (vsProcessId.HasValue)
            {
                Console.WriteLine($"Looking for Visual Studio instance with process ID {vsProcessId.Value}...");
                
                foreach (var (dte, dteProcessId) in availableInstances)
                {
                    if (dteProcessId.HasValue && dteProcessId.Value == vsProcessId.Value)
                    {
                        try
                        {
                            var version = dte.Version;
                            Console.WriteLine($"Found preferred Visual Studio instance {version} with process ID {vsProcessId.Value}");
                            return dte;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing preferred VS instance: {ex.Message}");
                            continue;
                        }
                    }
                }
                
                Console.WriteLine($"Visual Studio instance with process ID {vsProcessId.Value} not found. Using first available instance.");
            }

            // If no specific process ID or not found, return the first available instance
            Console.WriteLine($"Using first available Visual Studio instance");
            return availableInstances[0].dte;
        }

        /// <summary>
        /// Gets all Visual Studio instances of a specific version from the Running Object Table (ROT)
        /// </summary>
        /// <param name="version">Visual Studio version (e.g., "17.0")</param>
        /// <returns>List of DTE objects for all instances of the specified version</returns>
        static List<dynamic> GetAllVisualStudioInstancesFromROT(string version)
        {
            var instances = new List<dynamic>();
            IRunningObjectTable rot = null;
            IEnumMoniker enumMoniker = null;

            try
            {
                // Get the Running Object Table
                int hr = GetRunningObjectTable(0, out rot);
                if (hr != 0 || rot == null)
                {
                    Console.WriteLine($"  Failed to get Running Object Table (HRESULT: 0x{hr:X8})");
                    return instances;
                }

                // Enumerate all objects in the ROT
                rot.EnumRunning(out enumMoniker);
                if (enumMoniker == null)
                {
                    Console.WriteLine($"  Failed to enumerate ROT objects");
                    return instances;
                }

                var monikers = new IMoniker[1];
                IntPtr fetchedCount = IntPtr.Zero;
                var vsProgId = $"!VisualStudio.DTE.{version}:";

                // Iterate through all monikers in the ROT
                while (enumMoniker.Next(1, monikers, fetchedCount) == 0)
                {
                    var moniker = monikers[0];
                    if (moniker == null) continue;

                    try
                    {
                        // Get the display name of the moniker
                        IBindCtx bindCtx = null;
                        CreateBindCtx(0, out bindCtx);
                        
                        if (bindCtx != null)
                        {
                            moniker.GetDisplayName(bindCtx, null, out string displayName);
                            
                            // Check if this is a Visual Studio DTE object of the correct version
                            if (displayName != null && displayName.Contains(vsProgId))
                            {
                                Console.WriteLine($"    Found ROT entry: {displayName}");
                                
                                // Get the actual object from ROT
                                hr = rot.GetObject(moniker, out object dteObject);
                                if (hr == 0 && dteObject != null)
                                {
                                    instances.Add(dteObject);
                                    Console.WriteLine($"    Successfully retrieved DTE object");
                                }
                                else
                                {
                                    Console.WriteLine($"    Failed to get object from ROT (HRESULT: 0x{hr:X8})");
                                }
                            }
                            
                            Marshal.ReleaseComObject(bindCtx);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error processing moniker: {ex.Message}");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error accessing Running Object Table: {ex.Message}");
            }
            finally
            {
                // Clean up COM objects
                if (enumMoniker != null)
                {
                    Marshal.ReleaseComObject(enumMoniker);
                }
                if (rot != null)
                {
                    Marshal.ReleaseComObject(rot);
                }
            }

            Console.WriteLine($"  Found {instances.Count} Visual Studio {version} instance(s)");
            return instances;
        }

        // COM imports for Running Object Table access
        [DllImport("ole32.dll")]
        static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Checks if the potential child process is a descendant of the potential parent process
        /// </summary>
        /// <param name="childProcessId">The potential child process ID</param>
        /// <param name="parentProcessId">The potential parent process ID</param>
        /// <returns>True if childProcessId is a descendant of parentProcessId</returns>
        static bool IsChildProcess(int childProcessId, int parentProcessId)
        {
            try
            {
                var childProcess = Process.GetProcessById(childProcessId);
                var currentProcess = childProcess;
                
                // Walk up the process hierarchy
                while (currentProcess != null)
                {
                    try
                    {
                        // Get parent process ID using WMI query or similar
                        var parentPid = GetParentProcessId(currentProcess.Id);
                        if (parentPid == parentProcessId)
                        {
                            return true;
                        }
                        
                        if (parentPid == 0 || parentPid == currentProcess.Id)
                        {
                            break; // Reached the top or circular reference
                        }
                        
                        currentProcess = Process.GetProcessById(parentPid);
                    }
                    catch (ArgumentException)
                    {
                        // Parent process not found or exited
                        break;
                    }
                    catch (Exception)
                    {
                        // Other errors accessing parent process
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Error accessing child process
            }
            
            return false;
        }

        /// <summary>
        /// Gets the parent process ID for a given process ID using WMI
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Parent process ID, or 0 if not found</returns>
        static int GetParentProcessId(int processId)
        {
            try
            {
                // Use WMI to get parent process ID
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    using (var results = searcher.Get())
                    {
                        foreach (System.Management.ManagementObject result in results)
                        {
                            var parentPid = result["ParentProcessId"];
                            if (parentPid != null && uint.TryParse(parentPid.ToString(), out uint pid))
                            {
                                return (int)pid;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // WMI not available or other error
            }
            
            return 0;
        }

        /// <summary>
        /// Helper method to get property value using reflection (kept for compatibility, but not used)
        /// </summary>
        /// <param name="obj">Object to get property from</param>
        /// <param name="propertyName">Name of the property</param>
        /// <returns>Property value or null</returns>
        static object GetProperty(object obj, string propertyName)
        {
            try
            {
                var type = obj.GetType();
                var property = type.GetProperty(propertyName);
                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method to invoke method using reflection (kept for compatibility, but not used)
        /// </summary>
        /// <param name="obj">Object to invoke method on</param>
        /// <param name="methodName">Name of the method</param>
        /// <param name="parameters">Method parameters</param>
        /// <returns>Method result or null</returns>
        static object InvokeMethod(object obj, string methodName, params object[] parameters)
        {
            try
            {
                var type = obj.GetType();
                var method = type.GetMethod(methodName);
                return method?.Invoke(obj, parameters);
            }
            catch
            {
                return null;
            }
        }
    }
}