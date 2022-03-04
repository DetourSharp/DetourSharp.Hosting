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
    
    readonly int processId;

    readonly RemoteMethod loader;

    /// <summary>The ID of the process to load modules into.</summary>
    public int ProcessId => processId;

    /// <summary>Initializes a new <see cref="RemoteModuleLoader"/> instance.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when the target process is 64-bit and the host is 32-bit.</exception>
    public RemoteModuleLoader(int processId)
    {
        if (Is64BitProcess(processId) && !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Loading modules into a 64-bit process is not supported from a 32-bit host.");

        this.processId = processId;
        EnsureInitialized(processId);
        loader = CreateLoader(processId);
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
        using var param = VirtualAlloc.Alloc<byte>(processId, size);

        if (param.Address == null)
            ThrowForLastError();

        fixed (char* buffer = path)
            param.Write((ushort*)buffer, sizeof(ulong), (uint)path.Length + 1);

        ulong handle;
        RemoteInvoke(processId, loader.Address, param);
        param.Read(&handle);
        return (HMODULE)handle;
    }

    /// <summary>Gets the module handle for an library loaded into the process.</summary>
    public IntPtr GetModule(string name)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        return GetRemoteModuleHandle(processId, name);
    }

    /// <summary>Gets the address of a named export in the given module.</summary>
    public IntPtr GetExport(IntPtr module, string name)
    {
        ThrowIfDisposed();

        if (module == IntPtr.Zero)
            throw new ArgumentNullException(nameof(module));

        ArgumentNullException.ThrowIfNull(name);
        return GetRemoteProcAddress(processId, (HMODULE)module, name);
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
    static RemoteMethod CreateLoader(int processId)
    {
        HMODULE module = GetRemoteModuleHandle(processId, "KERNEL32.DLL");
        void* address  = (void*)GetRemoteProcAddress(processId, module, "LoadLibraryW");
        
        return RemoteMethod.Create(processId, asm =>
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

    // If a process was created suspended, we need to ensure it has been initialized.
    // This can be achieved by creating a remote thread with an empty function, which
    // will result in LdrInitializeThunk being called to initialize the process.
    static void EnsureInitialized(int processId)
    {
        using var initializer = RemoteMethod.Create(processId, asm =>
        {
            asm.xor(eax, eax);

            if (asm.Bitness == 64)
                asm.ret();
            else
                asm.ret(4);
        });

        RemoteInvoke(processId, initializer.Address, null);
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
