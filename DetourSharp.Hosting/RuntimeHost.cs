using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
namespace DetourSharp.Hosting;

static unsafe class RuntimeHost
{
    const BindingFlags AllStaticMembers = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        IncludeFields = true
    };

    [UnmanagedCallersOnly]
    public static void Initialize(void* context, RuntimeHostInterface* hostInterface)
    {
        *hostInterface = new RuntimeHostInterface
        {
            InvokeAction          = new(&InvokeAction),
            InvokeActionT         = new(&InvokeActionT),
            LoadAssemblyFromPath  = new(&LoadAssemblyFromPath),
            LoadAssemblyFromBytes = new(&LoadAssemblyFromBytes),
        };
    }

    [UnmanagedCallersOnly]
    static uint LoadAssemblyFromPath(ushort* json)
    {
        try
        {
            var assemblyPath = Deserialize<string>(json)!;
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            return 0;
        }
        catch (Exception ex)
        {
            return (uint)ex.HResult;
        }
    }

    [UnmanagedCallersOnly]
    static uint LoadAssemblyFromBytes(ushort* json)
    {
        try
        {
            var args = Deserialize<LoadAssemblyFromBytesParameters>(json);

            using (var ms = new UnmanagedMemoryStream((byte*)args.Buffer, args.Length))
                AssemblyLoadContext.Default.LoadFromStream(ms);

            return 0;
        }
        catch (Exception ex)
        {
            return (uint)ex.HResult;
        }
    }

    [UnmanagedCallersOnly]
    static uint InvokeAction(ushort* json)
    {
        try
        {
            var args = Deserialize<InvokeActionParameters>(json);

            Type.GetType(args.TypeName, throwOnError: true)!
                .GetMethod(args.MethodName, AllStaticMembers)!
                .Invoke(null, null);

            return 0;
        }
        catch (Exception ex)
        {
            return (uint)ex.HResult;
        }
    }

    [UnmanagedCallersOnly]
    static uint InvokeActionT(ushort* json)
    {
        try
        {
            var args      = Deserialize<InvokeActionTParameters>(json);
            var type      = Type.GetType(args.ParameterTypeName, throwOnError: true)!;
            var method    = Type.GetType(args.TypeName, throwOnError: true)!.GetMethod(args.MethodName, AllStaticMembers)!;
            var parameter = JsonSerializer.Deserialize(args.ParameterJson, type, JsonSerializerOptions);
            method.Invoke(null, new[] { parameter });
            return 0;
        }
        catch (Exception ex)
        {
            return (uint)ex.HResult;
        }
    }

    static T? Deserialize<T>(void* buffer)
    {
        var json  = Marshal.PtrToStringUni((IntPtr)buffer) ?? string.Empty;
        var value = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        return value ?? default;
    }
}
