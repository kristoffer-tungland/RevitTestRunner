using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

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
                    Console.WriteLine("Usage: RevitDebuggerHelper.exe <ProcessId>");
                    Console.WriteLine("       RevitDebuggerHelper.exe --find-revit");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach <ProcessId>");
                    Console.WriteLine("       RevitDebuggerHelper.exe --detach-all");
                    return 1;
                }

                if (args[0] == "--find-revit")
                {
                    return FindAndAttachToRevit();
                }

                if (args[0] == "--detach")
                {
                    if (args.Length > 1 && int.TryParse(args[1], out int detachProcessId))
                    {
                        return DetachFromProcess(detachProcessId);
                    }
                    else
                    {
                        Console.Error.WriteLine("--detach requires a process ID");
                        return 1;
                    }
                }

                if (args[0] == "--detach-all")
                {
                    return DetachFromAllRevitProcesses();
                }

                if (int.TryParse(args[0], out int processId))
                {
                    return AttachToProcess(processId);
                }

                Console.Error.WriteLine("Invalid process ID: " + args[0]);
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
        /// <returns>Exit code (0 = success)</returns>
        static int FindAndAttachToRevit()
        {
            var revitProcesses = Process.GetProcessesByName("Revit");
            if (revitProcesses.Length == 0)
            {
                Console.Error.WriteLine("No Revit process found.");
                return 2;
            }

            var proc = revitProcesses[0];
            Console.WriteLine($"Found Revit process ID: {proc.Id}");
            return AttachToProcess(proc.Id);
        }

        /// <summary>
        /// Detaches debugger from a specific process ID
        /// </summary>
        /// <param name="processId">The process ID to detach from</param>
        /// <returns>Exit code (0 = success)</returns>
        static int DetachFromProcess(int processId)
        {
            try
            {
                Console.WriteLine($"Attempting to detach debugger from process {processId}...");

                dynamic dte = GetVisualStudioDTE();
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
        /// <returns>Exit code (0 = success)</returns>
        static int DetachFromAllRevitProcesses()
        {
            try
            {
                Console.WriteLine("Attempting to detach debugger from all Revit processes...");

                dynamic dte = GetVisualStudioDTE();
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
        /// <returns>Exit code (0 = success)</returns>
        static int AttachToProcess(int processId)
        {
            try
            {
                Console.WriteLine($"Attempting to attach debugger to process {processId}...");

                dynamic dte = GetVisualStudioDTE();
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
        /// Gets the Visual Studio DTE object for the running instance using dynamic/reflection approach
        /// </summary>
        /// <returns>DTE object or null if not found</returns>
        static dynamic GetVisualStudioDTE()
        {
            // Try different Visual Studio versions (2022, 2019, 2017)
            string[] versions = { "17.0", "16.0", "15.0" };

            foreach (var version in versions)
            {
                try
                {
                    Console.WriteLine($"Trying Visual Studio {version}...");
                    var obj = Marshal.GetActiveObject($"VisualStudio.DTE.{version}");
                    if (obj != null)
                    {
                        Console.WriteLine($"Found Visual Studio {version}");
                        return obj;
                    }
                }
                catch (COMException comEx)
                {
                    // This version not running, try next
                    Console.WriteLine($"  Version {version} not available (HRESULT: 0x{comEx.HResult:X8})");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error checking Visual Studio {version}: {ex.Message}");
                    continue;
                }
            }

            // Try to get any running Visual Studio instance without version specificity
            try
            {
                Console.WriteLine("Trying generic Visual Studio DTE...");
                var obj = Marshal.GetActiveObject("VisualStudio.DTE");
                if (obj != null)
                {
                    Console.WriteLine("Found generic Visual Studio instance");
                    return obj;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing generic DTE: {ex.Message}");
            }

            return null;
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