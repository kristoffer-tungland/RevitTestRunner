You're **100% correct to question that** — yes, the code:

```csharp
var current = Process.GetCurrentProcess();
var hwnd = current.MainWindowHandle;
```

will get the **window handle of the current process (your launcher .exe)** — **not** Revit’s.

---

### ✅ Fix: Get the Revit Process and Window Handle

You need to store the **`Process` object returned from `Process.Start`**, and then wait until **Revit’s window is initialized**, then minimize or hide it.

Here’s the **corrected version**:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

public class RevitLauncher
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MINIMIZE = 6; // or use SW_HIDE = 0 to fully hide

    public void LaunchAndMinimizeRevit(string revitExePath, string modelPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = revitExePath,
            Arguments = $"\"{modelPath}\" /nosplash",
            UseShellExecute = false
        };

        Process revitProc = Process.Start(psi);

        // Wait for main window to initialize
        int waitTimeMs = 0;
        while (revitProc.MainWindowHandle == IntPtr.Zero && waitTimeMs < 15000)
        {
            Thread.Sleep(500);
            waitTimeMs += 500;
            revitProc.Refresh();
        }

        if (revitProc.MainWindowHandle != IntPtr.Zero)
        {
            ShowWindow(revitProc.MainWindowHandle, SW_MINIMIZE);
            // Or use SW_HIDE if you truly want to hide the UI
        }
        else
        {
            Console.WriteLine("Revit window handle not found in time.");
        }
    }
}
```

---

### 🧠 Important Notes

* `MainWindowHandle` becomes available **only after the window is shown**, so you need to poll until it’s non-zero.
* Use `/nosplash` to suppress the splash screen.
* Revit **still runs the UI internally**, but this minimizes visual disruption to the user.
* Do **not** use `SW_HIDE` in production unless you're sure — some UI actions may fail if the window is hidden entirely.

---

### ✅ Final Thoughts

You now correctly minimize (or hide) **Revit's** window — not your own launcher. Combine this with a Revit add-in that starts processing on load, and you’ve got a solid local "headless-like" automation setup.

Want a matching Revit add-in sample to run a job after launch and then auto-quit?
