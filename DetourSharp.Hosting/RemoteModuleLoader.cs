using System;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static DetourSharp.Hosting.Windows;
using static Iced.Intel.AssemblerRegisters;
namespace DetourSharp.Hosting;

/// <summary>Provides methods for loading modules into remote processes.</summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class RemoteModuleLoader : IDisposable
{
    bool disposed;

    readonly HANDLE process;

    readonly RemoteMethod loader;

    /// <summary>The handle of the process to load modules into.</summary>
    public IntPtr Process => process;

    /// <summary>Initializes a new <see cref="RemoteModuleLoader"/> instance.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when <paramref name="process"/> is 64-bit and the host is 32-bit.</exception>
    public RemoteModuleLoader(IntPtr process)
    {
        if (process == IntPtr.Zero)
            throw new ArgumentNullException(nameof(process));

        if (Is64BitProcess((HANDLE)process) && !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Loading modules into a 64-bit process is not supported from a 32-bit host.");

        this.process = (HANDLE)process;
        loader       = CreateLoader((HANDLE)process);
    }

    /// <summary>Loads a library from the given path into the process.</summary>
    /// <returns>The module handle for the loaded library.</returns>
    public IntPtr Load(string path)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);

        // Allocate memory for the file path and LoadLibraryParams.
        // The path's null terminator is included in the size of LoadLibraryParams.
        var size        = (uint)sizeof(LoadLibraryParams) + ((uint)path.Length * sizeof(char));
        using var param = VirtualAlloc.Alloc<byte>(process, size);

        if (param.Address == null)
            ThrowForLastError();

        fixed (char* buffer = path)
            param.Write((ushort*)buffer, sizeof(ulong), (uint)path.Length + 1);

        ulong handle;
        RemoteInvoke(process, loader.Address, param);
        param.Read(&handle);
        return (HMODULE)handle;
    }

    /// <summary>Gets the module handle for an library loaded into the process.</summary>
    public IntPtr GetModule(string name)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        return GetRemoteModuleHandle(process, name);
    }

    /// <summary>Gets the address of a named export in the given module.</summary>
    public IntPtr GetExport(IntPtr module, string name)
    {
        ThrowIfDisposed();

        if (module == IntPtr.Zero)
            throw new ArgumentNullException(nameof(module));

        ArgumentNullException.ThrowIfNull(name);
        return GetRemoteProcAddress(process, (HMODULE)module, name);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        loader.Dispose();
        disposed = true;
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteModuleLoader));
        }
    }

    // Allocates a helper method in the remote process.
    // The purpose of the helper is to call LoadLibraryW and store the result,
    // so that the module handle can be retrieved by the host without iteration.
    static RemoteMethod CreateLoader(HANDLE process)
    {
        HMODULE module = GetRemoteModuleHandle(process, "KERNEL32.DLL");
        void* address  = (void*)GetRemoteProcAddress(process, module, "LoadLibraryW");
        
        return RemoteMethod.Create(process, asm =>
        {
            switch (asm.Bitness)
            {
            case 64:
                asm.push(rsi);
                asm.sub(rsp, 32);
                asm.mov(rsi, rcx);
                asm.add(rcx, 8);
                asm.call((nuint)address);
                asm.mov(__[rsi], rax);
                asm.xor(eax, eax);
                asm.add(rsp, 32);
                asm.pop(rsi);
                asm.ret();
                break;
            case 32:
                asm.push(esi);
                asm.sub(esp, 8);
                asm.mov(esi, __dword_ptr[esp + 16]);
                asm.lea(eax, __[esi + 8]);
                asm.mov(__dword_ptr[esp], eax);
                asm.mov(edx, (uint)address);
                asm.call(edx);
                asm.sub(esp, 4);
                asm.mov(__dword_ptr[esi], eax);
                asm.mov(__dword_ptr[esi + 4], 0);
                asm.xor(eax, eax);
                asm.add(esp, 8);
                asm.pop(esi);
                asm.ret(4);
                break;
            }
        });
    }

#pragma warning disable 0649
    struct LoadLibraryParams
    {
        // [out] The module handle of the loaded library.
        public Ptr64 Module;

        // [in] The file path of the library to load.
        public fixed ushort FileName[1];
    }
#pragma warning restore 0649
}
