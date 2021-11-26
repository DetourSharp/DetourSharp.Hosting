namespace DetourSharp.Hosting;

/// <summary>Provides an unmanaged interface to allow the host process to interact with a remote runtime.</summary>
struct RuntimeHostInterface
{
    public ThreadStartPtr64<ushort> InvokeAction;
    public ThreadStartPtr64<ushort> InvokeActionT;
    public ThreadStartPtr64<ushort> LoadAssemblyFromPath;
    public ThreadStartPtr64<ushort> LoadAssemblyFromBytes;
}

struct InvokeActionParameters
{
    public string TypeName;
    public string MethodName;
}

struct InvokeActionTParameters
{
    public string TypeName;
    public string MethodName;
    public string ParameterTypeName;
    public string ParameterJson;
}

struct LoadAssemblyFromBytesParameters
{
    public ulong Buffer;
    public int Length;
}
