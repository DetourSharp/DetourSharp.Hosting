using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.WAIT;
using static TerraFX.Interop.Windows.IMAGE;
using static TerraFX.Interop.Windows.ERROR;
using static TerraFX.Interop.Windows.Windows;
namespace DetourSharp.Hosting;

/// <summary>Provides Windows-specific helper methods.</summary>
[SupportedOSPlatform("windows")]
static unsafe class Windows
{
    /// <summary>Creates a remote thread, waits for it to finish executing and returns its exit code.</summary>
    public static int RemoteInvoke(HANDLE process, void* address, void* parameter)
    {
        int exitCode;

        using (Win32Handle thread = CreateRemoteThread(process, null, 0, (delegate* unmanaged<void*, uint>)address, parameter, 0, null))
        {
            if (thread == HANDLE.NULL || WaitForSingleObject(thread, INFINITE) == WAIT_FAILED)
                ThrowForLastError();

            if (!GetExitCodeThread(thread, (uint*)&exitCode))
                ThrowForLastError();
        }

        return exitCode;
    }

    /// <summary>Throws an exception for the last system error, if any.</summary>
    public static void ThrowForLastError()
    {
        Marshal.ThrowExceptionForHR(HRESULT_FROM_WIN32(Marshal.GetLastSystemError()));
    }

    /// <summary>Gets a value indicating whether the given process is 64-bit.</summary>
    public static bool Is64BitProcess(HANDLE process)
    {
        BOOL wow64Process;

        if (!Environment.Is64BitOperatingSystem)
            return false;

        return !IsWow64Process(process, &wow64Process) || !wow64Process;
    }

    /// <summary>Gets the bitness of the given process.</summary>
    public static int GetProcessBitness(HANDLE process)
    {
        return Is64BitProcess(process) ? 64 : 32;
    }

    /// <summary>Searches for a module with the given name in a remote process and returns a handle.</summary>
    public static HMODULE GetRemoteModuleHandle(HANDLE hProcess, string name)
    {
        fixed (char* pName = name)
        {
            return GetRemoteModuleHandleW(hProcess, (ushort*)pName);
        }
    }

    /// <summary>Searches for a module with the given name in a remote process and returns a handle.</summary>
    public static HMODULE GetRemoteModuleHandleW(HANDLE hProcess, ushort* pName)
    {
        var module   = new MODULEENTRY32W { dwSize = (uint)sizeof(MODULEENTRY32W) };
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetProcessId(hProcess));

        if (snapshot == HANDLE.INVALID_VALUE)
            return HMODULE.NULL;

        if (Module32FirstW(snapshot, &module))
        {
            do
            {
                if (lstrcmpiW(pName, module.szModule) == 0)
                {
                    CloseHandle(snapshot);
                    return module.hModule;
                }

            } while (Module32NextW(snapshot, &module));
        }

