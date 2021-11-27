using System;
using System.Runtime.Versioning;
using System.Collections.Generic;
using TerraFX.Interop.Windows;
namespace DetourSharp.Hosting;

[SupportedOSPlatform("windows")]
sealed unsafe class RemoteAllocator : IDisposable
{
    bool disposed;

    readonly HANDLE process;

    readonly List<VirtualAlloc> allocations;

    public RemoteAllocator(HANDLE process)
    {
        this.process = process;
        allocations  = new List<VirtualAlloc>();
    }

    public T* Alloc<T>()
        where T : unmanaged
    {
        ThrowIfDisposed();
        var allocation = VirtualAlloc.Alloc<T>(process);
        allocations.Add(allocation);
        return (T*)allocation.Address;
    }

    public T* Alloc<T>(ReadOnlySpan<T> buffer, bool terminate = false)
        where T : unmanaged
    {
        ThrowIfDisposed();
        var allocation = VirtualAlloc.Alloc(process, buffer, terminate);
        allocations.Add(allocation);
        return (T*)allocation.Address;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (VirtualAlloc alloc in allocations)
            alloc.Dispose();

        disposed = true;
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteAllocator));
        }
    }
}
