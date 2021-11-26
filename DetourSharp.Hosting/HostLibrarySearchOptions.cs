using System.Runtime.InteropServices;
namespace DetourSharp.Hosting;

/// <summary>Provides configuration options for <see cref="HostLibrary.TryGetLibraryPath(HostLibrarySearchOptions, out string)"/>.</summary>
public sealed class HostLibrarySearchOptions
{
    /// <summary>Gets the default <see cref="HostLibrarySearchOptions"/> instance.</summary>
    public static HostLibrarySearchOptions Default { get; } = new();

    /// <summary>The location of an assembly from a self-contained .NET application.</summary>
    public string? AssemblyPath { get; init; }

    /// <summary>The .NET installation path.</summary>
    public string? RootDirectory { get; init; }

    /// <summary>The architecture to use when searching for <c>hostfxr</c>. Will default to the current process' architecture.</summary>
    public Architecture Architecture { get; init; }

    /// <summary>Initializes a new <see cref="HostLibrarySearchOptions"/> instance.</summary>
    public HostLibrarySearchOptions()
    {
        Architecture = RuntimeInformation.ProcessArchitecture;
    }

    /// <summary>Initializes a new <see cref="HostLibrarySearchOptions"/> instance with the values of another.</summary>
    public HostLibrarySearchOptions(HostLibrarySearchOptions options)
    {
        AssemblyPath  = options.AssemblyPath;
        RootDirectory = options.RootDirectory;
        Architecture  = options.Architecture;
    }
}
