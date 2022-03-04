using System;
using System.Runtime.Versioning;
using System.Collections.Generic;
using TerraFX.Interop.Windows;
namespace DetourSharp.Hosting;

[SupportedOSPlatform("windows")]
sealed unsafe class RemoteAllocator : IDisposable
{
    bool disposed;

    readonly int processId;

    readonly List<VirtualAlloc> allocations;

    public RemoteAllocator(int processId)
    {
        this.processId = processId;
        allocations    = new List<VirtualAlloc>();
    }

    public T* Alloc<T>()
        where T : unmanaged
    {
        ThrowIfDisposed();
        var allocation = VirtualAlloc.Alloc<T>(processId);
        allocations.Add(allocation);
        return (T*)allocation.Address;
    }

    public T* Alloc<T>(ReadOnlySpan<T> buffer, bool terminate = false)
        where T : unmanaged
    {
        ThrowIfDisposed();
        var allocation = VirtualAlloc.Alloc(processId, buffer, terminate);
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
