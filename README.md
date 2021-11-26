# DetourSharp.Hosting
DetourSharp.Hosting is a fully managed library for hosting the .NET runtime in remote processes.

# Sample
```cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using DetourSharp.Hosting;

// Start a new Notepad process to load the runtime into.
var process = Process.Start(@"C:\Windows\System32\notepad.exe");

// Wait for the process to initialize.
process.WaitForInputIdle();

// The RemoteRuntime class will load the .NET runtime into the
// process but it will not perform initialization immediately.
using var runtime = new RemoteRuntime(process);

// Initialize the runtime.
runtime.Initialize($"{typeof(Program).Assembly.GetName().Name}.runtimeconfig.json");

// Invoke a method in the remote runtime.
runtime.Invoke(((Delegate)ShowMessageBox).Method, ("Hello, world!", "Success"));

// We can only pass one parameter, so we use a tuple to pass multiple values.
static void ShowMessageBox((string Message, string Caption) parameters)
{
    _ = MessageBoxW(IntPtr.Zero, parameters.Message, parameters.Caption, 0);

    [DllImport("user32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
```
