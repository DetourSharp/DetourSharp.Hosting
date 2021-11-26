using System.Runtime.InteropServices;
namespace DetourSharp.Hosting;

static unsafe class HostFxr
{
    [DllImport("hostfxr", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hostfxr_initialize_for_runtime_config(void* runtime_config_path, void* parameters, void** context);

    [DllImport("hostfxr", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hostfxr_get_runtime_delegate(void* host_context_handle, int type, void** @delegate);

    [DllImport("hostfxr", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hostfxr_close(void* host_context_handle);

    public const int UNMANAGEDCALLERSONLY_METHOD = -1;

    public const int hdt_com_activation = 0;

    public const int hdt_load_in_memory_assembly = 1;

    public const int hdt_winrt_activation = 2;

    public const int hdt_com_register = 3;

    public const int hdt_com_unregister = 4;

    public const int hdt_load_assembly_and_get_function_pointer = 5;

    public const int hdt_get_function_pointer = 6;
}
