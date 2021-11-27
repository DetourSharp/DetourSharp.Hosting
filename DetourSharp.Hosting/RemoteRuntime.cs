using System;
using System.Text.Json;
using System.Reflection;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static Iced.Intel.AssemblerRegisters;
using static DetourSharp.Hosting.Windows;
using static DetourSharp.Hosting.HostFxr;
namespace DetourSharp.Hosting;

/// <summary>Provides methods for managing the .NET runtime in a remote process.</summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class RemoteRuntime : IDisposable
{
    readonly HANDLE process;

    readonly RemoteMethod initializer;

    RuntimeHostInterface hostInterface;

    bool disposed;

    /// <summary>Gets the handle for the process that the runtime is loaded into.</summary>
    public IntPtr Process => process;

    /// <summary>Initializes a new <see cref="RemoteRuntime"/> instance and loads the .NET runtime into the given process.</summary>
    public RemoteRuntime(IntPtr process)
        : this(process, new HostLibrarySearchOptions { Architecture = GetProcessArchitecture((HANDLE)process) })
    {
    }

    /// <summary>Initializes a new <see cref="RemoteRuntime"/> instance and loads the .NET runtime into the given process.</summary>
    public RemoteRuntime(IntPtr process, HostLibrarySearchOptions options)
    {
        if (process == IntPtr.Zero)
            throw new ArgumentNullException(nameof(process));

        ArgumentNullException.ThrowIfNull(options);

        if (GetProcessId((HANDLE)process) == Environment.ProcessId)
            throw new ArgumentException("The target process cannot be the host process.", nameof(process));

        if (GetProcessArchitecture((HANDLE)process) != options.Architecture)
            throw new ArgumentException("The architecture of the target process does not match the host library search options.", nameof(process));

        this.process     = (HANDLE)process;
        using var loader = new RemoteModuleLoader(process);
        var hostfxr      = (HMODULE)loader.Load(HostLibrary.GetLibraryPath(options));
        initializer      = CreateInitializer(loader, hostfxr);
    }

    /// <summary>Initializes the runtime with the provided runtime configuration.</summary>
    public void Initialize(string runtimeConfigurationPath)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(runtimeConfigurationPath);

        if (initializer.Address == null)
            throw new InvalidOperationException("The remote runtime has already been initialized.");

        using var allocator  = new RemoteAllocator(process);
        using var remoteHost = VirtualAlloc.Alloc<RuntimeHostInterface>(process);
        using var parameters = VirtualAlloc.Alloc(process, new InitializerParameters
        {
            RuntimeConfigPath = (ushort*)allocator.Alloc<char>(runtimeConfigurationPath, terminate: true),
            AssemblyPath      = (ushort*)allocator.Alloc<char>(typeof(RuntimeHost).Assembly.Location, terminate: true),
            TypeName          = (ushort*)allocator.Alloc<char>(typeof(RuntimeHost).AssemblyQualifiedName, terminate: true),
            MethodName        = (ushort*)allocator.Alloc<char>(nameof(RuntimeHost.Initialize), terminate: true),
            HostInterface     = (RuntimeHostInterface*)remoteHost.Address,
        });

        // We only want to dispose the initializer if initialization fails, so this code is not wrapped in a using.
        var result = (HostExitCode)Windows.RemoteInvoke(process, initializer.Address, (void*)parameters);

        switch (result)
        {
        case HostExitCode.Success:
        case HostExitCode.Success_HostAlreadyInitialized:
        case HostExitCode.Success_DifferentRuntimeProperties:
            initializer.Dispose();
            break;
        default:
            throw new ExternalException(result.ToString(), (int)result);
        }

        // RuntimeHost.Initialize will write to the HostInterface pointer.
        fixed (RuntimeHostInterface* pHostInterface = &hostInterface)
        {
            remoteHost.Read(pHostInterface);
        }
    }

    /// <summary>Executes the specified method in the remote process.</summary>
    public void Invoke(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        Invoke(method.DeclaringType!.AssemblyQualifiedName!, method.Name);
    }

    /// <summary>Executes the specified method in the remote process.</summary>
    public void Invoke(string typeName, string methodName)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(methodName);

        RemoteInvoke(hostInterface.InvokeAction, new InvokeActionParameters
        {
            TypeName   = typeName,
            MethodName = methodName,
        });
    }

    /// <summary>Executes the specified method in the remote process.</summary>
    public void Invoke<T>(MethodInfo method, T value)
    {
        ArgumentNullException.ThrowIfNull(method);
        Invoke(method.DeclaringType!.AssemblyQualifiedName!, method.Name, value);
    }

    /// <summary>Executes the specified method in the remote process.</summary>
    public void Invoke<T>(string typeName, string methodName, T value)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(methodName);

        RemoteInvoke(hostInterface.InvokeActionT, new InvokeActionTParameters
        {
            TypeName          = typeName,
            MethodName        = methodName,
            ParameterTypeName = typeof(T).AssemblyQualifiedName!,
            ParameterJson     = JsonSerializer.Serialize(value, RuntimeHost.JsonSerializerOptions)
        });
    }

    /// <summary>Loads an assembly file in the remote process.</summary>
    public void LoadAssembly(string assemblyPath)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentNullException.ThrowIfNull(assemblyPath);
        RemoteInvoke(hostInterface.LoadAssemblyFromPath, assemblyPath);
    }

    /// <summary>Loads an assembly file in the remote process.</summary>
    public void LoadAssembly(byte[] rawAssembly)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentNullException.ThrowIfNull(rawAssembly);

        using var buffer = VirtualAlloc.Alloc<byte>(process, rawAssembly);
        RemoteInvoke(hostInterface.LoadAssemblyFromBytes, new LoadAssemblyFromBytesParameters
        {
            Buffer = (ulong)buffer.Address,
            Length = rawAssembly.Length
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        initializer.Dispose();
        disposed = true;
    }

    void RemoteInvoke<T>(void* address, T parameter)
    {
        var json         = JsonSerializer.Serialize(parameter, RuntimeHost.JsonSerializerOptions);
        using var buffer = VirtualAlloc.Alloc<char>(process, json, terminate: true);
        Marshal.ThrowExceptionForHR(Windows.RemoteInvoke(process, address, (void*)buffer));
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteRuntime));
        }
    }

    void ThrowIfNotInitialized()
    {
        if (initializer.Address != null)
        {
            throw new InvalidOperationException("The remote runtime is not initialized.");
        }
    }

    struct InitializerParameters
    {
        public Ptr64<ushort> RuntimeConfigPath;
        public Ptr64<ushort> AssemblyPath;
        public Ptr64<ushort> TypeName;
        public Ptr64<ushort> MethodName;
        public Ptr64<RuntimeHostInterface> HostInterface;
    }

    // uint32_t WINAPI Initialize(InitializerParameters *params);
    //
    // This signature is compatible with LPSTART_THREAD_ROUTINE, allowing it to be
    // passed to the CreateRemoteThread Windows API function.
    //
    // The responsibility of the initializer method is to perform hostfxr initialization
    // and then call RuntimeHost.Initialize, which can retrieve any necessary function
    // pointers and provide them back to the host without need of further assembly.
    // 
    static RemoteMethod CreateInitializer(RemoteModuleLoader loader, HMODULE hostfxr)
    {
        var initialize_for_runtime_config = loader.GetExport(hostfxr, "hostfxr_initialize_for_runtime_config");
        var get_runtime_delegate          = loader.GetExport(hostfxr, "hostfxr_get_runtime_delegate");
        var close                         = loader.GetExport(hostfxr, "hostfxr_close");

        return RemoteMethod.Create(loader.Process, asm =>
        {
            switch (asm.Bitness)
            {
            case 64:
                asm.push(rsi);
                asm.push(rdi);
                asm.sub(rsp, 72);
                asm.mov(rsi, rcx);
                asm.lea(r8, __[rsp + 48]);
                asm.and(__qword_ptr[r8], 0);
                asm.mov(rcx, __qword_ptr[rcx]);
                asm.xor(edx, edx);
                asm.call((ulong)initialize_for_runtime_config);
                asm.mov(edi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.mov(rcx, __qword_ptr[rsp + 48]);
                asm.push(hdt_load_assembly_and_get_function_pointer);
                asm.pop(rdx);
                asm.lea(r8, __[rsp + 64]);
                asm.call((ulong)get_runtime_delegate);
                asm.mov(edi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.mov(rcx, __qword_ptr[rsi + 8]);
                asm.mov(rdx, __qword_ptr[rsi + 16]);
                asm.mov(r8, __qword_ptr[rsi + 24]);
                asm.lea(rax, __[rsp + 56]);
                asm.mov(__qword_ptr[rsp + 40], rax);
                asm.and(__qword_ptr[rsp + 32], 0);
                asm.push(UNMANAGEDCALLERSONLY_METHOD);
                asm.pop(r9);
                asm.call(__qword_ptr[rsp + 64]);
                asm.mov(edi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.mov(rcx, __qword_ptr[rsp + 48]);
                asm.mov(rdx, __qword_ptr[rsi + 32]);
                asm.call(__qword_ptr[rsp + 56]);
                asm.xor(edi, edi);
                asm.AnonymousLabel();
                asm.mov(rcx, __qword_ptr[rsp + 48]);
                asm.call((ulong)close);
                asm.mov(eax, edi);
                asm.add(rsp, 72);
                asm.pop(rdi);
                asm.pop(rsi);
                asm.ret();
                break;
            case 32:
                asm.push(edi);
                asm.push(esi);
                asm.sub(esp, 20);
                asm.mov(edi, __dword_ptr[esp + 32]);
                asm.lea(eax, __[esp + 8]);
                asm.and(__dword_ptr[eax], 0);
                asm.sub(esp, 4);
                asm.push(eax);
                asm.push(0);
                asm.push(__dword_ptr[edi]);
                asm.mov(ebx, (uint)initialize_for_runtime_config);
                asm.call(ebx);
                asm.add(esp, 16);
                asm.mov(esi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.sub(esp, 4);
                asm.lea(eax, __[esp + 20]);
                asm.push(eax);
                asm.push(hdt_load_assembly_and_get_function_pointer);
                asm.push(__dword_ptr[esp + 20]);
                asm.mov(ebx, (uint)get_runtime_delegate);
                asm.call(ebx);
                asm.add(esp, 16);
                asm.mov(esi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.sub(esp, 8);
                asm.lea(eax, __[esp + 20]);
                asm.push(eax);
                asm.push(0);
                asm.push(UNMANAGEDCALLERSONLY_METHOD);
                asm.push(__dword_ptr[edi + 24]);
                asm.push(__dword_ptr[edi + 16]);
                asm.push(__dword_ptr[edi + 8]);
                asm.call(__dword_ptr[esp + 48]);
                asm.add(esp, 8);
                asm.mov(esi, eax);
                asm.test(eax, eax);
                asm.jne(asm.@F);
                asm.sub(esp, 8);
                asm.push(__dword_ptr[edi + 32]);
                asm.push(__dword_ptr[esp + 20]);
                asm.call(__dword_ptr[esp + 28]);
                asm.add(esp, 8);
                asm.xor(esi, esi);
                asm.AnonymousLabel();
                asm.sub(esp, 12);
                asm.push(__dword_ptr[esp + 20]);
                asm.mov(ebx, (uint)close);
                asm.call(ebx);
                asm.add(esp, 16);
                asm.mov(eax, esi);
                asm.add(esp, 20);
                asm.pop(esi);
                asm.pop(edi);
                asm.ret(4);
                break;
            }
        });
    }
}
