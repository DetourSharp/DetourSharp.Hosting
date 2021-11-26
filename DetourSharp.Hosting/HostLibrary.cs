using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.KEY;
using static TerraFX.Interop.Windows.RRF;
using static TerraFX.Interop.Windows.HKEY;
using static TerraFX.Interop.Windows.ERROR;
using static TerraFX.Interop.Windows.Windows;
namespace DetourSharp.Hosting;

/// <summary>Provides methods for locating the .NET runtime library.</summary>
public static class HostLibrary
{
    static readonly string s_ModuleName = GetPlatformModuleName("hostfxr");

    static readonly string s_FrameworkVersion = GetFrameworkVersion();

    /// <summary>Gets the runtime library path with default search options.</summary>
    public static string GetLibraryPath()
    {
        if (!TryGetLibraryPath(out string? path))
            throw new DllNotFoundException();

        return path;
    }

    /// <summary>Gets the runtime library path with default search options.</summary>
    public static bool TryGetLibraryPath([NotNullWhen(true)] out string? path)
    {
        return TryGetLibraryPath(HostLibrarySearchOptions.Default, out path);
    }

    /// <summary>Gets the runtime library path with the given search options.</summary>
    public static string GetLibraryPath(HostLibrarySearchOptions options)
    {
        if (!TryGetLibraryPath(options, out string? path))
            throw new DllNotFoundException();

        return path;
    }

    /// <summary>Gets the runtime library path with the given search options.</summary>
    public static bool TryGetLibraryPath(HostLibrarySearchOptions options, [NotNullWhen(true)] out string? path)
    {
        ArgumentNullException.ThrowIfNull(options);

        // If the target architecutre matches the current process architecture,
        // we can attempt to locate the runtime library within our own process.
        if (options.Architecture == RuntimeInformation.OSArchitecture)
        {
            using var process = Process.GetCurrentProcess();

            if (TryGetLibraryFromProcess(process, out path))
            {
                return true;
            }
        }

        // If the user provided a root directory, attempt to search that.
        if (options.RootDirectory is not null)
        {
            if (TryGetLibraryFromRoot(options.RootDirectory, out path))
                return true;

            // If a root directory was manually provided, we don't attempt to locate one.
            return false;
        }

        // If an assembly path was provided, search its location for the runtime library.
        if (options.AssemblyPath is not null)
        {
            var directory = Path.GetDirectoryName(options.AssemblyPath);

            if (directory is not null && TryGetLibraryFromDirectory(directory, out path))
            {
                return true;
            }
        }

        // Attempt to locate the library from the various DOTNET_ROOT_* environment variables.
        if (TryGetLibraryFromEnvironment(options.Architecture, out path))
            return true;

        // Attempt to locate the library from a .NET installation.
        if (TryGetLibraryFromInstallation(options.Architecture, out path))
            return true;

        path = null;
        return false;
    }

    static bool TryGetLibraryFromRoot(string? root, [NotNullWhen(true)] out string? path)
    {
        path = null;

        if (string.IsNullOrEmpty(root))
            return false;

        var ver = (SemanticVersion?)null;
        var fxr = Path.Combine(root, "host", "fxr");

        if (!Directory.Exists(fxr))
            return false;

        if (TryGetLibraryFromDirectory(Path.Combine(fxr, s_FrameworkVersion), out path))
            return true;

        foreach (string directory in Directory.EnumerateDirectories(fxr))
        {
            if (!SemanticVersion.TryParse(Path.GetFileName(directory), out var dirVer))
                continue;

            if (ver is not null && ver > dirVer)
                continue;

            ver  = dirVer;
            path = directory;
        }

        if (ver is null || path is null)
        {
            path = null;
            return false;
        }

        return TryGetLibraryFromDirectory(path, out path);
    }

    static bool TryGetLibraryFromDirectory(string directory, [NotNullWhen(true)] out string? path)
    {
        path = Path.Combine(directory, s_ModuleName);

        if (File.Exists(path))
            return true;

        path = null;
        return false;
    }

    static bool TryGetLibraryFromProcess(Process process, [NotNullWhen(true)] out string? path)
    {
        if (TryGetModule(process, s_ModuleName, out ProcessModule? hostfxr))
        {
            if (hostfxr.FileName != null)
            {
                path = hostfxr.FileName;
                return true;
            }
        }

        path = null;
        return false;
    }

    static bool TryGetModule(Process process, string moduleName, [NotNullWhen(true)] out ProcessModule? module)
    {
        foreach (ProcessModule processModule in process.Modules)
        {
            if (processModule.ModuleName == moduleName)
            {
                module = processModule;
                return true;
            }
        }

        module = null;
        return false;
    }

    static string GetPlatformModuleName(string name)
    {
        if (OperatingSystem.IsWindows())
            return $"{name}.dll";
        else if (OperatingSystem.IsMacOS())
            return $"lib{name}.dylib";
        else
            return $"lib{name}.so";
    }

    static bool TryGetLibraryFromEnvironment(Architecture arch, [NotNullWhen(true)] out string? path)
    {
        path = null;
        return TryGetRootFromEnvironment(arch, out string? root) && TryGetLibraryFromRoot(root, out path);
    }

