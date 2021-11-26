using System;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
namespace DetourSharp.Hosting;

/// <summary>Provides methods for managing Windows handles.</summary>
[SupportedOSPlatform("windows")]
unsafe readonly struct Win32Handle : IDisposable
{
    /// <summary>The handle value.</summary>
    public readonly HANDLE Handle;

    /// <summary>Initializes a new <see cref="Win32Handle"/> instance.</summary>
    public Win32Handle(HANDLE handle)
    {
        Handle = handle;
    }

    /// <summary>Defines an implicit conversion from <see cref="Win32Handle"/> to <see cref="HANDLE"/>.</summary>
    public static implicit operator HANDLE(Win32Handle winHandle)
    {
        return winHandle.Handle;
    }

    /// <summary>Defines an implicit conversion from <see cref="HANDLE"/> to <see cref="Win32Handle"/>.</summary>
    public static implicit operator Win32Handle(HANDLE handle)
    {
        return new Win32Handle(handle);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CloseHandle(Handle);
    }
}
