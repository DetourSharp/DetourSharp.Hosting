using System;
using System.IO;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using Iced.Intel;
using static TerraFX.Interop.Windows.MEM;
using static TerraFX.Interop.Windows.PAGE;
using static TerraFX.Interop.Windows.Windows;
using static DetourSharp.Hosting.Windows;

namespace DetourSharp.Hosting;

/// <summary>Provides methods for allocating executable memory in remote processes.</summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class RemoteMethod : IDisposable
{
    bool disposed;

    readonly HANDLE process;

    readonly void* address;

    /// <summary>The ID of the process that contains the method.</summary>
    public int ProcessId { get; }

    /// <summary>The address of the method's code in the address space of <see cref="Process"/>.</summary>
    public void* Address
    {
        get
        {
            ThrowIfDisposed();
            return address;
        }
    }

    RemoteMethod(HANDLE process, int processId, void* address)
    {
        ProcessId    = processId;
        this.process = process;
        this.address = address;
    }

    /// <summary>Allocates a new function in the process using the provided generator.</summary>
    public static RemoteMethod Create(int processId, Action<Assembler> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        var process = OpenProcess(PROCESS.PROCESS_ALL_ACCESS, true, (uint)processId);

        try
        {
            var asm      = new Assembler(GetProcessBitness(process));
            using var ms = new MemoryStream();

            generator(asm);
            asm.Assemble(new StreamCodeWriter(ms), 0);
            var remote = VirtualAllocEx(process, null, (nuint)(ulong)ms.Length, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

            if (remote is null)
                ThrowForLastError();

            try
            {
                uint protect;

                fixed (byte* buffer = ms.GetBuffer())
                {
                    if (!WriteProcessMemory(process, remote, buffer, (nuint)(ulong)ms.Length, null))
                    {
                        ThrowForLastError();
                    }
                }

                if (!VirtualProtectEx(process, remote, (nuint)(ulong)ms.Length, PAGE_EXECUTE_READ, &protect))
                    ThrowForLastError();

                if (!FlushInstructionCache(process, remote, (nuint)(ulong)ms.Length))
                    ThrowForLastError();

                return new RemoteMethod(process, processId, remote);
            }
            catch
            {
                VirtualFreeEx(process, remote, 0, MEM_RELEASE);
                throw;
            }
        }
        catch
        {
            CloseHandle(process);
            throw;
        }
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteMethod));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        VirtualFreeEx(process, Address, 0, MEM_RELEASE);
        CloseHandle(process);
        disposed = true;
    }
}