    static bool TryGetRootFromEnvironment(Architecture arch, [NotNullWhen(true)] out string? value)
    {
        value = arch switch
        {
            Architecture.X86   => "DOTNET_ROOT_X86",
            Architecture.X64   => "DOTNET_ROOT_X64",
            Architecture.Arm   => "DOTNET_ROOT_ARM",
            Architecture.Arm64 => "DOTNET_ROOT_ARM64",
            Architecture.S390x => "DOTNET_ROOT_S390X",
            _                  => null
        };

        if (value is not null)
        {
            value = Environment.GetEnvironmentVariable(value);

            if (value is not null)
            {
                return true;
            }
        }

        if (arch == RuntimeInformation.OSArchitecture)
        {
            value = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            return value is not null;
        }

        return false;
    }

    static bool TryGetLibraryFromInstallation(Architecture arch, [NotNullWhen(true)] out string? path)
    {
        if (OperatingSystem.IsWindows())
        {
            if (TryGetWindowsInstallationDirectory(arch, out path))
                return TryGetLibraryFromRoot(path, out path);

            return TryGetLibraryFromRoot(GetDefaultWindowsInstallationDirectory(arch), out path);
        }

        if (TryGetUnixInstallationDirectory(arch, out path))
            return TryGetLibraryFromRoot(path, out path);

        return TryGetLibraryFromRoot(GetDefaultUnixInstallationDirectory(arch), out path);
    }

    static bool TryGetArchitectureName(Architecture arch, [NotNullWhen(true)] out string? name)
    {
        name = arch switch
        {
            Architecture.X86   => "x86",
            Architecture.X64   => "x64",
            Architecture.Arm   => "arm",
            Architecture.Arm64 => "arm64",
            Architecture.S390x => "s390x",
            _                  => null
        };

        return name is not null;
    }

    static unsafe bool TryGetWindowsInstallationDirectory(Architecture arch, [NotNullWhen(true)] out string? path)
    {
        if (!TryGetArchitectureName(arch, out string? archName))
            goto Failure;

        fixed (char* pKey   = @$"SOFTWARE\dotnet\Setup\InstalledVersions\{archName}")
        fixed (char* pValue = "InstallLocation")
        {
            uint length;
            HKEY result;

            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, (ushort*)pKey, 0, KEY_READ | KEY_WOW64_32KEY, &result) != ERROR_SUCCESS)
                goto Failure;

            try
            {
                if (RegGetValueW(result, null, (ushort*)pValue, RRF_RT_REG_SZ, null, null, &length) != ERROR_SUCCESS)
                    goto Failure;

                var buffer = NativeMemory.Alloc(length);

                try
                {
                    if (RegGetValueW(result, null, (ushort*)pValue, RRF_RT_REG_SZ, null, buffer, &length) != ERROR_SUCCESS)
                        goto Failure;

                    path = Marshal.PtrToStringUni((IntPtr)buffer);
                    return path is not null;
                }
                finally
                {
                    NativeMemory.Free(buffer);
                }
            }
            finally
            {
                _ = RegCloseKey(result);
            }
        }

    Failure:
        path = null;
        return false;
    }

    static string GetDefaultWindowsInstallationDirectory(Architecture arch)
    {
        string programs;

        if (arch == Architecture.X86)
            programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        else
            programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // TODO: Handle emulated x64.
        // https://github.com/dotnet/runtime/blob/c88c88a5f07325a70322cfc056949e8d52e4a04f/src/native/corehost/hostmisc/pal.windows.cpp#L268-L302
        return Path.Combine(programs, "dotnet");
    }

    static unsafe bool TryGetUnixInstallationDirectory(Architecture arch, [NotNullWhen(true)] out string? path)
    {
        if (!TryGetArchitectureName(arch, out string? archName))
            goto Failure;

        if (TryGetInstallationDirectoryFromFile($"/etc/dotnet/install_location_{archName}", out path))
            return true;

        if (arch == RuntimeInformation.OSArchitecture)
            return TryGetInstallationDirectoryFromFile("/etc/dotnet/install_location", out path);

    Failure:
        path = null;
        return false;
    }

    static string GetDefaultUnixInstallationDirectory(Architecture arch)
    {
        _ = arch;

        // TODO: Handle emulated x64.
        // https://github.com/dotnet/runtime/blob/c88c88a5f07325a70322cfc056949e8d52e4a04f/src/native/corehost/hostmisc/pal.unix.cpp#L530-L551

        if (OperatingSystem.IsMacOS())
            return "/usr/local/share/dotnet";
        else
            return "/usr/share/dotnet";
    }

    static bool TryGetInstallationDirectoryFromFile(string filePath, [NotNullWhen(true)] out string? path)
    {
        if (File.Exists(filePath))
        {
            using var fs = File.OpenRead(filePath);
            using var sr = new StreamReader(fs);
            string? line = sr.ReadLine();

            if (line != null)
            {
                path = line;
                return true;
            }
        }

        path = null;
        return false;
    }

    static string GetFrameworkVersion()
    {
        var attr    = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? string.Empty;
        var index   = version.IndexOf('+');
        return index == -1 ? version : version[..index];
    }
}
