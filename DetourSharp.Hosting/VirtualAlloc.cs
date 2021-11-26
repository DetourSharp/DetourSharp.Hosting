using System;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.MEM;
using static TerraFX.Interop.Windows.PAGE;
using static TerraFX.Interop.Windows.Windows;
using static DetourSharp.Hosting.Windows;
namespace DetourSharp.Hosting;

/// <summary>Provides methods for managing virtual memory allocations.</summary>
[SupportedOSPlatform("windows")]
unsafe readonly struct VirtualAlloc : IDisposable
{
    /// <summary>The process that owns the address.</summary>
    public readonly HANDLE Process;

    /// <summary>The starting address of the allocation.</summary>
    public readonly void* Address;

    /// <summary>Reserves, commits, or changes the state of a region of memory within the virtual address space of a specified process.</summary>
    public VirtualAlloc(HANDLE hProcess, void* lpAddress, nuint dwSize, uint flAllocationType, uint flProtect)
    {
        Process = hProcess;
        Address = VirtualAllocEx(hProcess, lpAddress, dwSize, flAllocationType, flProtect);
    }

    /// <summary>Reserves and commits a region of memory within the virtual address space of a specified process.</summary>
    public static VirtualAlloc Alloc<T>(HANDLE hProcess, nuint count = 1)
        where T : unmanaged
    {
        return new VirtualAlloc(hProcess, null, (uint)sizeof(T) * count, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
    }

    /// <summary>Reserves and commits a region of memory within the virtual address space of a specified process and writes a value to it.</summary>
    public static VirtualAlloc Alloc<T>(HANDLE hProcess, in T value)
        where T : unmanaged
    {
        var alloc = new VirtualAlloc(hProcess, null, (uint)sizeof(T), MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (alloc.Address == null)
            return alloc;

        fixed (T* pValue = &value)
            alloc.Write(pValue);

        return alloc;
    }

    /// <summary>Reserves and commits a region of memory within the virtual address space of a specified process and writes a buffer to it.</summary>
    public static VirtualAlloc Alloc<T>(HANDLE hProcess, ReadOnlySpan<T> buffer)
        where T : unmanaged
    {
        var alloc = new VirtualAlloc(hProcess, null, (uint)sizeof(T) * (uint)buffer.Length, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (alloc.Address == null)
            return alloc;

        fixed (T* pBuffer = buffer)
            alloc.Write(pBuffer, count: (uint)buffer.Length);

        return alloc;
    }

    /// <summary>Defines an implicit conversion from <see cref="VirtualAlloc"/> to <see cref="void*"/>.</summary>
    public static implicit operator void*(VirtualAlloc alloc)
    {
        return alloc.Address;
    }

    /// <summary>Writes a structure of type <typeparamref name="T"/> into the region of memory.</summary>
    public void Write<T>(T* value, nuint offset = 0, nuint count = 1)
        where T : unmanaged
    {
        if (!WriteProcessMemory(Process, (byte*)Address + offset, value, (uint)sizeof(T) * count, null))
        {
            ThrowForLastError();
        }
    }

    /// <summary>Reads a structure of type <typeparamref name="T"/> from the region of memory.</summary>
    public void Read<T>(T* destination, nuint offset = 0, nuint count = 1)
        where T : unmanaged
    {
        if (!ReadProcessMemory(Process, (byte*)Address + offset, destination, (uint)sizeof(T) * count, null))
        {
            ThrowForLastError();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        VirtualFreeEx(Process, Address, 0, MEM_RELEASE);
    }
}
