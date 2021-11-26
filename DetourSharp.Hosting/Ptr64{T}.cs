using System;
namespace DetourSharp.Hosting;

/// <summary>A 64-bit typed pointer.</summary>
unsafe readonly struct Ptr64<T>
    where T : unmanaged
{
    readonly ulong value;

    /// <summary>Gets the pointer address.</summary>
    public T* Pointer => (T*)value;

    /// <summary>Gets a reference to the value at the pointer address.</summary>
    public ref T Value => ref *(T*)value;

    /// <summary>Initializes a new <see cref="Ptr64"/> instance.</summary>
    public Ptr64(T* address) => value = (ulong)address;

    /// <summary>Defines an implicit conversion from <typeparamref name="T"/>* to <see cref="Ptr64{T}"/>.</summary>
    public static implicit operator Ptr64<T>(T* ptr) => new(ptr);

    /// <summary>Defines an explicit conversion from <see cref="Ptr64"/> to <see cref="Ptr64{T}"/>.</summary>
    public static explicit operator Ptr64<T>(Ptr64 ptr) => new((T*)ptr);

    /// <summary>Defines an implicit conversion from <see cref="Ptr64{T}"/> to <typeparamref name="T"/>*.</summary>
    public static implicit operator T*(Ptr64<T> ptr) => (T*)ptr.value;

    /// <summary>Defines an implicit conversion from <see cref="Ptr64{T}"/> to <see cref="void"/>*.</summary>
    public static implicit operator void*(Ptr64<T> ptr) => (void*)ptr.value;

    /// <summary>Defines an implicit conversion from <see cref="Ptr64{T}"/> to <see cref="Ptr64"/>.</summary>
    public static implicit operator Ptr64(Ptr64<T> ptr) => new((void*)ptr.value);

    /// <summary>Defines an implicit conversion from <see cref="Ptr64{T}"/> to <see cref="IntPtr"/>.</summary>
    public static implicit operator IntPtr(Ptr64<T> ptr) => (IntPtr)ptr.value;

    /// <summary>Defines an explicit conversion from <see cref="IntPtr"/> to <see cref="Ptr64{T}"/>.</summary>
    public static explicit operator Ptr64<T>(IntPtr ptr) => new((T*)ptr);
}
