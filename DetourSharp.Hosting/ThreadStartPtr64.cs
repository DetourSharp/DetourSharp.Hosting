namespace DetourSharp.Hosting;

/// <summary>A 64-bit pointer to a thread start routine.</summary>
unsafe readonly struct ThreadStartPtr64
{
    readonly ulong value;

    /// <summary>Gets the pointer as an unmanaged delegate.</summary>
    public delegate* unmanaged<void*, uint> Invoke => this;

    /// <summary>Initializes a new <see cref="ThreadStartPtr64"/> instance.</summary>
    public ThreadStartPtr64(delegate* unmanaged<void*, uint> address) => value = (ulong)address;

    /// <summary>Defines an implicit conversion from <see cref="ThreadStartPtr64"/> to <see cref="void"/>*.</summary>
    public static implicit operator void*(ThreadStartPtr64 ptr) => (void*)ptr.value;

    /// <summary>Defines an explicit conversion from <see cref="void"/>* to <see cref="ThreadStartPtr64"/>.</summary>
    public static explicit operator ThreadStartPtr64(void* ptr) => new((delegate* unmanaged<void*, uint>)ptr);

    /// <summary>Defines an implicit conversion from <see cref="ThreadStartPtr64"/> to <see langword="delegate"/>* <see langword="unmanaged"/>&lt;<see cref="void"/>*, <see cref="uint"/>&gt;.</summary>
    public static implicit operator delegate* unmanaged<void*, uint>(ThreadStartPtr64 ptr) => (delegate* unmanaged<void*, uint>)ptr.value;

    /// <summary>Defines an implicit conversion from <see langword="delegate"/>* <see langword="unmanaged"/>&lt;<see cref="void"/>*, <see cref="uint"/>&gt; to <see cref="ThreadStartPtr64"/>.</summary>
    public static implicit operator ThreadStartPtr64(delegate* unmanaged<void*, uint> ptr) => new(ptr);
}