        CloseHandle(snapshot);
        return HMODULE.NULL;
    }

    /// <summary>Searches for a named export in a remote process module and returns its address.</summary>
    public static IntPtr GetRemoteProcAddress(HANDLE hProcess, HMODULE hModule, string name)
    {
        var buffer = Marshal.StringToHGlobalAnsi(name);

        try
        {
            return GetRemoteProcAddress(hProcess, hModule, (sbyte*)buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Searches for an ordinal or named export in a remote process module and returns its address.</summary>
    public static IntPtr GetRemoteProcAddress(HANDLE hProcess, HMODULE hModule, sbyte* pName)
    {
        IMAGE_DATA_DIRECTORY entry;
        IMAGE_EXPORT_DIRECTORY export;

        if (!GetRemoteImageDirectory(hProcess, hModule, IMAGE_DIRECTORY_ENTRY_EXPORT, &entry))
            return IntPtr.Zero;
        
        if (!ReadProcessMemory(hProcess, (byte*)hModule + entry.VirtualAddress, &export, (uint)sizeof(IMAGE_EXPORT_DIRECTORY), null))
            return IntPtr.Zero;

        if (HIWORD((nuint)pName) == 0)
            return (IntPtr)GetOrdinalExport(hProcess, hModule, &export, &entry, LOWORD((nuint)pName) - export.Base);

        return (IntPtr)GetNameExport(hProcess, hModule, &export, &entry, pName);
    }

    /// <summary>Gets the <see cref="IMAGE_DATA_DIRECTORY"/> value of the specified directory entry in a remote process module.</summary>
    static bool GetRemoteImageDirectory(HANDLE hProcess, HMODULE hModule, ushort usIndex, IMAGE_DATA_DIRECTORY* pEntry)
    {
        IMAGE_NT_HEADERS ntHeader;

        if (!GetRemoteImageNtHeader(hProcess, hModule, &ntHeader))
            return false;

        if (ntHeader.Is32Bit)
        {
            if (usIndex >= ntHeader.X86.OptionalHeader.NumberOfRvaAndSizes)
                return false;

            var directory = ntHeader.X86.OptionalHeader.DataDirectory;
            var entry     = directory[usIndex];

            if (entry.VirtualAddress == 0)
                return false;

            *pEntry = entry;
            return true;
        }
        else if (ntHeader.Is64Bit)
        {
            if (usIndex >= ntHeader.X64.OptionalHeader.NumberOfRvaAndSizes)
                return false;

            var directory = ntHeader.X64.OptionalHeader.DataDirectory;
            var entry     = directory[usIndex];

            if (entry.VirtualAddress == 0)
                return false;

            *pEntry = entry;
            return true;
        }

        return false;
    }

    /// <summary>Gets the NT header of a remote process module.</summary>
    static bool GetRemoteImageNtHeader(HANDLE hProcess, HMODULE hModule, IMAGE_NT_HEADERS* ntHeader)
    {
        IMAGE_DOS_HEADER dosHeader;

        if (ntHeader == null)
            return false;

        if (!ReadProcessMemory(hProcess, hModule, &dosHeader, (uint)sizeof(IMAGE_DOS_HEADER), null))
            return false;

        if (dosHeader.e_magic != IMAGE_DOS_SIGNATURE)
            goto BadImageFormat;

        if (!ReadProcessMemory(hProcess, (byte*)hModule + dosHeader.e_lfanew, ntHeader, (uint)sizeof(IMAGE_NT_HEADERS), null))
            return false;

        if (ntHeader->IsInvalid)
            goto BadImageFormat;

        return true;

    BadImageFormat:
        Marshal.SetLastSystemError(ERROR_BAD_EXE_FORMAT);
        return false;
    }

    static void* GetNameExport(HANDLE hProcess, HMODULE hModule, IMAGE_EXPORT_DIRECTORY* pExport, IMAGE_DATA_DIRECTORY* pDirectory, sbyte* pName)
    {
        var names = (uint*)NativeMemory.Alloc(pExport->NumberOfNames * sizeof(uint));
        var ordinals = (ushort*)NativeMemory.Alloc(pExport->NumberOfNames * sizeof(ushort));

        // Temporary buffer to store the export name for comparison.
        var length = (uint)lstrlenA(pName);
        var buffer = (sbyte*)NativeMemory.Alloc(length + 1);

        try
        {
            if (!ReadProcessMemory(hProcess, (byte*)hModule + pExport->AddressOfNames, names, pExport->NumberOfNames * sizeof(uint), null))
                return null;

            if (!ReadProcessMemory(hProcess, (byte*)hModule + pExport->AddressOfNameOrdinals, ordinals, pExport->NumberOfNames * sizeof(ushort), null))
                return null;

            for (uint i = 0; i < pExport->NumberOfNames; i++)
            {
                if (!ReadProcessMemory(hProcess, (byte*)hModule + names[i], buffer, length + 1, null))
                    continue;

                if (StrCmpNIA(pName, buffer, (int)length) != 0)
                    continue;

                return GetOrdinalExport(hProcess, hModule, pExport, pDirectory, ordinals[i]);
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
            NativeMemory.Free(ordinals);
            NativeMemory.Free(names);
        }

        return null;
    }

    static void* GetOrdinalExport(HANDLE hProcess, HMODULE hModule, IMAGE_EXPORT_DIRECTORY* pExport, IMAGE_DATA_DIRECTORY* pDirectory, uint dwOrdinal)
    {
        uint func;

        if (dwOrdinal >= pExport->NumberOfFunctions)
            return null;

        if (!ReadProcessMemory(hProcess, (byte*)hModule + pExport->AddressOfFunctions + (sizeof(uint) * dwOrdinal), &func, sizeof(uint), null))
            return null;

        if (func >= pDirectory->VirtualAddress && func < pDirectory->VirtualAddress + pDirectory->Size)
            return GetForwarderExport(hProcess, hModule, pDirectory, func);

        return (byte*)hModule + func;
    }

    static void* GetForwarderExport(HANDLE hProcess, HMODULE hModule, IMAGE_DATA_DIRECTORY* pDirectory, uint dwOffset)
    {
        var length = pDirectory->VirtualAddress + pDirectory->Size - dwOffset;
        var buffer = (byte*)NativeMemory.Alloc(length + 1);

        // Ensure that the buffer is null-terminated.
        buffer[length] = 0;

        try
        {
            if (!ReadProcessMemory(hProcess, (byte*)hModule + dwOffset, buffer, length, null))
                return null;

            var forwarder = new Span<byte>(buffer, (int)length);
            var index     = forwarder.IndexOf((byte)'.');

            if (index == -1)
                return null;

            var moduleName = Marshal.PtrToStringAnsi((IntPtr)buffer, index) + ".dll";
            var exportName = forwarder[(index + 1)..];

            fixed (char* pModuleName = moduleName)
            fixed (byte* pExportName = exportName)
            {
                hModule = GetRemoteModuleHandleW(hProcess, (ushort*)pModuleName);
                return (void*)GetRemoteProcAddress(hProcess, hModule, (sbyte*)pExportName);
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct IMAGE_NT_HEADERS
    {
        public bool IsInvalid => X86.Signature != IMAGE_NT_SIGNATURE;

        public bool Is32Bit => X86.OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC;

        public bool Is64Bit => X64.OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC;

        [FieldOffset(0)]
        public IMAGE_NT_HEADERS32 X86;

        [FieldOffset(0)]
        public IMAGE_NT_HEADERS64 X64;
    }
}
