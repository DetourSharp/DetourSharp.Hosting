using System;
namespace DetourSharp.Hosting;

/// <summary>A 64-bit typeless pointer.</summary>
unsafe readonly struct Ptr64
{
    readonly ulong value;

    /// <summary>Gets the pointer address.</summary>
    public void* Pointer => (void*)value;

    /// <summary>Initializes a new <see cref="Ptr64"/> instance.</summary>
    public Ptr64(void* address) => value = (ulong)address;

    /// <summary>Defines an implicit conversion from <see cref="Ptr64"/> to <see cref="void"/>*.</summary>
    public static implicit operator void*(Ptr64 ptr) => (void*)ptr.value;

    /// <summary>Defines an implicit conversion from <see cref="void"/>* to <see cref="Ptr64"/>.</summary>
    public static implicit operator Ptr64(void* ptr) => new(ptr);

    /// <summary>Defines an implicit conversion from <see cref="Ptr64"/> to <see cref="IntPtr"/>.</summary>
    public static implicit operator IntPtr(Ptr64 ptr) => (IntPtr)ptr.value;

    /// <summary>Defines an implicit conversion from <see cref="IntPtr"/> to <see cref="Ptr64"/>.</summary>
    public static implicit operator Ptr64(IntPtr ptr) => new((void*)ptr);
}
